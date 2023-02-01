module nativeimplclient;
import std.stdio;
import std.string: toStringz;

import nativemethods;

// PRIVATE ====================================================================================================
private:

// C entry points:
extern (C) {
    void clientFuncExec(int id) {
        ClientFuncVal.getByID(id).exec();
    }

    void clientMethodExec(ni_InterfaceMethodRef method, int objID) {
        // writefln("dlang client method exec: %08X / %d", method, objID);
        auto obj = ClientObject.getByID(objID);
        methodWrappers[method](obj);
    }

    void clientResourceRelease(int id) {
        ClientResource.getByID(id).release();
    }

    void clientClearSafetyArea() {
        writeln("== dlang client clear safety area ==");
        safetyArea.length = 0;
        safetyArea.assumeSafeAppend(); // only because we know there are no slices out there
    }
}

__gshared bool remoteIsShutdown;
__gshared int nextResourceId = 1;
__gshared ClientResource[int] resourceMap;
__gshared ClientFuncVal[size_t] reverseFuncValDict; // key: delegateKey(...)
__gshared MethodWrapper[ni_InterfaceMethodRef] methodWrappers;
__gshared ServerObject[] safetyArea; // place to stash server objects until the other side has finished popping them from stack (in case they only exist as a return value)
__gshared ExceptionBuilder[ni_ExceptionRef] exceptionBuilders;

class ClientResource {
    int _id = -1;
    int refCount;

    abstract void nativePush(int id); // must be implemented

    void resourcePush() {
        // id is meaningless until it's about to go out, hence delayed to this point
        if (_id == -1) {
            _id = nextResourceId++;
            refCount = 1;
            resourceMap[_id] = this;
        } else {
            refCount += 1;
        }
        nativePush(_id);
    }

    void unregister() {
        writefln("dlang ClientResource.unregister(%d)", _id);
        resourceMap.remove(_id);
        // reset ID, since we're pulling it out of map
        // will have to be re-registered
        _id = -1;
        // refCount = 0;
        // override for any subclass cleanup
    }

    void release() {
        writefln("ClientResource.release()");
        refCount -= 1;
        if (refCount == 0) {
            unregister();
        }
    }

    static ClientResource getByID(int id) {
        return resourceMap[id];
    }
}

class ClientFuncVal : ClientResource {
    FuncValWrapper wrapper;
    size_t key;
    this(FuncValWrapper wrapper, size_t key) {
        this.wrapper = wrapper;
        this.key = key;
        // unlike the base ClientResource/ClientObject, a ClientFuncVal is only ever created just before being pushed
        // so no harm in filling the table entry immediately (vs. in a 'register' or push method)
        if (key) {
            reverseFuncValDict[key] = this;
        }
    }
    override void nativePush(int id) {
        nativemethods.pushClientFunc(id);
    }
    override void unregister() {
        super.unregister();
        // remove self from reverse dict
        // unlike ClientObjects, there's should be no potential for a ClientFuncVal being reused once it's removed, because they only exist within library wrapper code
        writefln("dlang ClientFuncVal.unregister()");
        if (key) {
            reverseFuncValDict.remove(key);
        }
    }
    void exec() {
        // woot!
        wrapper();
    }
    // static methods -------------------------------
    static ClientFuncVal getByID(int id) {
        return cast(ClientFuncVal) ClientResource.getByID(id);
    }
    static ClientFuncVal getOrCreate(FuncValWrapper wrapper, size_t key) {
        if (key == 0 || !(key in reverseFuncValDict)) { // either doesn't exist, or the key is 0 so we can't check for uniqueness
            // new
            return new ClientFuncVal(wrapper, key);
        } else {
            // return existing
            // used to addRef here, but now that's taken care of by ClientResource.push()
            return reverseFuncValDict[key];
        }
    }
}

class ServerResource {
    int id;
    bool disposed;
    this(int id) {
        this.id = id;
    }
    void dispose() {
        if (!disposed) {
            if (!remoteIsShutdown) {
                nativemethods.releaseServerResource(id);
            } else {
                writefln("(didn't release server obj %d - remote was already shutdown)", id);
            }
            disposed = true;
        }
    }
    ~this() {
        dispose();
    }
}

// PUBLIC ================================================================================================
public:

class WrappedHandle(T) {
    private T handle;
    private this(T handle) {
        this.handle = handle;
    }
}

class ModuleRef : WrappedHandle!ni_ModuleRef {
    private this(ni_ModuleRef handle) {
        super(handle);
    }
}

class ModuleMethodRef : WrappedHandle!ni_ModuleMethodRef {
    private this(ni_ModuleMethodRef handle) {
        super(handle);
    }
}

class InterfaceRef : WrappedHandle!ni_InterfaceRef {
    private this(ni_InterfaceRef handle) {
        super(handle);
    }
}

class InterfaceMethodRef : WrappedHandle!ni_InterfaceMethodRef {
    private this(ni_InterfaceMethodRef handle) {
        super(handle);
    }
}

class ExceptionRef : WrappedHandle!ni_ExceptionRef {
    private this(ni_ExceptionRef handle) {
        super(handle);
    }
}

int init() {
    return nativeImplInit(
        &clientFuncExec,
        &clientMethodExec,
        &clientResourceRelease,
        &clientClearSafetyArea);
}

void shutdown() {
    // prevent any calls to the backend (eg during object finalization), will just break
    remoteIsShutdown = true;
    nativeImplShutdown();
}

ModuleRef getModule(string name) {
    auto h = nativemethods.getModule(name.toStringz);
    return h ? new ModuleRef(h) : null;
}

ModuleMethodRef getModuleMethod(ModuleRef m, string name) {
    auto h = nativemethods.getModuleMethod(m.handle, name.toStringz);
    return h ? new ModuleMethodRef(h) : null;
}

InterfaceRef getInterface(ModuleRef m, string name) {
    auto h = nativemethods.getInterface(m.handle, name.toStringz);
    return h ? new InterfaceRef(h) : null;
}

InterfaceMethodRef getInterfaceMethod(InterfaceRef iface, string name) {
    auto h = nativemethods.getInterfaceMethod(iface.handle, name.toStringz);
    return h ? new InterfaceMethodRef(h) : null;
}

ExceptionRef getException(ModuleRef m, string name) {
    auto h = nativemethods.getException(m.handle, name.toStringz);
    return h ? new ExceptionRef(h) : null;
}

void invokeModuleMethod(ModuleMethodRef method) {
    nativemethods.invokeModuleMethod(method.handle);
}

void invokeModuleMethodWithExceptions(ModuleMethodRef method) {
    auto e = nativemethods.invokeModuleMethodWithExceptions(method.handle);
    if (e) {
        throw exceptionBuilders[e]();
    }
}

void pushBool(bool b) {
    nativemethods.pushBool(b);
}

bool popBool() {
    return nativemethods.popBool();
}

void pushInt32(int x) {
    nativemethods.pushInt32(x);
}

int popInt32() {
    return nativemethods.popInt32();
}

alias FuncValWrapper = void delegate();

void pushClientFuncVal(FuncValWrapper wrapper, size_t key) {
    auto f = ClientFuncVal.getOrCreate(wrapper, key);
    f.resourcePush();
}

ServerFuncVal popServerFuncVal() {
    auto id = nativemethods.popServerFunc();
    return new ServerFuncVal(id);
}

int popInstance(ref bool isClientId, ref bool inSafetyArea) {
    return nativemethods.popInstance(&isClientId, &inSafetyArea);
}

void pushNull() {
    nativemethods.pushNull();
}

void clearServerSafetyArea() {
    nativemethods.clearServerSafetyArea();
}

alias MethodWrapper = void delegate(ClientObject obj);

void setClientMethodWrapper(InterfaceMethodRef method, MethodWrapper wrapper) {
    methodWrappers[method.handle] = wrapper;
}

alias ExceptionBuilder = Exception delegate();

void setExceptionBuilder(ExceptionRef e, ExceptionBuilder builder) {
    exceptionBuilders[e.handle] = builder;
}

void setException(ExceptionRef e) {
    nativemethods.setException(e.handle);
}

// to prevent duplicates of a given func value
size_t delegateKey(T)(T d) {
    return d.ptr ? cast(size_t)d.ptr : cast(size_t)d.funcptr; // use context first if it exists, otherwise func ptr
}

class ServerFuncVal : ServerResource {
    this(int id) {
        super(id);
    }
    // finalizer handled by ServerResource
    void exec() {
        nativemethods.execServerFunc(id);
    }
}

// wrapper methods taking interface arguments won't know if they are dealing with a server or client objects,
// so both implement this interface for easy pushing
interface IPushable {
    void push(bool isReturnValue);
}

class ServerObject : ServerResource, IPushable {
    this(int id) {
        super(id);
    }
    // finalizer handled by ServerResource
    void invokeMethod(InterfaceMethodRef method) {
        nativemethods.invokeInterfaceMethod(method.handle, id);
    }
    void invokeWithExceptions(InterfaceMethodRef method) {
        auto e = nativemethods.invokeInterfaceMethodWithExceptions(method.handle, id);
        if (e) {
            throw exceptionBuilders[e]();
        }
    }
    override void push(bool isReturnValue) {
        if (isReturnValue) {
            safetyArea ~= this;
        }
        nativemethods.pushInstance(id, false, isReturnValue);
    }
}

class ClientObject: ClientResource, IPushable {
    override void nativePush(int id) {
        nativemethods.pushInstance(id, true, false); // never in safety area - only applicable when returning server objects to server
    }
    override void push(bool isReturnValue) {
        // isReturnValue unused - client objects never go in safety area
        resourcePush(); // rely on ClientResource push to determine ID / addref before sending out
    }
    // static methods:
    static ClientObject getByID(int id) {
        return cast(ClientObject) ClientResource.getByID(id);
    }
}

// lifetime management stuff

interface IDisposable {
    void dispose();
}

// void using(IDisposable thing, void delegate() woot) {
//     try {
//         woot();
//     } finally {
//         thing.dispose();
//     }
// }

// thanks to Paul Backus:
struct Disposable(T : IDisposable)
{
    T unwrap;
    alias unwrap this;

    ~this()
    {
        if (unwrap !is null)
            unwrap.dispose;
    }
}

Disposable!T using(T : IDisposable)(T payload)
{
    return Disposable!T(payload);
}

