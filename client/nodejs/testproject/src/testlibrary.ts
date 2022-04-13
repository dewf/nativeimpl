import * as nativeImplClient from './nativeimplclient';
import { ModuleHandle, ModuleMethodHandle, ServerFunc, InterfaceHandle, InterfaceMethodHandle, ExceptionHandle } from './nativeimplclient';
import { i32 } from 'typed-numbers';

var _module: ModuleHandle;
var _sumFunc: ModuleMethodHandle;
var _execFunc01: ModuleMethodHandle;
var _stashFunc: ModuleMethodHandle;
var _releaseFunc: ModuleMethodHandle;
var _returnStashedFunc: ModuleMethodHandle;

var _ithing01: InterfaceHandle;
var _ithing01SomeMethod: InterfaceMethodHandle;
var _ithing01StashThing: InterfaceMethodHandle;
var _ithing01GetStashedThing: InterfaceMethodHandle;
var _ithing01TestThrow: InterfaceMethodHandle;
var _ithing01OnTimerTick: InterfaceMethodHandle;

var _makeThing: ModuleMethodHandle;
var _operateOnThing: ModuleMethodHandle;
var _saveThing: ModuleMethodHandle;
var _getSavedThing: ModuleMethodHandle;
var _clientStashTest: ModuleMethodHandle;

var _negativeException: ExceptionHandle;
var _testNegative: ModuleMethodHandle;

var _interfaceMethodException: ExceptionHandle; // from ithing->testthrow

var _testClientThrow: ModuleMethodHandle;
var _startTimer: ModuleMethodHandle;
var _stopTimer: ModuleMethodHandle;

type IntFunc = (x: i32) => i32;

// some 'constants' (should we make some attempt to make these read-only?)
var global01: i32;
var anotherGlobal: i32;
var lastIntGlobal: i32;

function init() {
    if (nativeImplClient.init() !== 0) {
        console.log("nativeImplClient.init failed");
    }
    _module = nativeImplClient.getModule("TestLibrary");

    nativeImplClient.pushModuleConstants(_module);
    global01 = nativeImplClient.popInt();
    anotherGlobal = nativeImplClient.popInt();
    lastIntGlobal = nativeImplClient.popInt();

    _sumFunc = nativeImplClient.getModuleMethod(_module, "sum");
    _execFunc01 = nativeImplClient.getModuleMethod(_module, "execFunc01");
    _stashFunc = nativeImplClient.getModuleMethod(_module, "stashFunc");
    _releaseFunc = nativeImplClient.getModuleMethod(_module, "releaseFunc");
    _returnStashedFunc = nativeImplClient.getModuleMethod(_module, "returnStashedFunc");

    _ithing01 = nativeImplClient.getInterface(_module, "IThing01");
    _ithing01SomeMethod = nativeImplClient.getInterfaceMethod(_ithing01, "someMethod");
    _ithing01StashThing = nativeImplClient.getInterfaceMethod(_ithing01, "stashThing");
    _ithing01GetStashedThing = nativeImplClient.getInterfaceMethod(_ithing01, "getStashedThing");
    _ithing01TestThrow = nativeImplClient.getInterfaceMethod(_ithing01, "testThrow");
    _ithing01OnTimerTick = nativeImplClient.getInterfaceMethod(_ithing01, "onTimerTick");
    
    _makeThing = nativeImplClient.getModuleMethod(_module, "makeThing");
    _operateOnThing = nativeImplClient.getModuleMethod(_module, "operateOnThing");
    
    _saveThing = nativeImplClient.getModuleMethod(_module, "saveThing");
    _getSavedThing = nativeImplClient.getModuleMethod(_module, "getSavedThing");

    _clientStashTest = nativeImplClient.getModuleMethod(_module, "clientStashTest");

    // method wrappers here
    nativeImplClient.setClientMethodWrapper(_ithing01SomeMethod, (thing: ClientIThing01) => { // we are casting there with the arguments, it's supposed to be a ClientObject but we're more specific
        const x = nativeImplClient.popInt();
        const res = thing.someMethod(x);
        nativeImplClient.pushInt(res);
    });

    nativeImplClient.setClientMethodWrapper(_ithing01StashThing, (thing: ClientIThing01) => {
        const thingParam = IThing01__pop();
        thing.stashThing(thingParam);
        // no return value
    });

    nativeImplClient.setClientMethodWrapper(_ithing01GetStashedThing, (thing: ClientIThing01) => {
        // no params
        const ret = thing.getStashedThing();
        ret.__push(true); // safe push for return, remote side responsible for clearing
    });

    nativeImplClient.setClientMethodWrapper(_ithing01TestThrow, (thing: ClientIThing01) => {
        const doThrow = nativeImplClient.popBool();
        try {
            thing.testThrow(doThrow);
        } catch (e) { // catch and throw via nativeimpl!
            if (e instanceof InterfaceMethodException) {
                nativeImplClient.setException(_interfaceMethodException);
                e.push();
            }
        }
    });

    nativeImplClient.setClientMethodWrapper(_ithing01OnTimerTick, (thing: ClientIThing01) => {
        // no params
        thing.onTimerTick();
        // no return
    });

    // exception testing
    _negativeException = nativeImplClient.getException(_module, "NegativeException");
    nativeImplClient.setExceptionBuilder(_negativeException, NegativeException.pop);

    _testNegative = nativeImplClient.getModuleMethod(_module, "testNegative");

    _interfaceMethodException = nativeImplClient.getException(_module, "InterfaceMethodException");
    nativeImplClient.setExceptionBuilder(_interfaceMethodException, InterfaceMethodException.pop);

    _testClientThrow = nativeImplClient.getModuleMethod(_module, "testClientThrow");
    _startTimer = nativeImplClient.getModuleMethod(_module, "startTimer");
    _stopTimer = nativeImplClient.getModuleMethod(_module, "stopTimer");
}

function shutdown() {
    nativeImplClient.shutdown();
}

function sum(a: i32, b: i32, c: i32) {
    nativeImplClient.pushInt(a);
    nativeImplClient.pushInt(b);
    nativeImplClient.pushInt(c);
    nativeImplClient.invokeModuleMethod(_sumFunc);
    return nativeImplClient.popInt();
}

function execFunc01(userFunc: IntFunc): number {
    function wrapper(): void {
        const x = nativeImplClient.popInt();
        const res = userFunc(x);
        nativeImplClient.pushInt(res);
    }
    nativeImplClient.pushClientFuncVal(wrapper, userFunc);
    nativeImplClient.invokeModuleMethod(_execFunc01);
    return nativeImplClient.popInt();
}

function stashFunc(userFunc: IntFunc): void {
    function wrapper() {
        const x = nativeImplClient.popInt();
        const res = userFunc(x);
        nativeImplClient.pushInt(res);
    }
    nativeImplClient.pushClientFuncVal(wrapper, userFunc);
    nativeImplClient.invokeModuleMethod(_stashFunc);
}

function releaseFunc(): void {
    nativeImplClient.invokeModuleMethod(_releaseFunc);
}

function returnStashedFunc(): IntFunc {
    nativeImplClient.invokeModuleMethod(_returnStashedFunc);
    const id = nativeImplClient.popServerFunc();

    // wrap remote function, so it can self-free on GC finalization or whatever that's called around here
    const remoteFunc = new ServerFunc(id);
    
    function wrapper(x: i32): i32 {
        nativeImplClient.pushInt(x);
        remoteFunc.exec();
        return nativeImplClient.popInt();
    }

    return wrapper;
}

interface IDisposable {
    dispose(): void;
}

interface IThing01 extends IDisposable {
    // actual public methods =====
    someMethod(x: i32): i32;
    stashThing(subThing: IThing01): void;
    getStashedThing(): IThing01;
    testThrow(doThrow: boolean): void;
    onTimerTick(): void;

    // internal support stuff (why aren't these just separate/external methods?)
    // I guess because implementation-specific
    __push(isReturn: boolean): void;
    // static pop moved outside, below
}

function IThing01__pop(): IThing01 {
    const res = nativeImplClient.popInstance();
    if (!res.isClientId) {
        // wrap with server stuff
        return new ServerIThing01(res.id);
    } else {
        // its our own ID coming back to us, fetch by ID from table
        console.log("IThing01__pop(): client objects not yet supported!!!");
        return null;
    }
}

abstract class ClientIThing01 extends nativeImplClient.ClientObject implements IThing01 {
    public dispose(): void {
        // override if necessary
    }
    public abstract someMethod(x: i32): i32;
    public abstract stashThing(subThing: IThing01): void;
    public abstract getStashedThing(): IThing01;
    public abstract testThrow(doThrow: boolean): void;
    public abstract onTimerTick(): void;

    public __push(isReturn: boolean): void {
        this.clientPush();
    }
}

class ServerIThing01 extends nativeImplClient.ServerObject implements IThing01 {
    constructor(id: i32) {
        super(id);
    }
    public someMethod(x: i32): i32 {
        nativeImplClient.pushInt(x);
        nativeImplClient.invokeInterfaceMethod(_ithing01SomeMethod, this.id);
        return nativeImplClient.popInt();
    }
    public stashThing(subThing: IThing01): void {
        subThing.__push(false);
        nativeImplClient.invokeInterfaceMethod(_ithing01StashThing, this.id);
    }
    public getStashedThing(): IThing01 {
        nativeImplClient.invokeInterfaceMethod(_ithing01GetStashedThing, this.id);
        const ret = IThing01__pop();
        nativeImplClient.clearServerSafetyArea(); // because remote returned an interface to us - was being kept safe on the remote side until we got it
        return ret;
    }
    public testThrow(doThow: boolean): void {
        nativeImplClient.pushBool(doThow);
        nativeImplClient.invokeInterfaceMethodWithExceptions(_ithing01TestThrow, this.id);
    }
    public onTimerTick(): void {
        nativeImplClient.invokeInterfaceMethod(_ithing01OnTimerTick, this.id);
    }
    public dispose(): void {
        console.log("ServerIThing01.dispose()");
        this.serverDispose();
    }
    // IThing01 plumbing =====
    public __push(isReturn: boolean) {
        nativeImplClient.pushServerInstanceId(this.id);
        if (isReturn) {
            // save this object to the stack safety area, until the other side has pulled it off
            // otherwise, if this is the last proxy on this side,
            // could remotely detonate the server's real copy before it's done taking 'ownership'
            // (dependent on probably-unlikely GC collection, but better safe than sorry)
            this.makeSafe();
        }
    }
}

function makeThing(): IThing01 {
    nativeImplClient.invokeModuleMethod(_makeThing);
    const ret = IThing01__pop();
    nativeImplClient.clearServerSafetyArea(); // because remote returned an interface to us - was being kept safe on the remote side until we got it
    return ret;
}

function operateOnThing(thing: IThing01, arg: i32): i32 {
    nativeImplClient.pushInt(arg);
    thing.__push(false);
    nativeImplClient.invokeModuleMethod(_operateOnThing);
    return nativeImplClient.popInt();
}

function saveThing(thing: IThing01): void {
    thing.__push(false);
    nativeImplClient.invokeModuleMethod(_saveThing);
}

function getSavedThing(): IThing01 {
    nativeImplClient.invokeModuleMethod(_getSavedThing);
    const ret = IThing01__pop();
    console.log("C# IThing01 safely popped, before clearing safety area");
    nativeImplClient.clearServerSafetyArea();
    return ret;
}

function clientStashTest(thing: IThing01): void {
    thing.__push(false);
    nativeImplClient.invokeModuleMethod(_clientStashTest);
    // no return value
}

class NegativeException extends Error {
    private _value: i32;
    public get value() { return this._value; }
    constructor(value: i32) {
        super("NegativeException");
        this._value = value;
    }
    public static pop(): NegativeException {
        return new NegativeException(nativeImplClient.popInt());
    }
}

function testNegative(value: i32): void {
    nativeImplClient.pushInt(value);
    nativeImplClient.invokeModuleMethodWithExceptions(_testNegative);
}

class InterfaceMethodException extends Error {
    private _value: i32;
    public get value() { return this._value; }
    constructor(value: i32) {
        super("InterfaceMethodException");
        this._value = value;
    }
    public push(): void {
        nativeImplClient.pushInt(this._value);
    }
    public static pop(): InterfaceMethodException {
        return new InterfaceMethodException(nativeImplClient.popInt());
    }
}

function testClientThrow(thing: IThing01): void {
    thing.__push(false);
    nativeImplClient.invokeModuleMethod(_testClientThrow);
}

function startTimer(thing: IThing01, frequency: i32): void {
    nativeImplClient.pushInt(frequency);
    thing.__push(false);
    nativeImplClient.invokeModuleMethod(_startTimer);
}

function stopTimer(): void {
    nativeImplClient.invokeModuleMethod(_stopTimer);
}

export {
    init, shutdown, global01, anotherGlobal, lastIntGlobal, sum, execFunc01, stashFunc, releaseFunc, returnStashedFunc,
    IThing01, makeThing, operateOnThing, saveThing, getSavedThing,
    ClientIThing01,
    clientStashTest,
    testNegative, testClientThrow, startTimer, stopTimer,
    NegativeException, InterfaceMethodException
 };
