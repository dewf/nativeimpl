module corestuff;

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

	// instead of accessing these directly, perhaps they should be behind functions exported from core?
	extern __gshared niClientFuncExec ni_clientFuncExec;
	extern __gshared niClientFuncRelease ni_clientFuncRelease;
	extern __gshared niClientMethodExec ni_clientMethodExec;
	extern __gshared niClientObjRelease ni_clientObjRelease;
	extern __gshared niClientClearSafetyArea ni_clientClearSafetyArea;
	extern __gshared ni_ExceptionRef ni_currentException;

    alias ModuleFunc = void function();
    alias InterfaceFunc = void function(int serverID);
    alias ConstantsFunc = void function();
    alias ExceptionBuilder = void function();

	extern ni_ModuleRef ni_registerModule(const char *name);
	extern ni_ModuleMethodRef ni_registerModuleMethod(ni_ModuleRef m, const char *name, ModuleFunc func);
	extern void ni_registerModuleConstants(ni_ModuleRef m, ConstantsFunc func);
	extern ni_InterfaceRef ni_registerInterface(ni_ModuleRef m, const char *name);
	extern ni_InterfaceMethodRef ni_registerInterfaceMethod(ni_InterfaceRef iface, const char *name, InterfaceFunc ifunc);
	extern ni_ExceptionRef ni_registerException(ni_ModuleRef m, const char *name, ExceptionBuilder builder);

	extern void ni_pushBool(bool b);
	extern bool ni_popBool();
	extern void ni_pushInt(int x);
	extern int ni_popInt();

	extern int ni_popClientFunc();
	extern void ni_pushServerFunc(int id);

	extern void ni_pushClientInst(int id);
	extern void ni_pushServerInst(int id);
	extern int ni_popInstance(bool* isClient);

	extern void ni_setException(ni_ExceptionRef e);
	extern void ni_buildAndthrow(ni_ExceptionRef e);

	extern void ni_clearClientSafetyArea();
}