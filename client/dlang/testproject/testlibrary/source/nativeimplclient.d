module nativeimplclient;
import std.stdio;

extern (C) {
    template NIHANDLE(string name)
    {
        const char[] NIHANDLE = "struct ni_" ~ name ~ "; alias ni_" ~ name ~ "Ref = ni_" ~ name ~ "*;";
    }

    mixin(NIHANDLE!("Module"));
    mixin(NIHANDLE!("ModuleMethod"));
    mixin(NIHANDLE!("Interface"));
    mixin(NIHANDLE!("InterfaceMethod"));
    mixin(NIHANDLE!("Object"));
    mixin(NIHANDLE!("Exception"));

    alias niClientFuncExec = void function(int id);
    alias niClientFuncRelease = void function(int id);
    alias niClientMethodExec = void function(ni_InterfaceMethodRef method, int objID);
    alias niClientObjRelease = void function(int objID);
    alias niClientClearSafetyArea = void function();

    int nativeImplInit(
        niClientFuncExec clientFuncExec,
        niClientFuncRelease clientFuncRelease,
        niClientMethodExec clientMethodExec,
        niClientObjRelease clientObjRelease,
        niClientClearSafetyArea clientClearSafetyArea);

    void nativeImplShutdown();

    ni_ModuleRef getModule(const char* name);
    ni_ModuleMethodRef getModuleMethod(ni_ModuleRef m, const char *name);
    void invokeModuleMethod(ni_ModuleMethodRef method);
    ni_ExceptionRef invokeModuleMethodWithExceptions(ni_ModuleMethodRef method);

    ni_InterfaceRef getInterface(ni_ModuleRef m, const char *name);
    ni_InterfaceMethodRef getInterfaceMethod(ni_InterfaceRef iface, const char *name);
    void invokeInterfaceMethod(ni_InterfaceMethodRef method, int serverId);
    ni_ExceptionRef invokeInterfaceMethodWithExceptions(ni_InterfaceMethodRef method, int serverId);

    ni_ExceptionRef getException(ni_ModuleRef m, const char *name);

    void pushBool(bool b);
    bool popBool();
    void pushInt(int x);
    int popInt();

    void pushClientFunc(int id);
    int popServerFunc();
    void releaseServerFunc(int id);
    void execServerFunc(int id);

    void pushClientInst(int id);
    void pushServerInst(int id);
    void releaseServerInst(int id);
    int popInstance(bool* isClientId);

    void setException(ni_ExceptionRef e);

    void clearServerSafetyArea();
}

int init() {
    return nativeImplInit(
        &clientFuncExec,
        &clientFuncRelease,
        &clientMethodExec,
        &clientObjRelease,
        &clientClearSafetyArea);
}

alias FuncValWrapper = void delegate();

void pushClientFuncVal(FuncValWrapper wrapper, size_t key) {
    const id = addFuncVal(wrapper, key);
    pushClientFunc(id);
}

class ServerFunc {
    private int _id;
    public this(int id) {
        _id = id;
    }
    ~this() {
        // verify remote not shut down to avoid problems
        if (!remoteIsShutdown) {
            releaseServerFunc(_id);
        }
    }
    public void exec() {
        execServerFunc(_id);
    }
}

interface Pushable {
    void push(bool isReturn);
}

abstract class ClientObject : Pushable {
    private int _id = -1;
    private int _refCount;
    int id() = _id;
    int refCount() = _refCount;

    public void push(bool _isReturn) {
        writeln("in ClientObject.clientPush()");
        if (_id == -1) {
            // unset as yet
            _id = nextClientObjectId++;
            _refCount = 1;
            clientObjects[_id] = this;
            writefln("dlang ClientObject: pushed %d", _id);
        } else{
            // just increase refcount as we pass it over
            _refCount++;
            writefln("dlang ClientObject: increased refcount to %d", _id);
        }
        pushClientInst(_id);
    }
    public void release() {
        _refCount -= 1;
        if (_refCount == 0) {
            writefln("dlang release client object %d - refCount 0, removing from table", _id);
            clientObjects.remove(_id);
            _id = -1; // reset ID, since we're not in the table anymore
            // all this does is remove this object from the 'checked-out' table -
            // things we were force-kept-alive as long as the remote (server) side had reference to them
            // but that doesn't mean we're ready to dispose it - might still be useful to us on this side!
        }
    }
    public static ClientObject getById(int id) {
        return clientObjects[id];
    }
}

class ServerObject : Pushable {
    protected int _id;
    private bool _remoteReleased;
    public this(int id) {
        _id = id;
    }
    ~this() {
        // backstop in case the derived class not manually disposed
        writefln("dlang ServerObject id %d - dtor invoked, calling ServerDispose()", _id);
        serverDispose();
    }
    void serverDispose() {
        if (_remoteReleased) return;
        safeReleaseServerInstance(_id);
        _remoteReleased = true;
    }
    void makeSafe() {
        safetyArea ~= this;
    }
    void invoke(ni_InterfaceMethodRef method) {
        invokeInterfaceMethod(method, _id);
    }
    void invokeWithExceptions(ni_InterfaceMethodRef method) {
        auto e = invokeInterfaceMethodWithExceptions(method, _id);
        if (e) {
            throw exceptionBuilders[e]();
        }
    }
    void push(bool isReturn) {
        pushServerInst(_id);
        if (isReturn) {
            // save this object to the safety area, until the other side has pulled it off
            // otherwise, if this is the last proxy on this side,
            // could remotely detonate the server's real copy before it's done taking 'ownership'
            // (dependent on probably-unlikely GC collection, but better safe than sorry)
            makeSafe();
        }
    }
}

alias MethodWrapper = void delegate(ClientObject obj);

void setClientMethodWrapper(ni_InterfaceMethodRef method, MethodWrapper wrapper) {
    methodWrappers[method] = wrapper;
}

alias ExceptionBuilder = Exception delegate();

void setExceptionBuilder(ni_ExceptionRef e, ExceptionBuilder builder) {
    exceptionBuilders[e] = builder;
}

void invokeWithExceptions(ni_ModuleMethodRef method) {
    auto e = invokeModuleMethodWithExceptions(method);
    if (e) {
        throw exceptionBuilders[e]();
    }
}

// PRIVATE ====================================================================================================
private:

__gshared bool remoteIsShutdown;

struct FuncValInfo {
    FuncValWrapper wrapper;
    size_t key;
    int refCount;
}
__gshared FuncValInfo[int] funcValDict;
__gshared int[size_t] reverseFuncValDict;
__gshared int nextFuncValId = 1;

int addFuncVal(FuncValWrapper wrapper, size_t key) {
    if (key == 0 || !(key in reverseFuncValDict)) { // either doesn't exist, or the key is 0 so we can't check for uniqueness
        const id = nextFuncValId++;
        funcValDict[id] = FuncValInfo(wrapper, key, 1);
        if (key) {
            reverseFuncValDict[key] = id;
        }
        return id;
    } else {
        // else just increment refcount of existing
        const id = reverseFuncValDict[key];
        funcValDict[id].refCount += 1;
        return id;
    }
}

__gshared int nextClientObjectId = 1;
__gshared ClientObject[int] clientObjects;
__gshared MethodWrapper[ni_InterfaceMethodRef] methodWrappers;

void safeReleaseServerInstance(int id) { // ReleaseServerInstanceId in C#
    if (!remoteIsShutdown) {
        releaseServerInst(id);
    }
}

__gshared ServerObject[] safetyArea; // place to stash server objects until the other side has finished popping them from stack (in case they only exist as a return value)

__gshared ExceptionBuilder[ni_ExceptionRef] exceptionBuilders;


// C entry points:
extern (C) {
    void clientFuncExec(int id) {
        writefln("dlang client func exec: %d", id);
        funcValDict[id].wrapper();
    }

    void clientFuncRelease(int id) {
        writefln("dlang client func release: %d", id);
        auto info = funcValDict[id];
        info.refCount -= 1;
        if (info.refCount == 0) {
            // truly delete
            reverseFuncValDict.remove(info.key);
            funcValDict.remove(id);
            writefln(" - refcount 0, dlang callback %d removed", id);
        } else {
            // store with updated refcount
            funcValDict[id] = info;
            writefln(" - updated refcount: %d", info.refCount);
        }
    }

    void clientMethodExec(ni_InterfaceMethodRef method, int objID) {
        writefln("dlang client method exec: %08X / %d", method, objID);
        auto obj = clientObjects[objID];
        methodWrappers[method](obj);
    }

    void clientObjRelease(int objID) {
        writefln("dlang client obj release: %d", objID);
        clientObjects[objID].release();
    }

    void clientClearSafetyArea() {
        writeln("dlang client clear safety area");
        safetyArea.length = 0;
        safetyArea.assumeSafeAppend(); // only because we know there are no slices out there
    }
}
