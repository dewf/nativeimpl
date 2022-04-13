#pragma once

#include <string>

#define NIHANDLE(x) struct ni_##x; typedef struct ni_##x* ni_##x##Ref

NIHANDLE(Module);
NIHANDLE(ModuleMethod);
NIHANDLE(Interface);
NIHANDLE(InterfaceMethod);
NIHANDLE(Object);
NIHANDLE(Exception);

typedef void (*niClientFuncExec)(int id);
typedef void (*niClientFuncRelease)(int id);
typedef void (*niClientMethodExec)(ni_InterfaceMethodRef method, int objID);
typedef void (*niClientObjRelease)(int objID);
typedef void (*niClientClearSafetyArea)();

// our imports from core (we could include directly, but this lets us simulate what it will be like for other languages)
extern "C" {
	// instead of accessing these directly, perhaps they should be behind functions exported from core?
	extern niClientFuncExec ni_clientFuncExec;
	extern niClientFuncRelease ni_clientFuncRelease;
	extern niClientMethodExec ni_clientMethodExec;
	extern niClientObjRelease ni_clientObjRelease;
	extern niClientClearSafetyArea ni_clientClearSafetyArea;
	extern ni_ExceptionRef ni_currentException;

	typedef void (*ModuleFunc)();
	typedef void (*InterfaceFunc)(int serverID);
	typedef void (*ConstantsFunc)();
	typedef void (*ExceptionBuilder)(); // more of a 'pop and throw' than building, per se

	extern ni_ModuleRef ni_registerModule(const char *name);
	extern ni_ModuleMethodRef ni_registerModuleMethod(ni_ModuleRef m, const char* name, ModuleFunc func);
	extern void ni_registerModuleConstants(ni_ModuleRef m, ConstantsFunc func);
	extern ni_InterfaceRef ni_registerInterface(ni_ModuleRef m, const char* name);
	extern ni_InterfaceMethodRef ni_registerInterfaceMethod(ni_InterfaceRef iface, const char* name, InterfaceFunc ifunc);
	extern ni_ExceptionRef ni_registerException(ni_ModuleRef m, const char* name, ExceptionBuilder builder);

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
