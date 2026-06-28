#pragma once

#include "core/NativeImplCore.h"
#include <cstdint>

#ifdef _WIN32
#  ifdef DLLPROJECT_EXPORTS
#    define DLLPROJECT_API __declspec(dllexport)
#  else
#    define DLLPROJECT_API __declspec(dllimport)
#  endif
#else
#   define DLLPROJECT_API __attribute__((visibility("default")))
#   define CDECL
#endif

extern "C" {
    DLLPROJECT_API int nativeImplInit(
		niClientLogFunc clientLogFunc,
		niClientFuncExec clientFuncExec,
		niClientMethodExec clientMethodExec,
		niClientResourceRelease clientResourceRelease,
		niClientClearSafetyArea clientClearSafetyArea
	);
	DLLPROJECT_API void nativeImplShutdown();

	DLLPROJECT_API ni_ModuleRef getModule(const char* name);
	DLLPROJECT_API ni_ModuleMethodRef getModuleMethod(ni_ModuleRef m, const char* name);
	DLLPROJECT_API ni_InterfaceRef getInterface(ni_ModuleRef m, const char* name);
	DLLPROJECT_API ni_InterfaceMethodRef getInterfaceMethod(ni_InterfaceRef iface, const char* name);
	DLLPROJECT_API ni_ExceptionRef getException(ni_ModuleRef m, const char* name);
	DLLPROJECT_API void pushModuleConstants(ni_ModuleRef m);

	DLLPROJECT_API void invokeModuleMethod(ni_ModuleMethodRef method);
	DLLPROJECT_API ni_ExceptionRef invokeModuleMethodWithExceptions(ni_ModuleMethodRef method);

	DLLPROJECT_API void pushPtr(void* value);
	DLLPROJECT_API void* popPtr();
	DLLPROJECT_API void pushPtrArray(void** values, size_t count);
	DLLPROJECT_API void popPtrArray(void*** values, size_t* count);

	DLLPROJECT_API void pushSizeT(size_t value);
	DLLPROJECT_API size_t popSizeT();
	DLLPROJECT_API void pushSizeTArray(size_t* values, size_t count);
	DLLPROJECT_API void popSizeTArray(size_t** values, size_t* count);

	DLLPROJECT_API void pushBool(bool value);
	DLLPROJECT_API bool popBool();
	DLLPROJECT_API void pushBoolArray(bool* values, size_t count);
	DLLPROJECT_API void popBoolArray(bool** values, size_t* count);

	DLLPROJECT_API void pushInt8(int8_t x);
	DLLPROJECT_API int8_t popInt8();
	DLLPROJECT_API void pushInt8Array(int8_t* values, size_t count);
	DLLPROJECT_API void popInt8Array(int8_t** values, size_t* count);

	DLLPROJECT_API void pushUInt8(uint8_t x);
	DLLPROJECT_API uint8_t popUInt8();
	DLLPROJECT_API void pushUInt8Array(uint8_t* values, size_t count);
	DLLPROJECT_API void popUInt8Array(uint8_t** values, size_t* count);

	DLLPROJECT_API void pushInt16(int16_t x);
	DLLPROJECT_API int16_t popInt16();
	DLLPROJECT_API void pushInt16Array(int16_t* values, size_t count);
	DLLPROJECT_API void popInt16Array(int16_t** values, size_t* count);

	DLLPROJECT_API void pushUInt16(uint16_t x);
	DLLPROJECT_API uint16_t popUInt16();
	DLLPROJECT_API void pushUInt16Array(uint16_t* values, size_t count);
	DLLPROJECT_API void popUInt16Array(uint16_t** values, size_t* count);

	DLLPROJECT_API void pushInt32(int32_t x);
	DLLPROJECT_API int32_t popInt32();
	DLLPROJECT_API void pushInt32Array(int32_t* values, size_t count);
	DLLPROJECT_API void popInt32Array(int32_t** values, size_t* count);

	DLLPROJECT_API void pushUInt32(uint32_t x);
	DLLPROJECT_API uint32_t popUInt32();
	DLLPROJECT_API void pushUInt32Array(uint32_t* values, size_t count);
	DLLPROJECT_API void popUInt32Array(uint32_t** values, size_t* count);

	DLLPROJECT_API void pushInt64(int64_t x);
	DLLPROJECT_API int64_t popInt64();
	DLLPROJECT_API void pushInt64Array(int64_t* values, size_t count);
	DLLPROJECT_API void popInt64Array(int64_t** values, size_t* count);

	DLLPROJECT_API void pushUInt64(uint64_t x);
	DLLPROJECT_API uint64_t popUInt64();
	DLLPROJECT_API void pushUInt64Array(uint64_t* values, size_t count);
	DLLPROJECT_API void popUInt64Array(uint64_t** values, size_t* count);

	DLLPROJECT_API void pushFloat(float x);
	DLLPROJECT_API float popFloat();
	DLLPROJECT_API void pushFloatArray(float* values, size_t count);
	DLLPROJECT_API void popFloatArray(float** values, size_t* count);

	DLLPROJECT_API void pushDouble(double x);
	DLLPROJECT_API double popDouble();
	DLLPROJECT_API void pushDoubleArray(double* values, size_t count);
	DLLPROJECT_API void popDoubleArray(double** values, size_t* count);

	DLLPROJECT_API void pushString(const char* str, size_t length);
	DLLPROJECT_API void popString(const char** strPtr, size_t* length);
	DLLPROJECT_API void pushStringArray(const char** strs, size_t* lengths, size_t count);
	DLLPROJECT_API void popStringArray(const char*** strs, size_t** lengths, size_t* count);

	DLLPROJECT_API void pushBuffer(int id, bool isClientId, ni_BufferDescriptor* descriptor);
	DLLPROJECT_API void popBuffer(int* id, bool* isClientId, ni_BufferDescriptor* descriptor);

	DLLPROJECT_API void pushClientFunc(int id);
	DLLPROJECT_API int popServerFunc();

	DLLPROJECT_API void execServerFunc(int id);
	DLLPROJECT_API ni_ExceptionRef execServerFuncWithExceptions(int id);

	DLLPROJECT_API void invokeInterfaceMethod(ni_InterfaceMethodRef method, int serverID);
	DLLPROJECT_API ni_ExceptionRef invokeInterfaceMethodWithExceptions(ni_InterfaceMethodRef method, int serverID);

	DLLPROJECT_API void pushInstance(int id, bool isClientId);
	DLLPROJECT_API int popInstance(bool* isClientId);

	DLLPROJECT_API void pushNull();

	DLLPROJECT_API void releaseServerResource(int id);

	DLLPROJECT_API void clearServerSafetyArea(); // clear server resources from safety area (protects values being returned with no other shared_ptrs existing)

	DLLPROJECT_API void dumpTables();

	DLLPROJECT_API void setException(ni_ExceptionRef e);
}
