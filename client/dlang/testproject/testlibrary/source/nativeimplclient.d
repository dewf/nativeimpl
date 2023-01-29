module nativeimplclient;
import std.stdio;
import std.string: toStringz;

import nativemethods;

class WrappedHandle(T) {
    private T handle;
    public this(T handle) {
        this.handle = handle;
    }
}

class ModuleRef : WrappedHandle!ni_ModuleRef {
    public this(ni_ModuleRef handle) {
        super(handle);
    }
}

class ModuleMethodRef : WrappedHandle!ni_ModuleMethodRef {
    public this(ni_ModuleMethodRef handle) {
        super(handle);
    }
}

class InterfaceRef : WrappedHandle!ni_InterfaceRef {
    public this(ni_InterfaceRef handle) {
        super(handle);
    }
}

class InterfaceMethodRef : WrappedHandle!ni_InterfaceMethodRef {
    public this(ni_InterfaceMethodRef handle) {
        super(handle);
    }
}

class ObjectRef : WrappedHandle!ni_ObjectRef {
    public this(ni_ObjectRef handle) {
        super(handle);
    }
}

class ExceptionRef : WrappedHandle!ni_ExceptionRef {
    public this(ni_ExceptionRef handle) {
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

// C entry points:
extern (C) {
    void clientFuncExec(int id) {
        ClientFuncVal.getByID(id).exec();
    }

    void clientMethodExec(ni_InterfaceMethodRef method, int objID) {
        // writefln("dlang client method exec: %08X / %d", method, objID);
        // auto obj = clientObjects[objID];
        // methodWrappers[method](obj);
    }

    void clientResourceRelease(int id) {
        ClientResource.getByID(id).release();
    }

    void clientClearSafetyArea() {
        // writeln("dlang client clear safety area");
        // safetyArea.length = 0;
        // safetyArea.assumeSafeAppend(); // only because we know there are no slices out there
    }
}

// wrapped versions of native methods ======================================================

public:

ModuleRef getModule(string name) {
    auto h = nativemethods.getModule(name.toStringz);
    return h ? new ModuleRef(h) : null;
}

ModuleMethodRef getModuleMethod(ModuleRef m, string name) {
    auto h = nativemethods.getModuleMethod(m.handle, name.toStringz);
    return h ? new ModuleMethodRef(h) : null;
}

void invokeModuleMethod(ModuleMethodRef method) {
    nativemethods.invokeModuleMethod(method.handle);
}

InterfaceRef getInterface(ModuleRef m, string name) {
    auto h = nativemethods.getInterface(m.handle, name.toStringz);
    return h ? new InterfaceRef(h) : null;
}

InterfaceMethodRef getInterfaceMethod(InterfaceRef iface, string name) {
    auto h = nativemethods.getInterfaceMethod(iface.handle, name.toStringz);
    return h ? new InterfaceMethodRef(h) : null;
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
    nativemethods.pushClientFunc(f.id);
}

ServerFuncVal popServerFuncVal() {
    auto id = nativemethods.popServerFunc();
    return new ServerFuncVal(id);
}

int popInstance(ref bool isClientId, ref bool inSafetyArea) {
    return nativemethods.popInstance(&isClientId, &inSafetyArea);
}

void clearServerSafetyArea() {
    nativemethods.clearServerSafetyArea();
}

// interface Pushable {
//     void push(bool isReturn);
// }

// abstract class ClientObject : Pushable {
//     private int _id = -1;
//     private int _refCount;
//     int id() = _id;
//     int refCount() = _refCount;

//     public void push(bool _isReturn) {
//         writeln("in ClientObject.clientPush()");
//         if (_id == -1) {
//             // unset as yet
//             _id = nextClientObjectId++;
//             _refCount = 1;
//             clientObjects[_id] = this;
//             writefln("dlang ClientObject: pushed %d", _id);
//         } else{
//             // just increase refcount as we pass it over
//             _refCount++;
//             writefln("dlang ClientObject: increased refcount to %d", _id);
//         }
//         pushClientInst(_id);
//     }
//     public void release() {
//         _refCount -= 1;
//         if (_refCount == 0) {
//             writefln("dlang release client object %d - refCount 0, removing from table", _id);
//             clientObjects.remove(_id);
//             _id = -1; // reset ID, since we're not in the table anymore
//             // all this does is remove this object from the 'checked-out' table -
//             // things we were force-kept-alive as long as the remote (server) side had reference to them
//             // but that doesn't mean we're ready to dispose it - might still be useful to us on this side!
//         }
//     }
//     public static ClientObject getById(int id) {
//         return clientObjects[id];
//     }
// }

// class ServerObject : Pushable {
//     protected int _id;
//     private bool _remoteReleased;
//     public this(int id) {
//         _id = id;
//     }
//     ~this() {
//         // backstop in case the derived class not manually disposed
//         writefln("dlang ServerObject id %d - dtor invoked, calling ServerDispose()", _id);
//         serverDispose();
//     }
//     void serverDispose() {
//         if (_remoteReleased) return;
//         safeReleaseServerInstance(_id);
//         _remoteReleased = true;
//     }
//     void makeSafe() {
//         safetyArea ~= this;
//     }
//     void invoke(ni_InterfaceMethodRef method) {
//         invokeInterfaceMethod(method, _id);
//     }
//     void invokeWithExceptions(ni_InterfaceMethodRef method) {
//         auto e = invokeInterfaceMethodWithExceptions(method, _id);
//         if (e) {
//             throw exceptionBuilders[e]();
//         }
//     }
//     void push(bool isReturn) {
//         pushServerInst(_id);
//         if (isReturn) {
//             // save this object to the safety area, until the other side has pulled it off
//             // otherwise, if this is the last proxy on this side,
//             // could remotely detonate the server's real copy before it's done taking 'ownership'
//             // (dependent on probably-unlikely GC collection, but better safe than sorry)
//             makeSafe();
//         }
//     }
// }

// alias MethodWrapper = void delegate(ClientObject obj);

// void setClientMethodWrapper(ni_InterfaceMethodRef method, MethodWrapper wrapper) {
//     methodWrappers[method] = wrapper;
// }

// alias ExceptionBuilder = Exception delegate();

// void setExceptionBuilder(ni_ExceptionRef e, ExceptionBuilder builder) {
//     exceptionBuilders[e] = builder;
// }

// // void invokeWithExceptions(ni_ModuleMethodRef method) {
// //     auto e = invokeModuleMethodWithExceptions(method);
// //     if (e) {
// //         throw exceptionBuilders[e]();
// //     }
// // }

// PRIVATE ====================================================================================================
private:

__gshared bool remoteIsShutdown;

__gshared int nextResourceId = 1;
__gshared ClientResource[int] resourceMap;

class ClientResource {
    int id = -1;
    int refCount;
    this() {
        id = nextResourceId++;
        refCount = 1;
        resourceMap[id] = this;
    }
    void unregister() {
        writefln("dlang ClientResource.unregister(%d)", id);
        resourceMap.remove(id);
        // override for any subclass cleanup
    }
    void addRef() {
        refCount += 1;
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

// to prevent duplicates of a given func value
public size_t delegateKey(T)(T d) {
    return d.ptr ? cast(size_t)d.ptr : cast(size_t)d.funcptr; // use context first if it exists, otherwise func ptr
}
__gshared ClientFuncVal[size_t] reverseFuncValDict;

class ClientFuncVal : ClientResource {
    FuncValWrapper wrapper;
    size_t key;
    this(FuncValWrapper wrapper, size_t key) {
        this.wrapper = wrapper;
        this.key = key;
        // store in reverse dict
        if (key) {
            reverseFuncValDict[key] = this;
        }
    }
    override void unregister() {
        super.unregister();
        // remove self from reverse dict
        writefln("dlang ClientFuncVal.unregister()");
        if (key) {
            reverseFuncValDict.remove(key);
        }
    }
    void exec() {
        // woot!
        wrapper();
    }
    // static methods:
    static ClientFuncVal getByID(int id) {
        return cast(ClientFuncVal) ClientResource.getByID(id);
    }
    static ClientFuncVal getOrCreate(FuncValWrapper wrapper, size_t key) {
        if (key == 0 || !(key in reverseFuncValDict)) { // either doesn't exist, or the key is 0 so we can't check for uniqueness
            // new
            return new ClientFuncVal(wrapper, key);
        } else {
            // else just increment refcount of existing
            auto existing = reverseFuncValDict[key];
            existing.addRef();
            return existing;
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
                writefln("(didn't release server obj %d - remote was already shutdown", id);
            }
            disposed = true;
        }
    }
    ~this() {
        dispose();
    }
}

// __gshared MethodWrapper[ni_InterfaceMethodRef] methodWrappers;

// __gshared ExceptionBuilder[ni_ExceptionRef] exceptionBuilders;

// PUBLIC ==============================================================================================
public:

interface IPushable {
    void push(bool isReturnValue);
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

__gshared ServerObject[] safetyArea; // place to stash server objects until the other side has finished popping them from stack (in case they only exist as a return value)

class ServerObject : ServerResource, IPushable {
    this(int id) {
        super(id);
    }
    // finalizer handled by ServerResource
    void invokeMethod(InterfaceMethodRef method) {
        nativemethods.invokeInterfaceMethod(method.handle, id);
    }
    override void push(bool isReturnValue) {
        if (isReturnValue) {
            safetyArea ~= this;
        }
        nativemethods.pushInstance(id, false, isReturnValue);
    }
}

// TODO: clientobject
// serverobject
// hmmm ...

