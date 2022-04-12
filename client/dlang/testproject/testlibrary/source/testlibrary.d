module testlibrary;
import std.stdio;
import std.conv: to;

import nativeimplclient;

private: // =========================================================================================

size_t delegateKey(T)(T d) {
    return d.ptr ? cast(size_t)d.ptr : cast(size_t)d.funcptr; // use context first if it exists, otherwise func ptr
}

ni_ModuleRef _module;
ni_ModuleMethodRef _sumFunc;
ni_ModuleMethodRef _execFunc01;
ni_ModuleMethodRef _stashFunc;
ni_ModuleMethodRef _releaseFunc;
ni_ModuleMethodRef _returnStashedFunc;

ni_InterfaceRef _ithing01;
ni_InterfaceMethodRef _ithing01SomeMethod;
ni_InterfaceMethodRef _ithing01StashThing;
ni_InterfaceMethodRef _ithing01GetStashedThing;
ni_InterfaceMethodRef _ithing01TestThrow;
ni_InterfaceMethodRef _ithing01OnTimerTick;

ni_ModuleMethodRef _makeThing;
ni_ModuleMethodRef _operateOnThing;
ni_ModuleMethodRef _saveThing;
ni_ModuleMethodRef _getSavedThing;

ni_ModuleMethodRef _clientStashTest;

ni_ExceptionRef _negativeException;
ni_ModuleMethodRef _testNegative;

ni_ExceptionRef _interfaceMethodException;
ni_ModuleMethodRef _testClientThrow;
ni_ModuleMethodRef _startTimer;
ni_ModuleMethodRef _stopTimer;

public: // ==========================================================================================

void init() {
    writeln("dlang library init");
    assert(nativeimplclient.init() == 0, "nativeimplclient.init() failed");

    _module = getModule("TestLibrary");
    _sumFunc = getModuleMethod(_module, "sum|iii");
    _execFunc01 = getModuleMethod(_module, "execFunc01|(i->i)");
    _stashFunc = getModuleMethod(_module, "stashFunc|(i->i)");
    _releaseFunc = getModuleMethod(_module, "releaseFunc|");
    _returnStashedFunc = getModuleMethod(_module, "returnStashedFunc|");

    _ithing01 = getInterface(_module, "IThing01");
    _ithing01SomeMethod = getInterfaceMethod(_ithing01, "someMethod|i");
    _ithing01StashThing = getInterfaceMethod(_ithing01, "stashThing|(IThing01)");
    _ithing01GetStashedThing = getInterfaceMethod(_ithing01, "getStashedThing|");
    _ithing01TestThrow = getInterfaceMethod(_ithing01, "testThrow|b");
    _ithing01OnTimerTick = getInterfaceMethod(_ithing01, "onTimerTick|");

    _makeThing = getModuleMethod(_module, "makeThing|");
    _operateOnThing = getModuleMethod(_module, "operateOnThing|(IThing01)i");

    _saveThing = getModuleMethod(_module, "saveThing|(IThing01)");
    _getSavedThing = getModuleMethod(_module, "getSavedThing|");

    _clientStashTest = getModuleMethod(_module, "clientStashTest|(IThing01)");

    setClientMethodWrapper(_ithing01SomeMethod, (ClientObject obj) {
        auto thing = cast(ClientIThing01) obj;
        const x = popInt();
        const res = thing.someMethod(x);
        pushInt(res);
    });
    setClientMethodWrapper(_ithing01StashThing, (ClientObject obj) {
        auto thing = cast(ClientIThing01) obj;
        auto subThing = IThing01__pop();
        thing.stashThing(subThing);
        // no return value
    });
    setClientMethodWrapper(_ithing01GetStashedThing, (ClientObject obj) {
        auto thing = cast(ClientIThing01) obj;
        // no params
        auto ret = cast(Pushable) thing.getStashedThing();
        ret.push(true); // safe push for return, remote side responsible for clearing
    });
    setClientMethodWrapper(_ithing01TestThrow, (ClientObject obj){
        auto thing = cast(ClientIThing01) obj;
        const doThrow = popBool();
        try {
            thing.testThrow(doThrow);
        } catch (InterfaceMethodException e) {
            e.setAndPush();
        }
    });
    setClientMethodWrapper(_ithing01OnTimerTick, (ClientObject obj) {
        writeln("in method wrapper for client timertick");
        auto thing = cast(ClientIThing01) obj;
        // no params
        thing.onTimerTick();
        // no return
    });

    // exception testing
    _negativeException = getException(_module, "NegativeException");
    setExceptionBuilder(_negativeException, () => NegativeException.pop()); // not sure why we can't pass these directly with an &

    _testNegative = getModuleMethod(_module, "testNegative|i");

    _interfaceMethodException = getException(_module, "InterfaceMethodException");
    setExceptionBuilder(_interfaceMethodException, () => InterfaceMethodException.pop()); // same

    _testClientThrow = getModuleMethod(_module, "testClientThrow|(IThing01)");
    _startTimer = getModuleMethod(_module, "startTimer|(IThing01)i");
    _stopTimer = getModuleMethod(_module, "stopTimer|");
}

void shutdown() {
    writeln("dlang library shutdown");
    nativeImplShutdown();
}

class NegativeException : Exception {
    private int _value;
    public int value() = _value;
    public this(int value) {
        super("Negative Value Exception");
        _value = value;
    }
    public void setAndPush() {
        setException(_negativeException);
        pushInt(_value);
    }
    public static NegativeException pop() {
        return new NegativeException(popInt());
    }
}

class InterfaceMethodException : Exception {
    private int _value;
    public int value() = _value;
    public this(int value) {
        super("Interface Method Exception");
        _value = value;
    }
    public void setAndPush() {
        setException(_interfaceMethodException);
        pushInt(_value);
    }
    public static InterfaceMethodException pop() {
        return new InterfaceMethodException(popInt());
    }
}

int sum(int a, int b, int c) {
    pushInt(c);
    pushInt(b);
    pushInt(a);
    invokeModuleMethod(_sumFunc);
    return popInt();
}

alias IntToIntFunc = int delegate(int id);

int execFunc01(IntToIntFunc func) {
    void wrapper() {
        const x = popInt();
        const res = func(x);
        pushInt(res);
    }
    pushClientFuncVal(&wrapper, delegateKey(func));
    invokeModuleMethod(_execFunc01);
    return popInt();
}

void stashFunc(IntToIntFunc func) {
    void wrapper() {
        const x = popInt();
        const res = func(x);
        pushInt(res);
    }
    pushClientFuncVal(&wrapper, delegateKey(func));
    invokeModuleMethod(_stashFunc);
}

void releaseFunc() {
    invokeModuleMethod(_releaseFunc);
}

IntToIntFunc returnStashedFunc() {
    invokeModuleMethod(_returnStashedFunc);
    const id = popServerFunc();

    // wrap execution in something that's remote-releasing on GC finalization
    auto serverFunc = new ServerFunc(id);

    int wrapper(int x) {
        pushInt(x);
        serverFunc.exec();
        return popInt();
    }
    return &wrapper;
}

interface IThing01 {
    void dispose(); // disposable

    int someMethod(int x);
    void stashThing(IThing01 subThing);
    IThing01 getStashedThing();
    void testThrow(bool doThrow); // throws InterfaceMethodException
    void onTimerTick();
}

// keep this out of the interface to keep it clean
private IThing01 IThing01__pop() {
    bool isClientId;
    const id = popInstance(&isClientId);
    if (!isClientId) {
        return new ServerIThing01(id);
    } else {
        return cast(IThing01) ClientObject.getById(id);
    }
}

abstract class ClientIThing01 : ClientObject, IThing01 {
public:
    void dispose() {
        // override if necessary
        // (note: from C# version, might be useless around here)
    }
    abstract int someMethod(int x);
    abstract void stashThing(IThing01 subThing);
    abstract IThing01 getStashedThing();
    abstract void testThrow(bool doThrow);
    abstract void onTimerTick();
}

private class ServerIThing01 : ServerObject, IThing01 {
public:
    this(int id) {
        super(id);
    }
    int someMethod(int x) {
        pushInt(x);
        invoke(_ithing01SomeMethod);
        return popInt();
    }
    void stashThing(IThing01 subThing) {
        (cast(Pushable)subThing).push(false);
        invoke(_ithing01StashThing);
    }
    IThing01 getStashedThing() {
        invoke(_ithing01GetStashedThing);
        auto ret = IThing01__pop();
        clearServerSafetyArea();  // because remote returned an interface to us - was being kept safe on the remote side until we got it (premature derefs are bad mmkay)
        return ret;
    }
    void testThrow(bool doThrow) {
        pushBool(doThrow);
        invokeWithExceptions(_ithing01TestThrow); // will throw for us if necessary
    }
    void onTimerTick() {
        invoke(_ithing01OnTimerTick);
    }
    void dispose() {
        serverDispose();
    }
}

IThing01 makeThing() {
    invokeModuleMethod(_makeThing);
    auto ret = IThing01__pop();
    clearServerSafetyArea();
    return ret;
}

int operateOnThing(IThing01 thing, int arg) {
    pushInt(arg);
    (cast(Pushable)thing).push(false);
    invokeModuleMethod(_operateOnThing);
    return popInt();
}

void saveThing(IThing01 thing) {
    (cast(Pushable)thing).push(false);
    invokeModuleMethod(_saveThing);
}

IThing01 getSavedThing() {
    invokeModuleMethod(_getSavedThing);
    auto ret = IThing01__pop();
    writeln("dlang IThing01 safely popped, before clearing safety area");
    clearServerSafetyArea();
    return ret;
}

void testNegative(int value) {
    pushInt(value);
    invokeWithExceptions(_testNegative); // indirect version, will throw for us
}

void testClientThrow(IThing01 thing) {
    (cast(Pushable)thing).push(false);
    invokeModuleMethod(_testClientThrow);
}

void startTimer(IThing01 thing, int frequency) {
    pushInt(frequency);
    (cast(Pushable)thing).push(false);
    invokeModuleMethod(_startTimer);
    // no return value
}

void stopTimer() {
    invokeModuleMethod(_stopTimer);
}
