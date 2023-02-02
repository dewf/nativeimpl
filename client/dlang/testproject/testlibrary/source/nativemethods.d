module nativemethods;

import std.format;

extern (C) {
    template NIHANDLE(string name)
    {
        const char[] NIHANDLE = "struct ni_" ~ name ~ "; alias ni_" ~ name ~ "Ref = ni_" ~ name ~ "*;";
    }

    mixin(NIHANDLE!("Module"));
    mixin(NIHANDLE!("ModuleMethod"));
    mixin(NIHANDLE!("Interface"));
    mixin(NIHANDLE!("InterfaceMethod"));
    // mixin(NIHANDLE!("Object"));
    mixin(NIHANDLE!("Exception"));

    alias niClientFuncExec = void function(int id);
    alias niClientMethodExec = void function(ni_InterfaceMethodRef method, int objID);
    alias niClientResourceRelease = void function(int resID);
    alias niClientClearSafetyArea = void function();

    int nativeImplInit(
        niClientFuncExec clientFuncExec,
        niClientMethodExec clientMethodExec,
        niClientResourceRelease clientResourceRelease,
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

    void pushInt32(int x);
    int popInt32();

    void pushString(const(char)* str, size_t length);
    void popString(const(char)** strPtr, size_t* length);

    void pushClientFunc(int id);
    int popServerFunc();
    void execServerFunc(int id);
    
    void pushInstance(int id, bool isClientId, bool inSafetyArea);
    int popInstance(bool* isClientId, bool* inSafetyArea);

    void pushNull();

    void releaseServerResource(int id);
    
    void setException(ni_ExceptionRef e);

    void clearServerSafetyArea();
}

