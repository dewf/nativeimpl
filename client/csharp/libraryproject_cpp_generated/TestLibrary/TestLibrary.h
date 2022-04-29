#include "../../../../core/NativeImplCore.h"

#ifdef TESTLIBRARY_EXPORTS
#define TESTLIBRARY_API __declspec(dllexport)
#else
#define TESTLIBRARY_API __declspec(dllimport)
#endif

extern "C" {
	TESTLIBRARY_API int nativeImplInit(
		niClientFuncExec clientFuncExec,
		niClientFuncRelease clientFuncRelease,
		niClientMethodExec clientMethodExec,
		niClientObjRelease clientObjRelease,
		niClientClearSafetyArea clientClearSafetyArea
	);
	TESTLIBRARY_API void nativeImplShutdown();

	TESTLIBRARY_API ni_ModuleRef getModule(const char* name);
	TESTLIBRARY_API ni_ModuleMethodRef getModuleMethod(ni_ModuleRef m, const char* name);
	TESTLIBRARY_API ni_InterfaceRef getInterface(ni_ModuleRef m, const char* name);
	TESTLIBRARY_API ni_InterfaceMethodRef getInterfaceMethod(ni_InterfaceRef iface, const char* name);
	TESTLIBRARY_API ni_ExceptionRef getException(ni_ModuleRef m, const char* name);
	TESTLIBRARY_API void pushModuleConstants(ni_ModuleRef m);

	TESTLIBRARY_API void invokeModuleMethod(ni_ModuleMethodRef method);
	TESTLIBRARY_API ni_ExceptionRef invokeModuleMethodWithExceptions(ni_ModuleMethodRef method);

	TESTLIBRARY_API void pushSizeT(size_t value);
	TESTLIBRARY_API size_t popSizeT();
	TESTLIBRARY_API void pushSizeTArray(size_t* values, size_t count);
	TESTLIBRARY_API void popSizeTArray(size_t** values, size_t* count);

	TESTLIBRARY_API void pushBool(bool value);
	TESTLIBRARY_API bool popBool();
	TESTLIBRARY_API void pushBoolArray(bool* values, size_t count);
	TESTLIBRARY_API void popBoolArray(bool** values, size_t* count);

	TESTLIBRARY_API void pushInt(int x);
	TESTLIBRARY_API int popInt();
	TESTLIBRARY_API void pushIntArray(int* values, size_t count);
	TESTLIBRARY_API void popIntArray(int** values, size_t* count);

	TESTLIBRARY_API void pushString(const char* str, size_t length);
	TESTLIBRARY_API void popString(const char** strPtr, size_t* length);
	TESTLIBRARY_API void pushStringArray(const char** strs, size_t* lengths, size_t count);
	TESTLIBRARY_API void popStringArray(const char*** strs, size_t** lengths, size_t* count);

	TESTLIBRARY_API void pushClientFunc(int id);
	TESTLIBRARY_API int popServerFunc();

	TESTLIBRARY_API void execServerFunc(int id);
	TESTLIBRARY_API ni_ExceptionRef execServerFuncWithExceptions(int id);
	TESTLIBRARY_API void releaseServerFunc(int id);

	TESTLIBRARY_API void invokeInterfaceMethod(ni_InterfaceMethodRef method, int serverID);
	TESTLIBRARY_API ni_ExceptionRef invokeInterfaceMethodWithExceptions(ni_InterfaceMethodRef method, int serverID);

	TESTLIBRARY_API void pushInstance(int id, bool isClientId, bool inSafetyArea);
	TESTLIBRARY_API int popInstance(bool* isClientId, bool* inSafetyArea);
	TESTLIBRARY_API void releaseServerInst(int id);

	TESTLIBRARY_API void clearServerSafetyArea(); // clear server resources from safety area (protects values being returned with no other shared_ptrs existing)

	TESTLIBRARY_API void dumpTables();

	TESTLIBRARY_API void setException(ni_ExceptionRef e);
}
