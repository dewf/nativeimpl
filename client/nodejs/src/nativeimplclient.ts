var nativeMethods = require('./testlibrary_native.node');
import { i32, u32, u64 } from 'typed-numbers';

type NativePtr = u32 | u64;

class NativeHandle {
    ptr: NativePtr;
    constructor(ptr) {
        this.ptr = ptr;
    }
}

class ModuleHandle extends NativeHandle {
    constructor(ptr) {
        super(ptr);
    }
}

class ModuleMethodHandle extends NativeHandle {
    constructor(ptr) {
        super(ptr);
    }
}

class InterfaceHandle extends NativeHandle {
    constructor(ptr) {
        super(ptr);
    }
}

class InterfaceMethodHandle extends NativeHandle {
    constructor(ptr) {
        super(ptr);
    }
}

class ExceptionHandle extends NativeHandle {
    constructor(ptr) {
        super(ptr);
    }
}

type FuncValWrapper = () => void;

class FuncValInfo {
    wrapper: FuncValWrapper;
    key: Object;
    refCount: number;
    constructor(wrapper: FuncValWrapper, key: Object) {
        this.wrapper = wrapper;
        this.key = key;
        this.refCount = 1;
    }
}

const funcValMap = new Map<i32, FuncValInfo>();   // from ID to struct
const reverseFuncValMap = new Map<Object, i32>(); // from "function hash" (the function itself) to ID
var nextFuncValId = i32(1);
// a flag to prevent any more release()s of remote resources (on GC or whatever)
var remoteIsShutdown = false;

function clientFuncExec(id: i32): void {
    console.log("node: clientFuncExec(): ", id);
    funcValMap[id].wrapper();
}

function clientFuncRelease(id: i32): void {
    console.log("node: clientFuncRelease(): ", id);
    const funcVal = funcValMap[id];
    funcVal.refCount -= 1;
    if (funcVal.refCount === 0) {
        reverseFuncValMap.delete(funcVal.key);
        funcValMap.delete(id);
        console.log("nodejs removed client func: " + id);
    } else {
        // re-store with updated refcount
        funcValMap[id] = funcVal;
        console.log("nodejs updated refcount func id:" + id + ", count now: " + funcVal.refCount);
    }
}
function clientMethodExec(method: NativePtr, objId: i32): void {
    console.log("node: clientMethodExec(): ", method, objId);
    const obj = ClientObject.getById(objId);
    methodWrappers.get(method)(obj);
}
function clientObjectRelease(id: i32): void {
    // should we check for _remoteIsShutdown here?
    console.log("node: clientObjectRelease(): ", id);
    ClientObject.getById(id).release();
}
function clientClearSafetyArea(): void {
    console.log("node: clientClearSafetyArea()");
    // IIRC these aren't disposed of because there's no reason to believe this is the only place they exist on this side - this is just a protective mechanism
    // in case the return value of a function is the only place it does, but the other side hasn't retained it yet - so some bad timing could accidentally free it
    safeArea = new Array<ServerObject>(); // no way to clear existing, I guess
}

function addFuncVal(wrapper: FuncValWrapper, uniqueKey: Object) {
    if (!reverseFuncValMap.has(uniqueKey)) {
        // create new
        const id = i32(nextFuncValId++);
        reverseFuncValMap.set(uniqueKey, id);               // typescript won't let us use the key object with indexing syntax
        funcValMap[id] = new FuncValInfo(wrapper, uniqueKey);
        return id;
    } else {
        // just increment refcount
        const id = reverseFuncValMap.get(uniqueKey);
        const funcVal = funcValMap[id];
        // increment refcount + rewrite
        funcVal.refCount += 1;
        funcValMap[id] = funcVal;
        return id;
    }
}

function init(): i32 {
    return nativeMethods.init(clientFuncExec, clientFuncRelease, clientMethodExec, clientObjectRelease, clientClearSafetyArea);
}

function shutdown(): void {
    remoteIsShutdown = true;
    nativeMethods.shutdown();
}

function getModule(name: string): ModuleHandle {
    return new ModuleHandle(nativeMethods.getModule(name));
}

function getModuleMethod(module: ModuleHandle, name: string): ModuleMethodHandle {
    return new ModuleMethodHandle(nativeMethods.getModuleMethod(module.ptr, name));
}

function pushModuleConstants(module: ModuleHandle): void {
    nativeMethods.pushModuleConstants(module.ptr);
}

function invokeModuleMethod(method: ModuleMethodHandle): void {
    nativeMethods.invokeModuleMethod(method.ptr);
}

function invokeModuleMethodWithExceptions(method: ModuleMethodHandle): void {
    const exceptionPtr = nativeMethods.invokeModuleMethodWithExceptions(method.ptr);
    if (exceptionPtr != 0) {
        throw exceptionBuilders.get(exceptionPtr)();
    }
}

function getInterface(module: ModuleHandle, name: string): InterfaceHandle {
    return new InterfaceHandle(nativeMethods.getInterface(module.ptr, name));
}

function getInterfaceMethod(iface: InterfaceHandle, name: string): InterfaceMethodHandle {
    return new InterfaceMethodHandle(nativeMethods.getInterfaceMethod(iface.ptr, name));
}

function invokeInterfaceMethod(method: InterfaceMethodHandle, serverId: i32): void {
    nativeMethods.invokeInterfaceMethod(method.ptr, serverId);
}

function invokeInterfaceMethodWithExceptions(method: InterfaceMethodHandle, serverId: i32): void {
    const exceptionPtr = nativeMethods.invokeInterfaceMethodWithExceptions(method.ptr, serverId);
    if (exceptionPtr != 0) { // hopefully this works for both u32/u64?
        throw exceptionBuilders.get(exceptionPtr)();
    }
}

function pushBool(value: boolean) {
    nativeMethods.pushBool(value);
}

function popBool(): boolean {
    return nativeMethods.popBool();
}

function pushInt(x: i32) {
    nativeMethods.pushInt(x);
}

function popInt(): i32 {
    return nativeMethods.popInt();
}

function pushClientFuncVal(wrapper: FuncValWrapper, key: Object) {
    const id = addFuncVal(wrapper, key);
    console.log("node: addfuncval id: " + id);
    nativeMethods.pushClientFunc(id);
}

function popServerFunc(): i32 {
    return nativeMethods.popServerFunc();
}

const serverFuncFinalizer = new FinalizationRegistry((id: i32) => {
    if (!remoteIsShutdown) {
        nativeMethods.releaseServerFunc(id);
    }
});

class ServerFunc {
    private _id: i32;
    constructor(id: i32) {
        this._id = id;
        serverFuncFinalizer.register(this, id);
    }
    exec(): void {
        nativeMethods.execServerFunc(this._id);
    }
}

interface PopInstanceResult {
    id: i32;
    isClientId: boolean;
}

function popInstance(): PopInstanceResult {
    return nativeMethods.popInstance();
}

function releaseServerInstanceId(id: i32) {
    if (!remoteIsShutdown) {
        nativeMethods.releaseServerInst(id);
    }
}

class ServerObjectArk { // this is what gets passed to the finalizer, since the object is no more
    id: i32;
    remoteReleased: boolean;
    constructor(id: i32) {
        this.id = id;
        this.remoteReleased = false;
    }
    dispose() {
        if (this.remoteReleased) return;
        releaseServerInstanceId(this.id);
        this.remoteReleased = true;
        console.log("ServerObjectArk: dispose() completed");
    }
}

const serverObjectFinalizer = new FinalizationRegistry((ark: ServerObjectArk) => {
    ark.dispose();
});

class ServerObject {
    private ark: ServerObjectArk;
    public get id() {
        return this.ark.id;
    }
    constructor(id: i32) {
        this.ark = new ServerObjectArk(id);
        serverObjectFinalizer.register(this, this.ark);
    }
    serverDispose(): void {
        console.log("ServerObject.serverDispose()");
        this.ark.dispose();
    }
    makeSafe(): void {
        safeArea.push(this);
    }
}

// place to stash server objects until the other side has finished popping them from stack (in case they only exist as a return value)
var safeArea = new Array<ServerObject>();

function serverClearSafetyArea() {
    nativeMethods.clearServerSafetyArea(); // we should probably rename this for consistency ...
}

function pushServerInstanceId(id: i32): void {
    nativeMethods.pushServerInst(id);
}

class ClientObject {
    private _id = i32(-1);
    private _refCount = 0;
    public get id() { return this._id; }
    public get refCount() { return this._refCount; }

    public clientPush(): void {
        if (this._id === -1) {
            // unset as yet
            this._id = nextClientObjectId; // incrementing here makes i32() sad for some reason :(
            nextClientObjectId++;
            this._refCount = 1;
            clientObjects.set(this._id, this);
            console.log("nodejs ClientObject: pushed " + this._id + ", refcount " + this._refCount);
        } else {
            // just increase refcount as we pass it over
            this._refCount++;
            console.log("nodejs ClientObject: increased refcount to " + this._refCount);
        }
        nativeMethods.pushClientInst(this._id);
    }

    public release(): void {
        console.log("clientObject.release(): current refCount " + this._refCount);
        this._refCount--;
        if (this._refCount === 0) {
            console.log("nodejs ReleaseClientObjectID " + this._id + " - refCount 0, removing from clientObjects table");
            clientObjects.delete(this._id);
            this._id = i32(-1);  // reset ID, since we're not in the object table anymore
            // all this does is remove this object from the 'checked-out' table -
            // things we were keeping alive as long as the remote (server) side had reference to them
            // but that doesn't mean we're ready to dispose it - might still be useful to us!
        }
    }

    public static getById(id: i32): ClientObject {
        if (!clientObjects.has(id)) {
            console.log("### ClientObject.getById("+id+"): key not found!!");
            return null;
        }
        return clientObjects.get(id);
    }
}

var nextClientObjectId = i32(1);
const clientObjects = new Map<i32, ClientObject>();

type MethodWrapper = (obj: ClientObject) => void;
const methodWrappers = new Map<NativePtr, MethodWrapper>();

function setClientMethodWrapper(method: InterfaceMethodHandle, wrapper: MethodWrapper) {
    methodWrappers.set(method.ptr, wrapper);
}

function dumpTables(): void {
    console.log("============Dump Tables==================");
    console.log("Native tables----------------------------");
    nativeMethods.dumpTables();

    console.log("Local tables-----------------------------");

    console.log("== nodejs func vals:");
    funcValMap.forEach((value, key, map) => {
        console.log(" - fun " + key + ": refcount " + value.refCount);
    });

    console.log("== nodejs objects:");
    // foreach (var (key, value) in ClientObjects)
    // {
    //     Console.WriteLine($" - obj {value.Id}: refcount {value.RefCount}");
    // }

    console.log("=========================================");
}

type ExceptionBuilder = () => Error;
const exceptionBuilders = new Map<NativePtr, ExceptionBuilder>();

function getException(module: ModuleHandle, name: string): ExceptionHandle {
    const ptr = nativeMethods.getException(module.ptr, name);
    return new ExceptionHandle(ptr);
}

function setExceptionBuilder(eh: ExceptionHandle, builder: ExceptionBuilder) {
    exceptionBuilders.set(eh.ptr, builder);
}

function setException(eh: ExceptionHandle) {
    nativeMethods.setException(eh.ptr);
}

export { 
    ModuleHandle, ModuleMethodHandle, InterfaceHandle, InterfaceMethodHandle, ExceptionHandle,
    init, shutdown, 
    getModule, getModuleMethod, invokeModuleMethod, invokeModuleMethodWithExceptions,
    pushModuleConstants,
    getInterface, getInterfaceMethod, invokeInterfaceMethod, invokeInterfaceMethodWithExceptions,
    pushBool, popBool,
    pushInt, popInt,
    pushClientFuncVal, popServerFunc,
    ServerFunc,
    popInstance, PopInstanceResult,
    ClientObject, setClientMethodWrapper,
    ServerObject, serverClearSafetyArea as clearServerSafetyArea, pushServerInstanceId,
    dumpTables,
    getException, setExceptionBuilder, setException
};
