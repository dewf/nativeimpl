#pragma once

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

// server registration methods (server-facing only, not visible through DLL)
extern "C" {
	typedef void (*ModuleFunc)();
	typedef void (*InterfaceFunc)(int serverID);
	typedef void (*ConstantsFunc)();
	typedef void (*ExceptionBuilder)(); // more of a 'pop and throw' than building, per se

	ni_ModuleRef ni_registerModule(const char *name);
	ni_ModuleMethodRef ni_registerModuleMethod(ni_ModuleRef m, const char *name, ModuleFunc func);
	void ni_registerModuleConstants(ni_ModuleRef m, ConstantsFunc func);
	ni_InterfaceRef ni_registerInterface(ni_ModuleRef m, const char *name);
	ni_InterfaceMethodRef ni_registerInterfaceMethod(ni_InterfaceRef iface, const char *name, InterfaceFunc ifunc);
	ni_ExceptionRef ni_registerException(ni_ModuleRef m, const char *name, ExceptionBuilder builder);
}

// imported back from server (during linking process, against static server library)
extern "C" {
	extern int nativeLibraryInit();		 // must be defined by library itself, to register methods, start runtime, etc
	extern void nativeLibraryShutdown(); // any native cleanup (runtime shutdown etc)
	extern void ni_dumpTables();
	extern void ni_execServerFunc(int id);
	extern ni_ExceptionRef ni_execServerFuncWithExceptions(int id);
	extern void ni_releaseServerFunc(int id);
	extern void ni_releaseServerInst(int id);
}

// visible to both client and server
extern "C" {
	// will there be any problems with allowing direct access to these?
	extern niClientFuncExec ni_clientFuncExec;
	extern niClientFuncRelease ni_clientFuncRelease;
	extern niClientMethodExec ni_clientMethodExec;
	extern niClientObjRelease ni_clientObjRelease;
	extern niClientClearSafetyArea ni_clientClearSafetyArea;
	extern ni_ExceptionRef ni_currentException;

	// mostly client stuff, but some used by server as well
	int ni_nativeImplInit(
		niClientFuncExec clientFuncExec,
		niClientFuncRelease clientFuncRelease,
		niClientMethodExec clientMethodExec,
		niClientObjRelease clientObjRelease,
		niClientClearSafetyArea clientClearSafetyArea
	);
	void ni_nativeImplShutdown();

	ni_ModuleRef ni_getModule(const char* name);
	ni_ModuleMethodRef ni_getModuleMethod(ni_ModuleRef m, const char* name);
	ni_InterfaceRef ni_getInterface(ni_ModuleRef m, const char* name);
	ni_InterfaceMethodRef ni_getInterfaceMethod(ni_InterfaceRef iface, const char* name);
	ni_ExceptionRef ni_getException(ni_ModuleRef m, const char* name);
	void ni_pushModuleConstants(ni_ModuleRef m);

	void ni_invokeModuleMethod(ni_ModuleMethodRef method);
	ni_ExceptionRef ni_invokeModuleMethodWithExceptions(ni_ModuleMethodRef method);

	void ni_pushSizeT(size_t value);
	size_t ni_popSizeT();
	void ni_pushSizeTArray(size_t* values, size_t count);
	void ni_popSizeTArray(size_t** values, size_t* count);

	void ni_pushBool(bool value);
	bool ni_popBool();
	void ni_pushBoolArray(bool* values, size_t count);
	void ni_popBoolArray(bool** values, size_t* count);

	void ni_pushInt(int x);
	int ni_popInt();
	void ni_pushIntArray(int* values, size_t count);
	void ni_popIntArray(int** values, size_t* count);

	void ni_pushString(const char* str, size_t length);     // length is optional
	void ni_popString(const char** strPtr, size_t* length); // result guaranteed until the next popString
	void ni_pushStringArray(const char** strs, size_t* lengths, size_t count);
	void ni_popStringArray(const char*** strs, size_t** lengths, size_t* count);

	void ni_pushClientFunc(int id);
	void ni_pushServerFunc(int id);
	// the reason for two separate pops is that, unlike interfaces, we have no way of "short-circuiting" these
	// (ie, if a client sends a function value to the server, and the server returns it, it's always wrapped anew as a server function)
	// this was simpler at the time than using single-method-interfaces which could carry information about their origin
	// perhaps to be revisited at some point, but the idea behind function values was more for simple lambda type stuff,
	// not meant to make roundtrips between client and server, the way interfaces might
	int ni_popClientFunc();
	int ni_popServerFunc();

	void ni_invokeInterfaceMethod(ni_InterfaceMethodRef method, int serverID);
	ni_ExceptionRef ni_invokeInterfaceMethodWithExceptions(ni_InterfaceMethodRef method, int serverID);

	// TODO: get rid of the safety area flag, it was there so we could only conditionally clear the safety area,
	// but I think all the ifs and checking (conditional setting of 'remotesafetyareadirty' or whatever) will end up
	// being equivalent to simply clearing the remote safety area unconditionally at the end of every invocation returning (something containing) an interface reference
	void ni_pushInstance(int id, bool isClientId, bool inSafetyArea);
	int ni_popInstance(bool* isClientID, bool* inSafetyArea);

	void ni_releaseServerInst(int id);

	// safety area functions: the act of pushing remote interfaces can destroy the local proxy (if no other local proxies exist),
	// prematurely dereferencing the remote side
	// eg, server pushing a client instance (converting to a simple ID on the stack), would instantly destroy the proxy
	//   before the client has a chance to grab it off the stack and save it from a potentially fatal deref.
	// the safety area protects until a return value (containing interface references) is fully grabbed
	void ni_clearClientSafetyArea();
	void ni_clearServerSafetyArea();

	void ni_dumpTables();

	// ni_setException (and the invocation methods returning ni_ExceptionRef) exist because the client is assumed
	// to not have direct memory access to ni_currentException, like the server does.
	void ni_setException(ni_ExceptionRef e);
	void ni_buildAndthrow(ni_ExceptionRef e);
}
