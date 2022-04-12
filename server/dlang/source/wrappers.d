module wrappers;
import std.format;
import std.stdio;
import std.string: toStringz;
import core.runtime: Runtime;

import corestuff;
import nativeimplserver;
import implementation;

private: // ==================================================================================================

size_t delegateKey(T)(T d) {
    return d.ptr ? cast(size_t)d.ptr : cast(size_t)d.funcptr; // use context first if it exists, otherwise func ptr
}

IThing01 IThing01__pop() {
    bool isClientId;
    auto id = ni_popInstance(&isClientId);
    if (isClientId) {
        return new ClientThing01(id);
    } else {
        return ServerThing01.getById(id);
    }
}

void IThing01__push(IThing01 thing, bool isReturn) {
    // we know this to be a Pushable because ClientThings/ServerThings are the only things that should ever exist (always inherit from ServerThing01 for your own - rename to BaseThing01?)
    auto pushable = cast(Pushable)thing;
    pushable.push(isReturn);
}

class ClientThing01 : ClientObject, IThing01 {
    this(int id) {
        super(id);
    }
    int someMethod(int x) {
        ni_pushInt(x);
        invokeMethod(ithing_someMethod);
        return ni_popInt();
    }
    void stashThing(IThing01 subThing) {
        IThing01__push(this, false);
        invokeMethod(ithing_stashThing);
    }
    IThing01 getStashedThing() {
        writeln("dlang ClientThing01.getStashedThing");
        invokeMethod(ithing_getStashedThing);
        auto ret = IThing01__pop();
        writeln("== dlang server before clearClientSafetyArea ====");
        ni_clearClientSafetyArea();
        writeln("== dlang server after clearClientSafetyArea ====");
        return ret;
    }
    void testThrow(bool doThrow) {
        ni_pushBool(doThrow);
        invokeMethodWithExceptions(ithing_testThrow);
    }
    void onTimerTick() {
        invokeMethod(ithing_onTimerTick);
    }
}

// ==============================================================
// library wrapper stuff

__gshared ni_InterfaceMethodRef ithing_someMethod;
__gshared ni_InterfaceMethodRef ithing_stashThing;
__gshared ni_InterfaceMethodRef ithing_getStashedThing;
__gshared ni_InterfaceMethodRef ithing_testThrow;
__gshared ni_InterfaceMethodRef ithing_onTimerTick;
__gshared ni_ExceptionRef negativeException;
__gshared ni_ExceptionRef interfaceMethodException;

extern (C) {
    void sumWrapper() {
        auto a = ni_popInt();
        auto b = ni_popInt();
        auto c = ni_popInt();
        ni_pushInt(sum(a, b, c));
    }

    void execFunc01Wrapper() {
        auto id = ni_popClientFunc();
        auto cf = new ClientFunc(id);
        auto func = (int x) {
            ni_pushInt(x);
            cf.remoteExec();
            return ni_popInt();
        };
        ni_pushInt(execFunc01(func));
    }

    void stashFuncWrapper() {
        auto id = ni_popClientFunc();
        auto cf = new ClientFunc(id);
        auto func = (int x) {
            ni_pushInt(x);
            cf.remoteExec();
            return ni_popInt();
        };
        stashFunc(func);
    }

    void releaseFuncWrapper() {
        releaseFunc();
    }

    void returnStashedFuncWrapper() {
        // note this always wraps with an indirect/wrapped server func - no short-circuiting possible with function values going across client/server boundary
        // (because no way to pull apart a delegate to see what's inside, distinguishing client/server funcs)
        // ie, if for some reason you're sending and returning functions across the boundary,
        // it will keep being wrapped over and over again! needless to say, very bad for performance :)
        // if you need that kind of behavior, use a single-method interface instead - will properly be short-circuited across the boundary

        // get the native function value
        auto f = returnStashedFunc();

        // push the id for the wrapper of that function on the stack
        // client side will be able to invoke this wrapper whenever
        auto wrapper = () {
            auto x = ni_popInt();
            auto res = f(x);
            ni_pushInt(res);
        };
        pushServerFuncVal(wrapper, delegateKey(wrapper));
    }

    void makeThingWrapper() {
        auto thing = makeThing();
        IThing01__push(thing, true);
    }

    void operateOnThingWrapper() {
        auto thing = IThing01__pop(); // could be either client or server object
        auto arg = ni_popInt();
        auto res = operateOnThing(thing, arg);
        ni_pushInt(res);
    }

    // ithing_(name) methods are ONLY called on server objects, the remote side can invoke its own methods directly!

    void ithing_someMethodWrapper(int serverId) {
        auto thing = ServerThing01.getById(serverId);
        auto arg = ni_popInt();
        auto res = thing.someMethod(arg);
        ni_pushInt(res);
    }

    void ithing_stashThingWrapper(int serverId) {
        auto thing = ServerThing01.getById(serverId);
        auto subThing = IThing01__pop();
        thing.stashThing(subThing);
    }

    void ithing_getStashedThingWrapper(int serverId) {
        auto thing = ServerThing01.getById(serverId);
        auto res = thing.getStashedThing();
        IThing01__push(res, true); // safe push - remote will need to clear safe area
    }

    void ithing_testThrowWrapper(int serverId) {
        auto thing = ServerThing01.getById(serverId);
        auto doThrow = ni_popBool();
        try {
            thing.testThrow(doThrow);
        } catch (InterfaceMethodException e) {
            ni_setException(interfaceMethodException);
            e.push();
        }
    }

    void ithing_onTimerTickWrapper(int serverId) {
        auto thing = ServerThing01.getById(serverId);
        thing.onTimerTick();
    }

    void saveThingWrapper() {
        auto thing = IThing01__pop();
        saveThing(thing);
    }

    void getSavedThingWrapper() {
        auto thing = getSavedThing();
        IThing01__push(thing, true);
    }

    void clientStashTestWrapper() {
        auto thing = IThing01__pop(); // (probably) client thing
        clientStashTest(thing);
    }

    void constantsFunc() {
        // push in reverse order
        ni_pushInt(lastIntGlobal);
        ni_pushInt(anotherGlobal);
        ni_pushInt(global01);
    }

    void testNegativeWrapper() {
        auto arg = ni_popInt();
        try {
            testNegative(arg);
        } catch (NegativeException e) {
            ni_setException(negativeException);
            e.push();
        }
    }

    void testClientThrowWrapper() {
        auto thing = IThing01__pop(); // probably client thing
        testClientThrow(thing);
    }

    void startTimerWrapper() {
        auto thing = IThing01__pop();
        auto freq = ni_popInt();
        startTimer(thing, freq);
    }

    void stopTimerWrapper() {
        stopTimer();
    }

    // ======================================================================================
    // library entry point / registrations 

    int nativeLibraryInit() {
        // dlang runtime init (need .terminate somewhere, maybe?)
        Runtime.initialize();

        writeln("hello from dlang server initLibrary()");
        auto m = ni_registerModule("TestLibrary");

        // module constants
        ni_registerModuleConstants(m, &constantsFunc);

        // module methods
        ni_registerModuleMethod(m, "sum|iii", &sumWrapper);
        ni_registerModuleMethod(m, "execFunc01|(i->i)", &execFunc01Wrapper);
        ni_registerModuleMethod(m, "stashFunc|(i->i)", &stashFuncWrapper);
        ni_registerModuleMethod(m, "releaseFunc|", &releaseFuncWrapper);
        ni_registerModuleMethod(m, "returnStashedFunc|", &returnStashedFuncWrapper);
        ni_registerModuleMethod(m, "makeThing|", &makeThingWrapper);
        ni_registerModuleMethod(m, "operateOnThing|(IThing01)i", &operateOnThingWrapper);
        ni_registerModuleMethod(m, "saveThing|(IThing01)", &saveThingWrapper);
        ni_registerModuleMethod(m, "getSavedThing|", &getSavedThingWrapper);
        ni_registerModuleMethod(m, "clientStashTest|(IThing01)", &clientStashTestWrapper);
        ni_registerModuleMethod(m, "testNegative|i", &testNegativeWrapper);
        ni_registerModuleMethod(m, "testClientThrow|(IThing01)", &testClientThrowWrapper);
        ni_registerModuleMethod(m, "startTimer|(IThing01)i", &startTimerWrapper);
        ni_registerModuleMethod(m, "stopTimer|", &stopTimerWrapper);

        // IThing01 stuff
        auto ithing = ni_registerInterface(m, "IThing01");
        ithing_someMethod = ni_registerInterfaceMethod(ithing, "someMethod|i", &ithing_someMethodWrapper);
        ithing_stashThing = ni_registerInterfaceMethod(ithing, "stashThing|(IThing01)", &ithing_stashThingWrapper);
        ithing_getStashedThing = ni_registerInterfaceMethod(ithing, "getStashedThing|", &ithing_getStashedThingWrapper);
        ithing_testThrow = ni_registerInterfaceMethod(ithing, "testThrow|b", &ithing_testThrowWrapper);
        ithing_onTimerTick = ni_registerInterfaceMethod(ithing, "onTimerTick|", &ithing_onTimerTickWrapper);

        // exceptions
        negativeException = ni_registerException(m, "NegativeException", &NegativeException.buildAndThrow);
        interfaceMethodException = ni_registerException(m, "InterfaceMethodException", &InterfaceMethodException.buildAndThrow);

        stdout.flush(); // for JNI
        return 0; // = OK
    }

    void nativeLibraryShutdown() {
        // dlang runtime shutdown
        writeln("dlang nativeLibraryShutdown()");
        Runtime.terminate();
    }
}

