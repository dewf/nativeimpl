module testlibrary;

import std.stdio;
import std.conv: to;
import std.sumtype;
import nativeimplclient;
import simpleset: Set;

private:

ModuleRef _module;
ModuleMethodRef _myOpaqueSomeMethod;
ModuleMethodRef _makeOpaque;

public:

struct MyOpaque {
private:
    void* _ptr;
    this(void* ptr) {
        _ptr = ptr;
    }
    void push() {
        nativeimplclient.pushPtr(_ptr);
    }
    static MyOpaque pop() {
        auto ptr = nativeimplclient.popPtr();
        return MyOpaque(ptr);
    }
}

int myOpaqueSomeMethod(MyOpaque _this, int a, string b) {
    nativeimplclient.pushString(b);
    nativeimplclient.pushInt32(a);
    (_this).push();
    nativeimplclient.invokeModuleMethod(_myOpaqueSomeMethod);
    return nativeimplclient.popInt32();
}

MyOpaque makeOpaque(int param1) {
    nativeimplclient.pushInt32(param1);
    nativeimplclient.invokeModuleMethod(_makeOpaque);
    return MyOpaque.pop();
}

void init() {
    assert(nativeimplclient.init() == 0, "nativeimplclient.init() failed");
    _module = getModule("TestLibrary");

    _myOpaqueSomeMethod = nativeimplclient.getModuleMethod(_module, "myOpaqueSomeMethod");
    _makeOpaque = nativeimplclient.getModuleMethod(_module, "makeOpaque");
}

void shutdown() {
    nativeimplclient.shutdown();
}

void dumpTables() {
    nativeimplclient.dumpTables();
}
