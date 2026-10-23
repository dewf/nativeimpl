module %%CLIENT_LIBRARY_PREFIX%%.support.nativeimplclient;

// this is nested in the library namespace (instead of at root level),
// because a project could potentially have multiple NativeImpl-based libraries, using different versions of NI
// this will keep the NI methods from conflicting

import %%CLIENT_LIBRARY_PREFIX%%.support.nativemethods;
import nativemethods = %%CLIENT_LIBRARY_PREFIX%%.support.nativemethods; // also alias for convenience

import std.stdio;
import std.string : toStringz, format;
import std.algorithm: map;
import std.range: array;

import core.sync.mutex: Mutex;

import stonesoup.disposable;

// PRIVATE ====================================================================================================
private:

// client callbacks:
extern (C) {
    void clientLogFunc(ni_ClientLogLevel level, const (char)* msg, int length) {
        // no allocations, because if this is called back during a GC collection it'll crash!
        auto message = cast(string) msg[0 .. length];
        writefln("[native: %s]: %s", level, message);
    }

    void clientFuncExec(int id) {
        ClientFuncVal.getById(id).exec();
    }

    void clientMethodExec(ni_InterfaceMethodRef method, int objId) {
        auto obj = ClientObject.getById(objId);
        methodWrappers[method](obj);
    }

    void clientResourceRelease(int id) {
        ClientResource.getById(id).release();
    }

    void clientClearSafetyArea() {
        assert(0, "clientClearSafetyArea() not yet implemented");
    }
}

struct ClientFuncKey {
    void *context;      // OK if zero, I think (just means it's a standard function, no closure?)
    void *funcptr;
    bool isValid() {
        return funcptr != null;
    }
    string toString() const
    {
        return format!"ClientFuncKey[context:%08X,func:%08X]"(context, funcptr);
    }
}


__gshared bool remoteIsShutdown;

// client resource stuff =======================
__gshared Mutex clientResourceMutex;
__gshared int nextResourceId = 1;
__gshared ClientResource[int] resourceMap;
__gshared ClientFuncVal[ClientFuncKey] reverseFuncValDict;
// =============================================

alias ExceptionBuildAndThrowFunc = void function();
alias MethodWrapperFunc = void delegate(ClientObject obj);

__gshared ExceptionBuildAndThrowFunc[ni_ExceptionRef] exceptionBuilders;
__gshared MethodWrapperFunc[ni_InterfaceMethodRef] methodWrappers;

interface IPushable {
    void push(bool isReturn);
}

abstract class ClientResource : IPushable {
private:
    int _id = -1;
    int refCount;
protected:
    int id() {
        assert(_id != -1, "ClientResource: attempted to use [id] before it has been set!");
        return _id;
    }
    abstract void nativePush(); // must be implemented

    void releaseExtra() {
        // override if you need to do any extra cleanup when this resource has been fully released by the server side
        // (currently used by ClientFuncVal to remove entry from reverse mapping)
    }
public:
    override void push(bool isReturn) {
        if (_id == -1) {
            synchronized (clientResourceMutex) {
                // unset as yet
                _id = nextResourceId++;
                refCount = 1;
                resourceMap[_id] = this;
            }
            writefln("Dlang ClientResource: assigned new ID: %d", _id);
        } else {
            // just increase refcount since it's a known thing
            refCount += 1;
            writefln("Dlang ClientResource: increased refcount of ID [%d] to %d", _id, refCount);
        }
        nativePush();
    }
    void release() {
        refCount -= 1;
        if (refCount == 0) {
            writefln("Dlang release on ClientResource [%d] - refCount 0, removing from resource table", _id);
            synchronized (clientResourceMutex) {
                resourceMap.remove(_id);
            }
            _id = -1; // reset ID, since we're not in the table anymore
            // all this does is remove this object from the 'checked-out' table -
            // things we were keeping alive as long as the remote (server) side had reference to them
            // but that doesn't mean we're ready to dispose it - might still be useful to us on this side!
            releaseExtra();
        }
    }
    static ClientResource getById(int id) {
        synchronized (clientResourceMutex) {
            return resourceMap[id];
        }
    }
    static void dumpTables() {
        synchronized (clientResourceMutex) {
            foreach (id, resource; resourceMap) {
                writefln(" - ClientResource [%d]: refcount [%d]", id, resource.refCount);
            }
        }
    }
}

alias FuncValWrapper = void delegate();

class ClientFuncVal : ClientResource {
    FuncValWrapper wrapper;
    ClientFuncKey key;
    this(FuncValWrapper wrapper, ClientFuncKey key) {
        this.wrapper = wrapper;
        this.key = key;
        // unlike the base ClientResource/ClientObject, a ClientFuncVal is only ever created just before being pushed
        // so no harm in filling the table entry immediately (vs. in a 'register' or push method)
        if (key.isValid) {
            synchronized (clientResourceMutex) {
                reverseFuncValDict[key] = this;
            }
        }
    }
    override void nativePush() {
        nativemethods.pushClientFunc(id);
    }
    override void releaseExtra() {
        writefln("dlang releaseExtra()");
        if (key.isValid) {
            // remove self from reverse dict
            // unlike ClientObjects, there's should be no potential for a ClientFuncVal being reused once it's removed, because they only exist within library wrapper code
            synchronized (clientResourceMutex) {
                reverseFuncValDict.remove(key);
            }
            writefln("ClientFuncVal: removed key [%s] from reverse dict", key);
        }
        // probably unnecessary, but who knows!
        super.releaseExtra();
    }
    void exec() {
        // woot!
        wrapper();
    }
    override string toString() const
    {
        return format!"ClientFuncVal[key:%s]"(key);
    }
    // static methods -------------------------------
    static ClientFuncVal getById(int id) {
        return cast(ClientFuncVal) ClientResource.getById(id);
    }
    static ClientFuncVal getOrCreate(FuncValWrapper wrapper, ClientFuncKey key) {
        synchronized (clientResourceMutex) {
            if (key.isValid && (key in reverseFuncValDict)) {
                // re-use existing
                writefln("ClientFuncVal.getorCreate() - client func key already exists!");
                // return existing
                // used to addRef here, but now that's taken care of by ClientResource.push()
                return reverseFuncValDict[key];
            } else {
                // create new
                writefln("pushing new ClientFuncVal");
                return new ClientFuncVal(wrapper, key);
            }
        }
    }
}

abstract class ClientObject : ClientResource {
protected:
    override void nativePush() {
        nativemethods.pushClientInterface(id);
    }
public:
    static ClientObject getById(int id) {
        return cast(ClientObject) ClientResource.getById(id);
    }
}

// marker interface for client interfaces
public interface IClientInterface {}
// map so we can use bare interfaces in the client code (no special inheritance, other than marker interface)
__gshared ClientInterfaceWrapper[IClientInterface] clientInterfaceWrapperMap;

// needs to be public, because it's used directly in module definitions
class ClientInterfaceWrapper : ClientObject {
private:
    IClientInterface _rawInterface;
protected:
    override void releaseExtra() {
        synchronized (clientResourceMutex) {
            clientInterfaceWrapperMap.remove(_rawInterface);
        }
        super.releaseExtra();
    }
public:
    this(IClientInterface raw) {
        _rawInterface = raw;
    }
    IClientInterface rawInterface() => _rawInterface;
}

// place to stash server objects until the other side has finished popping them from stack
// (in case they only exist as a return value)
__gshared ServerResource[] safeArea;

void clearSafeArea() {
    synchronized (clientResourceMutex) {
        safeArea.length = 0;
    }
}

enum DisposedFrom {
    UserCode,
    Finalizer
}

abstract class ServerResource : IPushable, IDisposable {
protected:
    int id;
    bool disposed;
    this(int id) {
        this.id = id;
    }

    abstract void nativePush(); // overridden by derived class to implement actual nativeimpl push

    // in our C# code we used a releaseExtra() thing
    // but the dispose pattern (used below) is more familiar

    void dispose(DisposedFrom from) {
        if (disposed) {
            writefln("dlang already disposed server obj %d, early return", id);
            return;
        }

        if (from != DisposedFrom.Finalizer) {
            // here we could also dispose non-native resources
            // (it's not safe to dispose any managed objects from a finalizer)

            // that's irrelevant for us since we don't maintain links to disposable objects
            // but subclasses of ServerResource will have to be aware of this, should they choose to override
        }

        // native resource disposal below:
        if (!remoteIsShutdown) {
            nativemethods.releaseServerResource(id);
            writefln("dlang released server obj %d", id);
        } else {
            writefln("(didn't release server obj %d - remote was already shutdown)", id);
        }
        disposed = true;
    }
public:
    // all ServerResources get comparison / hashing for free
    // I guess we could make this more specific (eg only ServerObjects) because it's unlikely to be used by other types
    // but if somebody wants to compare eg ServerBuffers ... why not?
    override bool opEquals(Object o) const {
        if (auto other = cast(ServerResource)o) {
            return id == other.id;
        }
        return false;
    }
    override hash_t toHash() const {
        return typeid(id).getHash(&id);
    }
    ~this() {
        // backstop in case the derived class not manually disposed
        writefln("Dlang finalizing ServerResource[%d]", id);
        dispose(DisposedFrom.Finalizer);
    }
    final void dispose() {
        dispose(DisposedFrom.UserCode);
        // AFAIK no way to suppress finalization
    }
    override void push(bool isReturn) {
        if (isReturn) {
            // save this object to the stack safety area, until the other side has pulled it off
            // otherwise, if this is the last proxy on this side,
            // could remotely detonate the server's real copy before it's done taking 'ownership'
            // (dependent on probably-unlikely GC collection, but better safe than sorry)
            synchronized (clientResourceMutex) {
                safeArea ~= this;
            }
        }
        // do yo' thing, subclass
        nativePush();
    }
}

class ServerFuncVal : ServerResource {
protected:
    override void nativePush() {
        // ServerFuncs currently will never be pushed - we don't support "short-circuiting" of function values,
        // they are always wrapped afresh from each side (so a server function returned from our side,
        // becomes a client function - use a single method interface for that kind of thing)
        assert(0, "should be unreachable");
    }
public:
    this(int id) {
        super(id);
    }
    // finalizer handled by ServerResource
    void exec() {
        // we don't bother with a dispose check, because client code never sees ServerFuncVals directly (they are always wrapped in delegates)
        // so there is no way to dispose them (unlike interface instances)
        nativemethods.execServerFunc(id);
    }
    void execWithExceptions() {
        auto eh = nativemethods.execServerFuncWithExceptions(id);
        if (eh != null) {
            exceptionBuilders[eh]();
        }
    }
}

// buffer stuff
// public alias BufferDescriptor = ni_BufferDescriptor;

public interface INativeBuffer(T) : IDisposable {
    T* getSpan(size_t* length);
}

class ServerBuffer(T) : ServerResource, INativeBuffer!T {
private:
    ni_BufferDescriptor desc;
    this(int id, ni_BufferDescriptor desc) {
        super(id);
        this.desc = desc;
    }
protected:
    override void nativePush() {
        nativemethods.pushBuffer(id, false, &desc);
    }
public:
    // ServerResource handles dispose(), we don't have anything extra
    T* getSpan(size_t* length) {
        *length = desc.totalCount;
        return cast(T*)desc.start;
    }
}

public class ClientBuffer(T) : ClientResource, INativeBuffer!T {
private:
    enum Kind {
        Slice
    }
    Kind kind;
    T[] slice;

    static ClientBuffer!T getById(int id) {
        return cast(ClientBuffer!T) ClientResource.getById(id);
    }
protected:
    override void nativePush() {
        ni_BufferDescriptor desc;
        switch (kind) {
            case Kind.Slice:
                desc.start = slice.ptr;
                desc.elementSize = T.sizeof;
                desc.totalCount = slice.length;
                desc.totalSize = desc.elementSize * desc.totalCount;
                break;
            default:
                assert(0);
        }
        nativemethods.pushBuffer(id, true, &desc);
    }

    // do we need any kind of extra cleanup?
    // override void releaseExtra() {
    //     super.releaseExtra();
    // }

    // must be implemented due to INativeBuffer
    override void dispose() {
        // free any copied stuff?
    }

public:
    this(T[] data) {
        kind = Kind.Slice;
        slice = data;
    }

    T* getSpan(size_t* length) {
        switch (kind) {
            case Kind.Slice:
                *length = slice.length;
                return slice.ptr;
            default:
                assert(0);
        }
    }
}

// ========================================================================
public:

class WrappedHandle(T) {
    private T handle;
    private this(T handle) {
        this.handle = handle;
    }
}

class ModuleHandle : WrappedHandle!ni_ModuleRef {
    private this(ni_ModuleRef handle) {
        super(handle);
    }
}

class ModuleMethodHandle : WrappedHandle!ni_ModuleMethodRef {
    private this(ni_ModuleMethodRef handle) {
        super(handle);
    }
}

class InterfaceHandle : WrappedHandle!ni_InterfaceRef {
    private this(ni_InterfaceRef handle) {
        super(handle);
    }
}

class InterfaceMethodHandle : WrappedHandle!ni_InterfaceMethodRef {
    private this(ni_InterfaceMethodRef handle) {
        super(handle);
    }
}

class ExceptionHandle : WrappedHandle!ni_ExceptionRef {
    private this(ni_ExceptionRef handle) {
        super(handle);
    }
}

void init() {
    clientResourceMutex = new Mutex();

    auto result =
        nativeImplInit(
            &clientLogFunc,
            &clientFuncExec,
            &clientMethodExec,
            &clientResourceRelease,
            &clientClearSafetyArea);
    assert(result == 0, "D side: nativeImplInit failed!");
}

void shutdown() {
    // prevent any calls to the backend (eg during object finalization), will just break
    remoteIsShutdown = true;
    nativeImplShutdown();
}

ModuleHandle getModule(string name) {
    auto h = nativemethods.getModule(name.toStringz);
    return h ? new ModuleHandle(h) : null;
}

ModuleMethodHandle getModuleMethod(ModuleHandle m, string name) {
    auto h = nativemethods.getModuleMethod(m.handle, name.toStringz);
    return h ? new ModuleMethodHandle(h) : null;
}

InterfaceHandle getInterface(ModuleHandle m, string name) {
    auto h = nativemethods.getInterface(m.handle, name.toStringz);
    return h ? new InterfaceHandle(h) : null;
}

InterfaceMethodHandle getInterfaceMethod(InterfaceHandle iface, string name) {
    auto h = nativemethods.getInterfaceMethod(iface.handle, name.toStringz);
    return h ? new InterfaceMethodHandle(h) : null;
}

ExceptionHandle getException(ModuleHandle m, string name) {
    auto h = nativemethods.getException(m.handle, name.toStringz);
    return h ? new ExceptionHandle(h) : null;
}

void pushModuleConstants(ModuleHandle m) {
    nativemethods.pushModuleConstants(m.handle);
}

void invokeModuleMethod(ModuleMethodHandle method) {
    nativemethods.invokeModuleMethod(method.handle);
}

void invokeModuleMethodWithExceptions(ModuleMethodHandle method) {
    auto e = nativemethods.invokeModuleMethodWithExceptions(method.handle);
    if (e != null) {
        // build + throw!
        exceptionBuilders[e]();
    }
}

alias ObjectPtr = ni_ObjectPtr;
auto nullObjectPtr = ObjectPtr(null, null);

interface InterfaceImplementer {
    // any client class wrapping an opaque implementing server interfaces,
    //   needs to implement this interface
    // otherwise they'd be unpushable!
    ObjectPtr objectPtr();
}

void pushObject(ObjectPtr obj) {
    nativemethods.pushObject(obj);
}

ObjectPtr popObject() {
    return nativemethods.popObject();
}

void pushObjectArray(ObjectPtr[] objects) {
    nativemethods.pushObjectArray(objects.ptr, objects.length);
}

ObjectPtr[] popObjectArray() {
    ObjectPtr* values;
    size_t count;
    nativemethods.popObjectArray(&values, &count);
    return values[0..count].dup();
}

void pushPtr(void* ptr) {
    nativemethods.pushPtr(ptr);
}

void* popPtr() {
    return nativemethods.popPtr();
}

void pushPtrArray(void*[] values) {
    nativemethods.pushPtrArray(values.ptr, values.length);
}

void*[] popPtrArray() {
    void** values;
    size_t count;
    nativemethods.popPtrArray(&values, &count);
    return values[0..count].dup();
}

void pushSizeT(size_t value) {
    nativemethods.pushSizeT(value);
}

size_t popSizeT() {
    return nativemethods.popSizeT();
}

void pushSizeTArray(size_t[] values) {
    nativemethods.pushSizeTArray(values.ptr, values.length);
}

size_t[] popSizeTArray() {
    size_t* values;
    size_t count;
    nativemethods.popSizeTArray(&values, &count);
    return values[0..count].dup();
}

void pushBool(bool b) {
    nativemethods.pushBool(b);
}

bool popBool() {
    return nativemethods.popBool();
}

void pushBoolArray(bool[] values) {
    nativemethods.pushBoolArray(values.ptr, values.length);
}

bool[] popBoolArray() {
    bool* values;
    size_t count;
    nativemethods.popBoolArray(&values, &count);
    return values[0..count].dup();
}

void pushInt8(byte x) {
    nativemethods.pushInt8(x);
}

byte popInt8() {
    return nativemethods.popInt8();
}

void pushInt8Array(byte[] values) {
    nativemethods.pushInt8Array(values.ptr, values.length);
}

byte[] popInt8Array() {
    byte* values;
    size_t count;
    nativemethods.popInt8Array(&values, &count);
    return values[0..count].dup();
}

void pushInt16(short x) {
    nativemethods.pushInt16(x);
}

short popInt16() {
    return nativemethods.popInt16();
}

void pushInt16Array(short[] values) {
    nativemethods.pushInt16Array(values.ptr, values.length);
}

short[] popInt16Array() {
    short* values;
    size_t count;
    nativemethods.popInt16Array(&values, &count);
    return values[0..count].dup();
}

void pushInt32(int x) {
    nativemethods.pushInt32(x);
}

int popInt32() {
    return nativemethods.popInt32();
}

void pushInt32Array(int[] values) {
    nativemethods.pushInt32Array(values.ptr, values.length);
}

int[] popInt32Array() {
    int* values;
    size_t count;
    nativemethods.popInt32Array(&values, &count);
    return values[0..count].dup();
}

void pushInt64(long x) {
    nativemethods.pushInt64(x);
}

long popInt64() {
    return nativemethods.popInt64();
}

void pushInt64Array(long[] values) {
    nativemethods.pushInt64Array(values.ptr, values.length);
}

long[] popInt64Array() {
    long* values;
    size_t count;
    nativemethods.popInt64Array(&values, &count);
    return values[0..count].dup();
}

void pushFloat(float x) {
    nativemethods.pushFloat(x);
}

float popFloat() {
    return nativemethods.popFloat();
}

void pushFloatArray(float[] values) {
    nativemethods.pushFloatArray(values.ptr, values.length);
}

float[] popFloatArray() {
    float* values;
    size_t count;
    nativemethods.popFloatArray(&values, &count);
    return values[0..count].dup();
}

void pushDouble(double x) {
    nativemethods.pushDouble(x);
}

double popDouble() {
    return nativemethods.popDouble();
}

void pushDoubleArray(double[] values) {
    nativemethods.pushDoubleArray(values.ptr, values.length);
}

double[] popDoubleArray() {
    double* values;
    size_t count;
    nativemethods.popDoubleArray(&values, &count);
    return values[0..count].dup();
}

// ===== unsigned, not sure if we'll keep these since not all languages support them =========

void pushUInt8(ubyte x) {
    nativemethods.pushUInt8(x);
}

ubyte popUInt8() {
    return nativemethods.popUInt8();
}

void pushUInt8Array(ubyte[] values) {
    nativemethods.pushUInt8Array(values.ptr, values.length);
}

ubyte[] popUInt8Array() {
    ubyte* values;
    size_t count;
    nativemethods.popUInt8Array(&values, &count);
    return values[0..count].dup();
}

void pushUInt16(ushort x) {
    nativemethods.pushUInt16(x);
}

ushort popUInt16() {
    return nativemethods.popUInt16();
}

void pushUInt16Array(ushort[] values) {
    nativemethods.pushUInt16Array(values.ptr, values.length);
}

ushort[] popUInt16Array() {
    ushort* values;
    size_t count;
    nativemethods.popUInt16Array(&values, &count);
    return values[0..count].dup();
}

void pushUInt32(uint x) {
    nativemethods.pushUInt32(x);
}

uint popUInt32() {
    return nativemethods.popUInt32();
}

void pushUInt32Array(uint[] values) {
    nativemethods.pushUInt32Array(values.ptr, values.length);
}

uint[] popUInt32Array() {
    uint* values;
    size_t count;
    nativemethods.popUInt32Array(&values, &count);
    return values[0..count].dup();
}

void pushUInt64(ulong x) {
    nativemethods.pushUInt64(x);
}

ulong popUInt64() {
    return nativemethods.popUInt64();
}

void pushUInt64Array(ulong[] values) {
    nativemethods.pushUInt64Array(values.ptr, values.length);
}

ulong[] popUInt64Array() {
    ulong* values;
    size_t count;
    nativemethods.popUInt64Array(&values, &count);
    return values[0..count].dup();
}

// end unsigned ==================================================

void pushString(string s) {
    nativemethods.pushString(s.toStringz, s.length);
}

string popString() {
    const (char)* str;
    size_t length;
    nativemethods.popString(&str, &length);
    return str[0..length].idup;
}

void pushStringArray(string[] values) {
    auto strs = values.map!(v => v.toStringz).array;
    auto lengths = values.map!(v => v.length).array;
    nativemethods.pushStringArray(cast(const(char)**)strs.ptr, lengths.ptr, lengths.length);
}

string[] popStringArray() {
    const (char)** strs;
    size_t* lengths;
    size_t count;
    nativemethods.popStringArray(&strs, &lengths, &count);
    string[] ret = new string[count];
    for (size_t i; i < count; i++) {
        auto len = lengths[i];
        ret[i] = strs[i][0..len].idup;
    }
    return ret;
}


void pushBuffer(T)(INativeBuffer!T buffer, bool isReturn) {
    if (buffer !is null) {
        (cast(IPushable)buffer).push(isReturn);
    } else {
        nativemethods.pushNull();
    }
}

INativeBuffer!T popBuffer(T)() {
    int id;
    bool isClientId;
    ni_BufferDescriptor desc;
    nativemethods.popBuffer(&id, &isClientId, &desc);
    if (id != 0) {
        if (isClientId) {
            return ClientBuffer!T.getById(id);
        } else {
            return new ServerBuffer!T(id, desc);
        }
    } else {
        return null;
    }
}

// void pushClientFunc(int id);
void pushClientFuncVal(FuncValWrapper wrapper, void *context, void *funcptr) {
    auto funcval = ClientFuncVal.getOrCreate(wrapper, ClientFuncKey(context, funcptr));
    funcval.push(false); // 'isReturn' always false, client func vals never come back to us as return values
}

// int popServerFunc();
ServerFuncVal popServerFuncVal() {
    auto id = nativemethods.popServerFunc();
    return new ServerFuncVal(id);
}

// void execServerFunc(int id);
// ni_ExceptionRef execServerFuncWithExceptions(int id);

void setClientMethodWrapper(T : IClientInterface)(InterfaceMethodHandle method, void function(T inst) wrapperFunc) {
    void inner(ClientObject obj) {
        auto interfaceWrapper = cast(ClientInterfaceWrapper) obj;
        auto inst = cast(T) interfaceWrapper.rawInterface;
        wrapperFunc(inst);
    }
    methodWrappers[method.handle] = &inner;
}

// void pushInstance(int id, bool isClientId);
// int popInstance(bool* isClientId);
void pushClientInterface(IClientInterface thing, bool isReturn) {
    if (thing is null) {
        nativemethods.pushNull();
    } else {
        synchronized (clientResourceMutex) {
            if (auto existing = thing in clientInterfaceWrapperMap) {
                existing.push(isReturn);
            } else {
                // it's fresh, need to create a wrapper to push it (so we don't have to force developers to inherit from ClientObject themselves)
                auto wrapper = new ClientInterfaceWrapper(thing);
                clientInterfaceWrapperMap[thing] = wrapper;
                wrapper.push(isReturn);
            }
        }
    }
}

T popClientInterface(T : IClientInterface)() {
    auto id = nativemethods.popClientInterface();
    if (id != 0) {
        // if we're popping it, we must have pushed it in the past sometime
        auto wrapper = cast(ClientInterfaceWrapper) ClientObject.getById(id);
        // we don't know exactly what it is, only that it implements T
        return cast(T) wrapper.rawInterface;
    }
    return null;
}

void pushNull() {
    nativemethods.pushNull();
}

// void releaseServerResource(int id);

// Clear server resources from safety area (protects values being returned with no other shared_ptrs existing).
void clearServerSafetyArea() {
    nativemethods.clearServerSafetyArea();
}

// void dumpTables();

void setExceptionBuilder(ExceptionHandle eh, ExceptionBuildAndThrowFunc builder) {
    exceptionBuilders[eh.handle] = builder;
}

// void setException(ni_ExceptionRef e);
void setException(ExceptionHandle eh) {
    nativemethods.setException(eh.handle);
}
