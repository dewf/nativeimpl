import * as testlibrary from './testlibrary';
import { i32 } from 'typed-numbers';
import { using } from 'using-statement';
import * as nativeImplClient from './nativeimplclient';

testlibrary.init();

console.log("globals:");
console.log("  - global01: " + testlibrary.global01);
console.log("  - anotherGlobal: " + testlibrary.anotherGlobal);
console.log("  - lastIntGlobal: " + testlibrary.lastIntGlobal);

console.log("sum is: " + testlibrary.sum(i32(10), i32(20), i32(30)));
console.log("execfunc01: " + testlibrary.execFunc01(x => i32(x * 3)));
testlibrary.stashFunc(x => {
    console.log("** hello from client stashed func!");
    return i32(x + 1001);
});
const stashed = testlibrary.returnStashedFunc();
console.log("stashed(101) result: " + stashed(i32(101)));
testlibrary.releaseFunc();

console.log("============")

class MyIThing extends testlibrary.ClientIThing01 {
    private _stashed: testlibrary.IThing01;
    public override someMethod(x: i32): i32 {
        console.log("nodejs MyIThing.someMethod(" + x + ")");
        return i32(x + 101);
    }
    public override stashThing(subThing: testlibrary.IThing01): void {
        this._stashed = subThing;
    }
    public override getStashedThing(): testlibrary.IThing01 {
        // clear out _stashed?
        const ret = this._stashed;
        this._stashed = null;
        // if this were a language like C++, we'd have to be concerned
        // if 'ret' were the only living proxy to the remote side ---
        // because after the calling code (client method wrapper in TestLibrary.cs) pushes the object ID,
        // the proxy would be destroyed, releasing the remote side (again, remote stack only holds ID)
        // nonetheless because GC is unpredictable, we still do a 'safe push' in the wrapper code
        return ret;
    }
    public override testThrow(doThrow: boolean): void {
        console.log("nodejs MyIThing.testThrow (" + doThrow + ")");
        if (doThrow) {
            throw new testlibrary.InterfaceMethodException(i32(98765));
        }
    }
    public override onTimerTick(): void {
        console.log("nodejs on timer tick!");
    }

    public override dispose(): void {
        console.log("nodejs MyIThing - perfunctory dispose()");
        super.dispose(); // optional, doesn't do anything yet
    }
}

using(testlibrary.makeThing(), (thing1) => {
    console.log("IThing01 someMethod(123): " + thing1.someMethod(i32(123)));

    thing1.testThrow(false);
    try {
        thing1.testThrow(true);
    } catch (e) {
        if (e instanceof testlibrary.InterfaceMethodException) {
            console.log(`nodejs caught interfacemethodexception in .testthrow (message ${e.message} value ${e.value})`);
        } else {
            console.log("???");
        }
    }

    testlibrary.saveThing(thing1);
    using(testlibrary.getSavedThing(), (thing2) => {
        console.log("t2 someMethod(456): " + thing2.someMethod(i32(456)));
    });
});

using(new MyIThing(), (mything) => {
    console.log("instantiated mything!");
    const res = testlibrary.operateOnThing(mything, i32(987));
    console.log("result of operating on MyIThing: " + res);
});

// exceptions
testlibrary.testNegative(i32(101));
testlibrary.testNegative(i32(1));
testlibrary.testNegative(i32(0));
try {
    testlibrary.testNegative(i32(-1));
} catch (e) {
    if (e instanceof testlibrary.NegativeException) {
        console.log(`Caught negative exception! ${e.message} (${e.value})`);
    }
}

// client throw test + timer
const mything = new MyIThing();
testlibrary.testClientThrow(mything);

// timer stuff:
testlibrary.startTimer(mything, i32(2));
console.log("nodejs sleeping for 5 seconds");
setTimeout(() => {
    testlibrary.stopTimer();
    console.log("nodejs done sleeping / stopped timer");

    // what we'd do in a using block, which doesn't play nicely with this setTimeout business ...
    mything.dispose();

    nativeImplClient.dumpTables();
    testlibrary.shutdown();
    // how is a final tick coming through after this???
}, 5000);
