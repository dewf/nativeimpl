module nativeimplserver;
import std.stdio;

import corestuff;

class ClientFunc {
protected:
    int _id;
public:
    this(int id) {
        _id = id;
    }
    void remoteExec() {
        ni_clientFuncExec(_id);
    }
    ~this() {
        // release remote function, we're done with it
        // (only reduces remote refcount - there may be other client refCounts referring to separate ClientFuncs on this side!)
        // although I think a weak_ptr table (id -> weak_ptr) will get rid of that issue, deduplicating for us!
        writefln("dlang server: ClientFunc dtor releasing func id: %d", _id);
        stdout.flush();
        ni_clientFuncRelease(_id);
    }
}

interface Pushable {
    void push(bool isReturn);
}

class ServerObject : Pushable {
    int _id = -1;
    static ServerObject getById(int id) {
        return serverObjects[id].obj;
    }
    void push(bool isReturn) {
        // isReturn not used for server stuff, that's just to help out with the safe area / protecting client objects
        if (_id == -1) { // unset as yet
            _id = nextServerObjectId++;
            serverObjects[_id] = ServerObjectInfo(_id, this, 1);
        } else {
            // add ref to existing
            serverObjects[_id].refCount++; // this assumes that it's going across a boundary which will be balanced by an eventual deref. is that always true?
            writefln("bumped server ID %d refcount to %d", _id, serverObjects[_id].refCount);
        }
        ni_pushServerInst(_id);
    }
}

class ClientObject : Pushable {
private:
    int _id;
public:
    this(int id) {
        _id = id;
    }
    void invokeMethod(ni_InterfaceMethodRef method) {
        writefln("dlang server ClientObject::invokeMethod(%08X) on id: %d", method, _id);
        ni_clientMethodExec(method, _id);
        assert(ni_currentException == null); // never use this where an exception can throw! (trying to keep things tight for maximum throughput)
    }
    void invokeMethodWithExceptions(ni_InterfaceMethodRef method) {
        writefln("dlang server ClientObject::invokeMethodWithExceptions(%08X) on id: %d", method, _id);
        assert(ni_currentException == null);
        ni_clientMethodExec(method, _id);
        if (ni_currentException != null) {
            auto e = ni_currentException;
            ni_currentException = null;
            ni_buildAndthrow(e);
        }
    }
    void push(bool isReturn) {
        if (isReturn) {
            // for return values, to keep them alive until the other side has taken them
            writefln("dlang server ClientObject %d safepush", _id);
            ni_pushClientInst(_id);
            safeArea ~= this;
        } else {
            ni_pushClientInst(_id);
        }
    }
    ~this() {
        writeln("dlang server ClientObject dtor, remote releasing");
        ni_clientObjRelease(_id);
    }
}

// while server objects are kept alive ("checked out") via the serverObjects/refcount,
// any client objects that currently only exist on this side, need to be protected during returns to keep their shared_ptr from going away
// and releasing them prematurely (dropping remote refcount to 0)
// the other side will call clearServerSafetyArea when it's gotten the return value
// each side has its own safety area, to protect the other's resources during function return handoff
__gshared ClientObject[] safeArea;

alias WrapperFunc = void delegate();

struct ServerFuncVal {
    int id;
    WrapperFunc wrapper;
    size_t key;
    int refCount;
}
// TODO: make thread-safe
__gshared int nextServerFuncId = 1;
__gshared ServerFuncVal[int] serverFuncVals;
__gshared int[size_t] reverseServerFuncVals;

int addFuncVal(WrapperFunc wrapper, size_t key) {
    if (key != 0 && key in reverseServerFuncVals) {
        // increment refcount of existing
        auto id = reverseServerFuncVals[key];
        serverFuncVals[id].refCount++;
        return id;
    }
    // either no key provided, or not found; create anew
    auto id = nextServerFuncId++;
    serverFuncVals[id] = ServerFuncVal(id, wrapper, key, 1);
    if (key != 0) {
        // no point stuff keyless things into the reverse map, all on top of each other
        // (actually this was from the C++, where we'd sometimes have 0 keys, because no way to pull apart a std::function if it was a closure)
        reverseServerFuncVals[key] = id;
    }
    return id;
}

void pushServerFuncVal(WrapperFunc wrapper, size_t key) {
    auto id = addFuncVal(wrapper, key);
    ni_pushServerFunc(id);
}

struct ServerObjectInfo {
    int id;
    ServerObject obj;
    int refCount;
}
// TODO: make thread-safe
__gshared int nextServerObjectId = 1;
__gshared ServerObjectInfo[int] serverObjects;

// some "exports" back to core (well, the DLL being built around core)
extern (C) {
	void ni_dumpTables() {
        writeln("== dlang server func vals:");
        foreach(fv; serverFuncVals) {
            writefln(" - func %d: refcount %d", fv.id, fv.refCount);
        }
        writeln("== dlang server objects:");
        foreach(obj; serverObjects) {
            writefln(" - obj %d: refcount %d", obj.id, obj.refCount);
        }
    }
	void ni_execServerFunc(int id) {
        serverFuncVals[id].wrapper();
    }
	void ni_releaseServerFunc(int id) {
        serverFuncVals[id].refCount -= 1;
        if (serverFuncVals[id].refCount == 0) {
            writefln("server releasefuncval(%d): refcount 0, releasing", id);
            auto key = serverFuncVals[id].key;
            reverseServerFuncVals.remove(key);
            serverFuncVals.remove(id);
        }
    }
	void ni_releaseServerInst(int id) {
        serverObjects[id].refCount -= 1;
        if (serverObjects[id].refCount == 0) {
            writefln("dlang server: releasing server obj %d", id);
            serverObjects.remove(id);
        } else {
            writefln("dlang server: releaseServerInst(%d) - refcount still %d", id, serverObjects[id].refCount);
        }
    }
	void ni_clearServerSafetyArea() {
        writeln("dlang server before safety clear =========");
        safeArea.length = 0;
        safeArea.assumeSafeAppend(); // because no slices of it exist elsewhere
        writeln("dlang server after safety clear ==========");
    }
}
