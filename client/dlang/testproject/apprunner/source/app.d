import std.stdio;
import core.thread.osthread: Thread;
import core.time: dur;

import testlib = testlibrary;
import testlibrary;

// class MyThing : testlib.ClientIThing01 {
// public:
//     private testlib.IThing01 _stashed;
//     override int someMethod(int x) {
//         writefln("dlang MyThing.someMethod(%d)", x);
//         return x + 101;
//     }
//     override void stashThing(testlib.IThing01 subThing) {
//         _stashed = subThing;
//     }
//     override testlib.IThing01 getStashedThing() {
//         auto ret = _stashed;
//         _stashed = null;
//         // see note in C#, not sure if applicable or ... it's been a long time
//         return ret;
//     }
//     override void testThrow(bool doThrow) {
//         writefln("dlang hello from MyThing.testThrow! dothrow: %s", doThrow);
//         if (doThrow) {
//             throw new testlib.InterfaceMethodException(98_765);
//         }
//     }
//     override void onTimerTick() {
//         writeln("dlang on timer tick!!!");
//     }
//     override void dispose() {
//         writeln("dlang MyThing - perfunctory dispose()");
//     }
// }

void main()
{
    auto x = 1001;
    testlib.init();

    // writefln("sum(10, 11, 12) = %d", testlib.sum(10, 11, 12));

    // writefln("execFunc01(101) = %d", testlib.execFunc01(i => x * i));

    // auto f = testlib.getServerFunc();
    // writefln("getServerFunc()(123) = %d", f(123));

    // auto thing = testlib.makeThing();
    {
        auto thing = using(testlib.makeThing());
        auto res = thing.someMethod(1003);
        testlib.operateOnThing(thing, 111);
    }
    // {
    //     auto thing2 = using2(testlib.makeThing());
    //     auto res = thing2.someMethod(1003);
    //     testlib.operateOnThing(thing2, 111);
    // }
    // writefln("end of 2nd scope");

    // writefln("stashing func ...");
    // testlib.stashFunc((int i) {
    //     writeln("storing this to stashfunc, let's see if it comes back!");
    //     return i + 111;
    // });

    // // auto f2 = testlib.returnStashedFunc();
    // // f2(1738);

    // writefln("releasing func ...");
    // testlib.releaseFunc();

    // auto remoteFunc = testlib.returnStashedFunc();
    // writefln("<func returned from returnStashedFunc>(123) = %d", remoteFunc(123));
    // testlib.stashFunc((int i) {
    //     writeln("storing this to stashfunc, let's see if it comes back!");
    //     return 99;
    // });
    // auto remoteFunc2 = testlib.returnStashedFunc();
    // writefln("<func returned from returnStashedFunc #2>(10) = %d", remoteFunc2(10));

    // auto thing = testlib.makeThing();
    // writefln("thing.someMethod(456): %d", thing.someMethod(456));

    // auto myThing = new MyThing();
    // testlib.operateOnThing(myThing, 109);
    // testlib.testClientThrow(myThing);

    // testlib.startTimer(myThing, 2);
    // writeln("sleeping 5 seconds ...");
    // Thread.sleep(dur!"seconds"(5));
    // testlib.stopTimer();

    testlib.shutdown();
}
