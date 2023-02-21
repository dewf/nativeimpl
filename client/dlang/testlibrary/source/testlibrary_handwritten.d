module testlibrary_handwritten;

import std.stdio;
import std.conv: to;
import std.sumtype;
import nativeimplclient;
import simpleset: Set;

private: // =========================================================================================

ModuleRef _module;
ModuleMethodRef _sumFunc;
ModuleMethodRef _execFunc01;
ModuleMethodRef _getServerFunc;

ModuleMethodRef _stashFunc;
ModuleMethodRef _releaseFunc;
ModuleMethodRef _returnStashedFunc;

InterfaceRef _ithing01;
InterfaceMethodRef _ithing01SomeMethod;
InterfaceMethodRef _ithing01StashThing;
InterfaceMethodRef _ithing01GetStashedThing;
InterfaceMethodRef _ithing01TestThrow;
InterfaceMethodRef _ithing01OnTimerTick;

ModuleMethodRef _makeThing;
ModuleMethodRef _operateOnThing;
ModuleMethodRef _saveThing;
ModuleMethodRef _getSavedThing;

ModuleMethodRef _clientStashTest;

ExceptionRef _negativeException;
ModuleMethodRef _testNegative;

ExceptionRef _interfaceMethodException;
ModuleMethodRef _testClientThrow;
ModuleMethodRef _startTimer;
ModuleMethodRef _stopTimer;

ModuleMethodRef _takeSumType;
ModuleMethodRef _makeSumType;

ModuleMethodRef _takeSet;
ModuleMethodRef _makeSet;

ModuleMethodRef _myOpaqueSomeMethod;
ModuleMethodRef _makeOpaque;

public: // ==========================================================================================

void init() {
    writeln("dlang library init");
    assert(nativeimplclient.init() == 0, "nativeimplclient.init() failed");

    _module = getModule("TestLibrary");
    _sumFunc = getModuleMethod(_module, "sum");
    _execFunc01 = getModuleMethod(_module, "execFunc01");
    _getServerFunc = getModuleMethod(_module, "getServerFunc");

    _stashFunc = getModuleMethod(_module, "stashFunc");
    _releaseFunc = getModuleMethod(_module, "releaseFunc");
    _returnStashedFunc = getModuleMethod(_module, "returnStashedFunc");

    _ithing01 = getInterface(_module, "IThing01");
    _ithing01SomeMethod = getInterfaceMethod(_ithing01, "someMethod");
    _ithing01StashThing = getInterfaceMethod(_ithing01, "stashThing");
    _ithing01GetStashedThing = getInterfaceMethod(_ithing01, "getStashedThing");
    _ithing01TestThrow = getInterfaceMethod(_ithing01, "testThrow");
    _ithing01OnTimerTick = getInterfaceMethod(_ithing01, "onTimerTick");

    _makeThing = getModuleMethod(_module, "makeThing");
    _operateOnThing = getModuleMethod(_module, "operateOnThing");

    _saveThing = getModuleMethod(_module, "saveThing");
    _getSavedThing = getModuleMethod(_module, "getSavedThing");

    _clientStashTest = getModuleMethod(_module, "clientStashTest");

    setClientMethodWrapper(_ithing01SomeMethod, (ClientObject obj) {
        auto thing = cast(ClientIThing01) obj;
        const x = nativeimplclient.popInt32();
        const res = thing.someMethod(x);
        nativeimplclient.pushInt32(res);
    });
    setClientMethodWrapper(_ithing01StashThing, (ClientObject obj) {
        auto thing = cast(ClientIThing01) obj;
        auto subThing = __IThing01__pop();
        thing.stashThing(subThing);
        // no return value
    });
    setClientMethodWrapper(_ithing01GetStashedThing, (ClientObject obj) {
        auto thing = cast(ClientIThing01) obj;
        // no params
        auto ret = cast(IPushable) thing.getStashedThing();
        ret.push(true); // safe push for return, remote side (potentially) responsible for clearing our safety area - if the object is a client object
    });
    setClientMethodWrapper(_ithing01TestThrow, (ClientObject obj){
        auto thing = cast(ClientIThing01) obj;
        const doThrow = nativeimplclient.popBool();
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
    setExceptionBuilder(_negativeException, () => NegativeException.pop());

    _testNegative = getModuleMethod(_module, "testNegative");

    _interfaceMethodException = getException(_module, "InterfaceMethodException");
    setExceptionBuilder(_interfaceMethodException, () => InterfaceMethodException.pop());

    _testClientThrow = getModuleMethod(_module, "testClientThrow");
    _startTimer = getModuleMethod(_module, "startTimer");
    _stopTimer = getModuleMethod(_module, "stopTimer");

    _takeSumType = getModuleMethod(_module, "takeSumType");
    _makeSumType = getModuleMethod(_module, "makeSumType");

    _takeSet = getModuleMethod(_module, "takeSet");
    _makeSet = getModuleMethod(_module, "makeSet");

    _myOpaqueSomeMethod = getModuleMethod(_module, "myOpaqueSomeMethod");
    _makeOpaque = getModuleMethod(_module, "makeOpaque");
}

void shutdown() {
    writeln("dlang library shutdown");
    nativeimplclient.shutdown();
}

class NegativeException : Exception {
    private int _value;
    public int value() {
        return _value;
    }
    public this(int value) {
        super("Negative Value Exception");
        _value = value;
    }
    public void setAndPush() {
        setException(_negativeException);
        nativeimplclient.pushInt32(_value);
    }
    public static NegativeException pop() {
        return new NegativeException(nativeimplclient.popInt32());
    }
}

class InterfaceMethodException : Exception {
    private int _value;
    public int value() {
        return _value;
    }
    public this(int value) {
        super("Interface Method Exception");
        _value = value;
    }
    public void setAndPush() {
        setException(_interfaceMethodException);
        nativeimplclient.pushInt32(_value);
    }
    public static InterfaceMethodException pop() {
        return new InterfaceMethodException(nativeimplclient.popInt32());
    }
}

int sum(int a, int b, int c) {
    nativeimplclient.pushInt32(c);
    nativeimplclient.pushInt32(b);
    nativeimplclient.pushInt32(a);
    nativeimplclient.invokeModuleMethod(_sumFunc);
    return nativeimplclient.popInt32();
}

alias SimpleIntFunc = int delegate(int id);

int execFunc01(SimpleIntFunc func) {
    void wrapper() {
        auto x = nativeimplclient.popInt32();
        auto res = func(x);
        nativeimplclient.pushInt32(res);
    }
    pushClientFuncVal(&wrapper, nativeimplclient.delegateKey(func));
    invokeModuleMethod(_execFunc01);
    return nativeimplclient.popInt32();
}

SimpleIntFunc getServerFunc() {
    invokeModuleMethod(_getServerFunc);
    auto f = nativeimplclient.popServerFuncVal();
    int wrapper(int x) {
        nativeimplclient.pushInt32(x);
        f.exec();
        return nativeimplclient.popInt32();
    }
    return &wrapper;
}

void stashFunc(SimpleIntFunc func) {
    void wrapper() {
        auto x = nativeimplclient.popInt32();
        auto res = func(x);
        nativeimplclient.pushInt32(res);
    }
    nativeimplclient.pushClientFuncVal(&wrapper, delegateKey(func));
    nativeimplclient.invokeModuleMethod(_stashFunc);
}

void releaseFunc() {
    nativeimplclient.invokeModuleMethod(_releaseFunc);
}

SimpleIntFunc returnStashedFunc() {
    nativeimplclient.invokeModuleMethod(_returnStashedFunc);
    auto f = nativeimplclient.popServerFuncVal();
    int wrapper(int x) {
        nativeimplclient.pushInt32(x);
        f.exec();
        return nativeimplclient.popInt32();
    }
    return &wrapper;
}

// re-export these
alias IDisposable = nativeimplclient.IDisposable;
alias Disposable = nativeimplclient.Disposable;
alias using = nativeimplclient.using;

interface IThing01 : IDisposable {
    int someMethod(int x);
    void stashThing(IThing01 subThing);
    IThing01 getStashedThing();
    void testThrow(bool doThrow); // throws InterfaceMethodException
    void onTimerTick();
}

// keep this out of the interface to keep it clean
private IThing01 __IThing01__pop() {
    bool isClientId;
    const id = nativeimplclient.popInstance(isClientId);
    if (id != 0) {
        if (isClientId) {
            return cast(IThing01) ClientObject.getByID(id);
        } else {
            return new ServerIThing01(id);
        }
    } else {
        return null;
    }
}

abstract class ClientIThing01 : ClientObject, IThing01 {
public:
    void dispose() {
        // making this an optional override for client
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
        nativeimplclient.pushInt32(x);
        invokeMethod(_ithing01SomeMethod);
        return nativeimplclient.popInt32();
    }
    void stashThing(IThing01 subThing) {
        (cast(IPushable)subThing).push(false);
        invokeMethod(_ithing01StashThing);
    }
    IThing01 getStashedThing() {
        invokeMethod(_ithing01GetStashedThing);
        auto ret = __IThing01__pop();
        clearServerSafetyArea();
        return ret;
    }
    void testThrow(bool doThrow) {
        nativeimplclient.pushBool(doThrow);
        invokeWithExceptions(_ithing01TestThrow); // will throw for us if necessary
    }
    void onTimerTick() {
        invokeMethod(_ithing01OnTimerTick);
    }
}

IThing01 makeThing() {
    nativeimplclient.invokeModuleMethod(_makeThing);
    auto ret = __IThing01__pop();
    nativeimplclient.clearServerSafetyArea();
    return ret;
}

int operateOnThing(IThing01 thing, int arg) {
    nativeimplclient.pushInt32(arg);
    if (thing) {
        (cast(IPushable)thing).push(false);
    } else {
        nativeimplclient.pushNull();
    }
    nativeimplclient.invokeModuleMethod(_operateOnThing);
    return nativeimplclient.popInt32();
}

void saveThing(IThing01 thing) {
    if (thing) {
        (cast(IPushable)thing).push(false);
    } else {
        nativeimplclient.pushNull();
    }
    invokeModuleMethod(_saveThing);
}

IThing01 getSavedThing() {
    invokeModuleMethod(_getSavedThing);
    auto ret = __IThing01__pop();
    writeln("dlang clearing remote safety area");
    clearServerSafetyArea();
    return ret;
}

void clientStashTest(IThing01 thing) {
    if (thing) {
        (cast(IPushable)thing).push(false);
    } else {
        nativeimplclient.pushNull();
    }
    invokeModuleMethod(_clientStashTest);
}

void testNegative(int value) {
    nativeimplclient.pushInt32(value);
    nativeimplclient.invokeModuleMethodWithExceptions(_testNegative); // indirect version, will throw for us
}

void testClientThrow(IThing01 thing) {
    if (thing) {
        (cast(IPushable)thing).push(false);
    } else {
        nativeimplclient.pushNull();
    }
    invokeModuleMethod(_testClientThrow);
}

void startTimer(IThing01 thing, int frequency) {
    pushInt32(frequency);
    if (thing) {
        (cast(IPushable)thing).push(false);
    } else {
        nativeimplclient.pushNull();
    }
    invokeModuleMethod(_startTimer);
    // no return value
}

void stopTimer() {
    invokeModuleMethod(_stopTimer);
}

struct FancySumType {
    private enum Tag {
        Value1,
        Value2,
        Value3
    }
    struct Value1 { 
        int a;
        string b;
        static FancySumType make(int a, string b) {
            return FancySumType(cast(FancySumType.T) Value1(a, b));
        }
    }
    struct Value2 {
        static FancySumType make() {
            return FancySumType(cast(FancySumType.T) Value2());
        }
    }
    struct Value3 {
        string x;
        static FancySumType make(string x) {
            return FancySumType(cast(FancySumType.T) Value3(x));
        }
    }
    alias T = SumType!(Value1, Value2, Value3);
    T value;
    alias value this;
}

void __FancySumType__Push(FancySumType value) {
    value.match!(
        (FancySumType.Value1 v1) {
            nativeimplclient.pushString(v1.b);
            nativeimplclient.pushInt32(v1.a);
            nativeimplclient.pushInt32(FancySumType.Tag.Value1);
        },
        (FancySumType.Value2 v2) {
            // no params
            nativeimplclient.pushInt32(FancySumType.Tag.Value2);
        },
        (FancySumType.Value3 v3) {
            nativeimplclient.pushString(v3.x);
            nativeimplclient.pushInt32(FancySumType.Tag.Value3);
        }
    );
}

FancySumType __FancySumType__Pop() {
    auto tag = cast(FancySumType.Tag) nativeimplclient.popInt32();
    switch (tag) {
        case FancySumType.Tag.Value1: {
            auto a = nativeimplclient.popInt32();
            auto b = nativeimplclient.popString();
            return FancySumType.Value1.make(a, b);
        }
        case FancySumType.Tag.Value2: {
            return FancySumType.Value2.make();
        }
        case FancySumType.Tag.Value3: {
            auto x = nativeimplclient.popString();
            return FancySumType.Value3.make(x);
        }
        default:
            assert(0);
    }
}

void takeSumType(FancySumType value) {
    __FancySumType__Push(value);
    nativeimplclient.invokeModuleMethod(_takeSumType);
}

FancySumType makeSumType() {
    nativeimplclient.invokeModuleMethod(_makeSumType);
    return __FancySumType__Pop();
}

void __StringSet__Push(Set!string stringSet) {
    auto arr = stringSet.array();
    nativeimplclient.pushStringArray(arr);
}

Set!string __StringSet__Pop() {
    auto arr = nativeimplclient.popStringArray();
    return Set!string(arr);
}

void takeSet(Set!string stringSet) {
    __StringSet__Push(stringSet);
    nativeimplclient.invokeModuleMethod(_takeSet);
}

Set!string makeSet() {
    nativeimplclient.invokeModuleMethod(_makeSet);
    return __StringSet__Pop();
}

struct MyOpaque {
private:
    void* _opaque;
    this(void* ptr) {
        _opaque = ptr;
    }
    void push() {
        nativeimplclient.pushPtr(_opaque);
    }
    static MyOpaque pop() {
        auto ptr = nativeimplclient.popPtr();
        return MyOpaque(ptr);
    }
}

int myOpaqueSomeMethod(MyOpaque _this, int a, string b) {
    nativeimplclient.pushString(b);
    nativeimplclient.pushInt32(a);
    _this.push();
    nativeimplclient.invokeModuleMethod(_myOpaqueSomeMethod);
    return nativeimplclient.popInt32();
}

MyOpaque makeOpaque(int param1) {
    nativeimplclient.pushInt32(param1);
    nativeimplclient.invokeModuleMethod(_makeOpaque);
    return MyOpaque.pop();
}
