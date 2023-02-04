import std.stdio;
import core.thread.osthread: Thread;
import core.time: dur;

import testlib = testlibrary;
import testlibrary; // : using;

import simpleset: Set;

import std.sumtype;

class MyThing : testlib.ClientIThing01 {
public:
    private testlib.IThing01 _stashed;
    override int someMethod(int x) {
        writefln("dlang MyThing.someMethod(%d)", x);
        return x + 101;
    }
    override void stashThing(testlib.IThing01 subThing) {
        _stashed = subThing;
    }
    override testlib.IThing01 getStashedThing() {
        auto ret = _stashed;
        _stashed = null;
        // see note in C#, not sure if applicable or ... it's been a long time
        return ret;
    }
    override void testThrow(bool doThrow) {
        writefln("dlang hello from MyThing.testThrow! dothrow: %s", doThrow);
        if (doThrow) {
            throw new testlib.InterfaceMethodException(98_765);
        }
    }
    override void onTimerTick() {
        writeln("dlang MyThing.onTimerTick()");
    }
    override void dispose() {
        writeln("dlang MyThing - perfunctory dispose()");
    }
}

void main()
{
    testlib.init();

    // writefln("sum(10, 11, 12) = %d", testlib.sum(10, 11, 12));

    // auto x = 1001;
    // auto f = (int i) { return x * i; };
    // testlib.stashFunc(f);
    // writefln("execFunc01(101) = %d", testlib.execFunc01(f));
    // writeln("----");

    // auto thing2 = new MyThing();
    // auto res = testlib.operateOnThing(thing2, 987);
    // writefln("testlib.operateOnThing(myThing, 987) = %d", res);

    // // auto res2 = testlib.operateOnThing(thing2, 1001);
    // // writefln("testlib.operateOnThing(myThing, 1001) = %d", res2);
    // foreach (value; [1, 0, -1])
    // {
    //     writefln("testNegative(%d)", value);
    //     try {
    //         testlib.testNegative(value);
    //     } catch (testlib.NegativeException e) {
    //         writefln(" ... caught negative exception! : %d", e.value);
    //     }
    // }

    // auto f2 = testlib.getServerFunc();
    // writefln("getServerFunc()(123) = %d", f2(123));

    // // IThing01 + using(...)
    // {
    //     auto serverThing = using(testlib.makeThing());
    //     auto res3 = serverThing.someMethod(1003);
    //     testlib.operateOnThing(serverThing, 111);
    //     // testlib.testClientThrow(serverThing);
    // }

    // auto thing3 = new MyThing();
    // // testlib.testClientThrow(thing3);
    // testlib.saveThing(thing3);
    // auto gotBack = testlib.getSavedThing();
    // // auto x = gotBack.someMethod(10_001);

    // auto myThing = new MyThing();
    // {
    //     testlib.saveThing(myThing);
    //     auto wot = using(testlib.getSavedThing());
    //     // auto thing4 = using(testlib.makeThing());
    //     // testlib.saveThing(thing4);
    //     // auto gotBack2 = using(testlib.getSavedThing());
    // }
    // writeln("end of block");

    // // =============================================

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

    // // auto myThing = new MyThing();
    // testlib.operateOnThing(myThing, 109);
    // testlib.testClientThrow(myThing);

    // testlib.clientStashTest(myThing);

    // testlib.startTimer(myThing, 2);
    // writeln("sleeping 5 seconds ...");
    // Thread.sleep(dur!"seconds"(5));
    // testlib.stopTimer();

    // FancySumType sumValue = FancySumType.Value1.make(9876, "wahooo");
    // takeSumType(sumValue);

    // auto sumValue2 = makeSumType();
    // writeln("dlang makeSumType():");
    // sumValue2.match!(
    //     (FancySumType.Value1 v1) {
    //         writefln(" - got v1: %d/%s", v1.a, v1.b);
    //     },
    //     (FancySumType.Value2 v2) {
    //         writefln(" - got v2: (no params)");
    //     },
    //     (FancySumType.Value3 v3) {
    //         writefln(" - got v3: %s", v3.x);
    //     }
    // );

    auto set1 = testlib.makeSet();
    writefln("set1: %s", set1);

    testlib.takeSet(Set!string("1 - one", "2 - two", "3 - three", "1 - one"));

    testlib.shutdown();
}

