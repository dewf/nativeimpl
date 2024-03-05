#include "core/NativeImplCore.h"
#include <cstdint>

#ifdef KITCHENSINKSERVER_EXPORTS
#define KITCHENSINKSERVER_API __declspec(dllexport)
#else
#define KITCHENSINKSERVER_API __declspec(dllimport)
#endif

extern "C" {
	KITCHENSINKSERVER_API int nativeImplInit(
		niClientFuncExec clientFuncExec,
		niClientMethodExec clientMethodExec,
		niClientResourceRelease clientResourceRelease,
		niClientClearSafetyArea clientClearSafetyArea
	);
	KITCHENSINKSERVER_API void nativeImplShutdown();

	KITCHENSINKSERVER_API ni_ModuleRef getModule(const char* name);
	KITCHENSINKSERVER_API ni_ModuleMethodRef getModuleMethod(ni_ModuleRef m, const char* name);
	KITCHENSINKSERVER_API ni_InterfaceRef getInterface(ni_ModuleRef m, const char* name);
	KITCHENSINKSERVER_API ni_InterfaceMethodRef getInterfaceMethod(ni_InterfaceRef iface, const char* name);
	KITCHENSINKSERVER_API ni_ExceptionRef getException(ni_ModuleRef m, const char* name);
	KITCHENSINKSERVER_API void pushModuleConstants(ni_ModuleRef m);

	KITCHENSINKSERVER_API void invokeModuleMethod(ni_ModuleMethodRef method);
	KITCHENSINKSERVER_API ni_ExceptionRef invokeModuleMethodWithExceptions(ni_ModuleMethodRef method);

	KITCHENSINKSERVER_API void pushPtr(void* value);
	KITCHENSINKSERVER_API void* popPtr();
	KITCHENSINKSERVER_API void pushPtrArray(void** values, size_t count);
	KITCHENSINKSERVER_API void popPtrArray(void*** values, size_t* count);

	KITCHENSINKSERVER_API void pushSizeT(size_t value);
	KITCHENSINKSERVER_API size_t popSizeT();
	KITCHENSINKSERVER_API void pushSizeTArray(size_t* values, size_t count);
	KITCHENSINKSERVER_API void popSizeTArray(size_t** values, size_t* count);

	KITCHENSINKSERVER_API void pushBool(bool value);
	KITCHENSINKSERVER_API bool popBool();
	KITCHENSINKSERVER_API void pushBoolArray(bool* values, size_t count);
	KITCHENSINKSERVER_API void popBoolArray(bool** values, size_t* count);

	KITCHENSINKSERVER_API void pushInt8(int8_t x);
	KITCHENSINKSERVER_API int8_t popInt8();
	KITCHENSINKSERVER_API void pushInt8Array(int8_t* values, size_t count);
	KITCHENSINKSERVER_API void popInt8Array(int8_t** values, size_t* count);

	KITCHENSINKSERVER_API void pushUInt8(uint8_t x);
	KITCHENSINKSERVER_API uint8_t popUInt8();
	KITCHENSINKSERVER_API void pushUInt8Array(uint8_t* values, size_t count);
	KITCHENSINKSERVER_API void popUInt8Array(uint8_t** values, size_t* count);

	KITCHENSINKSERVER_API void pushInt16(int16_t x);
	KITCHENSINKSERVER_API int16_t popInt16();
	KITCHENSINKSERVER_API void pushInt16Array(int16_t* values, size_t count);
	KITCHENSINKSERVER_API void popInt16Array(int16_t** values, size_t* count);

	KITCHENSINKSERVER_API void pushUInt16(uint16_t x);
	KITCHENSINKSERVER_API uint16_t popUInt16();
	KITCHENSINKSERVER_API void pushUInt16Array(uint16_t* values, size_t count);
	KITCHENSINKSERVER_API void popUInt16Array(uint16_t** values, size_t* count);

	KITCHENSINKSERVER_API void pushInt32(int32_t x);
	KITCHENSINKSERVER_API int32_t popInt32();
	KITCHENSINKSERVER_API void pushInt32Array(int32_t* values, size_t count);
	KITCHENSINKSERVER_API void popInt32Array(int32_t** values, size_t* count);

	KITCHENSINKSERVER_API void pushUInt32(uint32_t x);
	KITCHENSINKSERVER_API uint32_t popUInt32();
	KITCHENSINKSERVER_API void pushUInt32Array(uint32_t* values, size_t count);
	KITCHENSINKSERVER_API void popUInt32Array(uint32_t** values, size_t* count);

	KITCHENSINKSERVER_API void pushInt64(int64_t x);
	KITCHENSINKSERVER_API int64_t popInt64();
	KITCHENSINKSERVER_API void pushInt64Array(int64_t* values, size_t count);
	KITCHENSINKSERVER_API void popInt64Array(int64_t** values, size_t* count);

	KITCHENSINKSERVER_API void pushUInt64(uint64_t x);
	KITCHENSINKSERVER_API uint64_t popUInt64();
	KITCHENSINKSERVER_API void pushUInt64Array(uint64_t* values, size_t count);
	KITCHENSINKSERVER_API void popUInt64Array(uint64_t** values, size_t* count);

	KITCHENSINKSERVER_API void pushFloat(float x);
	KITCHENSINKSERVER_API float popFloat();
	KITCHENSINKSERVER_API void pushFloatArray(float* values, size_t count);
	KITCHENSINKSERVER_API void popFloatArray(float** values, size_t* count);

	KITCHENSINKSERVER_API void pushDouble(double x);
	KITCHENSINKSERVER_API double popDouble();
	KITCHENSINKSERVER_API void pushDoubleArray(double* values, size_t count);
	KITCHENSINKSERVER_API void popDoubleArray(double** values, size_t* count);

	KITCHENSINKSERVER_API void pushString(const char* str, size_t length);
	KITCHENSINKSERVER_API void popString(const char** strPtr, size_t* length);
	KITCHENSINKSERVER_API void pushStringArray(const char** strs, size_t* lengths, size_t count);
	KITCHENSINKSERVER_API void popStringArray(const char*** strs, size_t** lengths, size_t* count);

	KITCHENSINKSERVER_API void pushBuffer(int id, bool isClientId, ni_BufferDescriptor* descriptor);
	KITCHENSINKSERVER_API void popBuffer(int* id, bool* isClientId, ni_BufferDescriptor* descriptor);

	KITCHENSINKSERVER_API void pushClientFunc(int id);
	KITCHENSINKSERVER_API int popServerFunc();

	KITCHENSINKSERVER_API void execServerFunc(int id);
	KITCHENSINKSERVER_API ni_ExceptionRef execServerFuncWithExceptions(int id);

	KITCHENSINKSERVER_API void invokeInterfaceMethod(ni_InterfaceMethodRef method, int serverID);
	KITCHENSINKSERVER_API ni_ExceptionRef invokeInterfaceMethodWithExceptions(ni_InterfaceMethodRef method, int serverID);

	KITCHENSINKSERVER_API void pushInstance(int id, bool isClientId);
	KITCHENSINKSERVER_API int popInstance(bool* isClientId);

	KITCHENSINKSERVER_API void pushNull();

	KITCHENSINKSERVER_API void releaseServerResource(int id);

	KITCHENSINKSERVER_API void clearServerSafetyArea(); // clear server resources from safety area (protects values being returned with no other shared_ptrs existing)

	KITCHENSINKSERVER_API void dumpTables();

	KITCHENSINKSERVER_API void setException(ni_ExceptionRef e);
}
