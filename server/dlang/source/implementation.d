module implementation;

import std.functional: toDelegate;
import std.stdio;
import std.format;

import corestuff;
import nativeimplserver;

interface IThing01 {
    int someMethod(int x);
    void stashThing(IThing01 subThing);
    IThing01 getStashedThing();
    void testThrow(bool doThrow);
    void onTimerTick();
}

class NegativeException : Exception {
public:
    int value;
    this(int value) {
        super(format!"Negative Exception: %d"(value));
        this.value = value;
    }
    void push() {
        ni_pushInt(value);
    }
    extern(C) static void buildAndThrow() {
        throw new NegativeException(ni_popInt());
    }
}

class InterfaceMethodException : Exception {
public:
    int value;
    this(int value) {
        super(format!"Interface Method Exception: %d"(value));
        this.value = value;
    }
    void push() {
        ni_pushInt(value);
    }
    extern(C) static void buildAndThrow() {
        throw new InterfaceMethodException(ni_popInt());
    }
}

abstract class ServerThing01 : ServerObject, IThing01 {
public:
    ~this() {
        // nothing yet
    }
    static ServerThing01 getById(int id) {
        auto obj = ServerObject.getById(id);
        return cast(ServerThing01)obj;
    }
    // you need to inherit from this and implement the rest yourself
}

__gshared const int global01 = 101;
__gshared const int anotherGlobal = 202;
__gshared const int lastIntGlobal = 3003;

alias IntToIntFunc = int delegate(int);

int sum(int a, int b, int c) {
    return a + b + c;
}

int execFunc01(IntToIntFunc func) {
    return func(101);
}

int defaultStashedFunc(int x) {
    writeln("defaultStashedFunc: x * 1001");
    stdout.flush();
    return x * 1001;
}

__gshared IntToIntFunc stashedFunc = (int x) { return defaultStashedFunc(x); }; // well that's annoting

void stashFunc(IntToIntFunc func) {
    writeln("dlang server: stashFunc(i -> i)");
    stashedFunc = func;
}

void releaseFunc() {
    writeln("dlang server releasing stashed func");
    stashedFunc = (int x) { return defaultStashedFunc(x); };
}

IntToIntFunc returnStashedFunc() {
    writeln("dlang server returning stashed func");
    return stashedFunc;
}

private class RealThing01 : ServerThing01 {
private:
    IThing01 stashed;
public:
    int someMethod(int x) {
        return x * 321;
    }
    void stashThing(IThing01 subThing) {
        stashed = subThing;
    }
    IThing01 getStashedThing() {
        auto ret = stashed;
        stashed = null;
        return ret;
    }
    void testThrow(bool doThrow) {
        if (doThrow) {
            throw new InterfaceMethodException(1234);
        }
    }
    void onTimerTick() {
        writeln("dlang server RealThing01.onTimerTick()");
    }
    ~this() {
        writeln("dlang server real thing 01 finalizer");
    }
}

IThing01 makeThing() {
    return new RealThing01();
}

int operateOnThing(IThing01 thing, int arg) {
    writeln("dlang server operateOnThing:");
    auto res = thing.someMethod(arg);
    writeln(format!" - thing->someMethod(%d) = %d"(arg, res));
    return res;
}

__gshared private IThing01 savedThing;

void saveThing(IThing01 thing) {
    writeln("dlang server saving thing");
    savedThing = thing;
}

IThing01 getSavedThing() {
    writeln("dlang server returning thing");
    auto ret = savedThing;
    savedThing = null;
    return ret;
}

void clientStashTest(IThing01 thing) {
    auto arg = new RealThing01();

    writeln("dlang server clientStashTest: stashing thing ...");
    thing.stashThing(arg);

    writeln("dlang server clientStashTest: getStashedThing");
    auto _unused = thing.getStashedThing();
}

void testNegative(int value) {
    if (value < 0) {
        throw new NegativeException(value);
    }
}

void testClientThrow(IThing01 thing) {
    writeln("dlang servet testClientThrow");
    writeln("  - testThrow(false)");
    thing.testThrow(false);
    writeln("  - testThrow(true)");
    try {
        thing.testThrow(true);
        writeln(" ... uhh dlang server did not catch expected client exception");
    } catch (InterfaceMethodException e) {
        writeln(format!"*** dlang server caught InterfaceMethodException(%d)"(e.value));
    }
}

import core.sys.windows.windows;

__gshared HANDLE timerHandle;
__gshared IThing01 timerThing;
__gshared bool timerAlive;

extern (Windows) { // stdcall
    void timerCallback(PVOID lpParameter, BOOLEAN timerOrWaitFired) {
        writeln("dlang server timer callback!");
        if (timerAlive) {
            timerThing.onTimerTick();
        } else {
            writeln("dlang server timer: (ignoring tick with timerAlive = false)");
        }
    }
}

void startTimer(IThing01 thing, int freq) {
    timerThing = thing;
    timerAlive = true;
    if (!CreateTimerQueueTimer(&timerHandle, NULL, &timerCallback, NULL, 0, 1000 / freq, 0)) {
        writeln("dlang startTimer: timer creation failed");
    }
}

void stopTimer() {
    timerAlive = false;
    DeleteTimerQueueTimer(NULL, timerHandle, INVALID_HANDLE_VALUE);
    timerThing = null;
    writeln("dlang server stopTimer() done");
}
