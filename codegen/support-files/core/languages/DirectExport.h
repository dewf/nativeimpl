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
    DLLPROJECT_API int %%DLL_EXPORT_PREFIX%%nativeImplInit(
		niClientLogFunc clientLogFunc,
		niClientFuncExec clientFuncExec,
		niClientMethodExec clientMethodExec,
		niClientResourceRelease clientResourceRelease,
		niClientClearSafetyArea clientClearSafetyArea
	);
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%nativeImplShutdown();

	DLLPROJECT_API ni_ModuleRef %%DLL_EXPORT_PREFIX%%getModule(const char* name);
	DLLPROJECT_API ni_ModuleMethodRef %%DLL_EXPORT_PREFIX%%getModuleMethod(ni_ModuleRef m, const char* name);
	DLLPROJECT_API ni_InterfaceRef %%DLL_EXPORT_PREFIX%%getInterface(ni_ModuleRef m, const char* name);
	DLLPROJECT_API ni_InterfaceMethodRef %%DLL_EXPORT_PREFIX%%getInterfaceMethod(ni_InterfaceRef iface, const char* name);
	DLLPROJECT_API ni_ExceptionRef %%DLL_EXPORT_PREFIX%%getException(ni_ModuleRef m, const char* name);
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushModuleConstants(ni_ModuleRef m);

	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%invokeModuleMethod(ni_ModuleMethodRef method);
	DLLPROJECT_API ni_ExceptionRef %%DLL_EXPORT_PREFIX%%invokeModuleMethodWithExceptions(ni_ModuleMethodRef method);

	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushPtr(void* value);
	DLLPROJECT_API void* %%DLL_EXPORT_PREFIX%%popPtr();
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushPtrArray(void** values, size_t count);
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%popPtrArray(void*** values, size_t* count);

	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushSizeT(size_t value);
	DLLPROJECT_API size_t %%DLL_EXPORT_PREFIX%%popSizeT();
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushSizeTArray(size_t* values, size_t count);
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%popSizeTArray(size_t** values, size_t* count);

	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushBool(bool value);
	DLLPROJECT_API bool %%DLL_EXPORT_PREFIX%%popBool();
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushBoolArray(bool* values, size_t count);
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%popBoolArray(bool** values, size_t* count);

	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushInt8(int8_t x);
	DLLPROJECT_API int8_t %%DLL_EXPORT_PREFIX%%popInt8();
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushInt8Array(int8_t* values, size_t count);
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%popInt8Array(int8_t** values, size_t* count);

	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushUInt8(uint8_t x);
	DLLPROJECT_API uint8_t %%DLL_EXPORT_PREFIX%%popUInt8();
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushUInt8Array(uint8_t* values, size_t count);
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%popUInt8Array(uint8_t** values, size_t* count);

	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushInt16(int16_t x);
	DLLPROJECT_API int16_t %%DLL_EXPORT_PREFIX%%popInt16();
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushInt16Array(int16_t* values, size_t count);
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%popInt16Array(int16_t** values, size_t* count);

	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushUInt16(uint16_t x);
	DLLPROJECT_API uint16_t %%DLL_EXPORT_PREFIX%%popUInt16();
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushUInt16Array(uint16_t* values, size_t count);
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%popUInt16Array(uint16_t** values, size_t* count);

	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushInt32(int32_t x);
	DLLPROJECT_API int32_t %%DLL_EXPORT_PREFIX%%popInt32();
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushInt32Array(int32_t* values, size_t count);
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%popInt32Array(int32_t** values, size_t* count);

	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushUInt32(uint32_t x);
	DLLPROJECT_API uint32_t %%DLL_EXPORT_PREFIX%%popUInt32();
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushUInt32Array(uint32_t* values, size_t count);
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%popUInt32Array(uint32_t** values, size_t* count);

	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushInt64(int64_t x);
	DLLPROJECT_API int64_t %%DLL_EXPORT_PREFIX%%popInt64();
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushInt64Array(int64_t* values, size_t count);
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%popInt64Array(int64_t** values, size_t* count);

	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushUInt64(uint64_t x);
	DLLPROJECT_API uint64_t %%DLL_EXPORT_PREFIX%%popUInt64();
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushUInt64Array(uint64_t* values, size_t count);
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%popUInt64Array(uint64_t** values, size_t* count);

	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushFloat(float x);
	DLLPROJECT_API float %%DLL_EXPORT_PREFIX%%popFloat();
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushFloatArray(float* values, size_t count);
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%popFloatArray(float** values, size_t* count);

	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushDouble(double x);
	DLLPROJECT_API double %%DLL_EXPORT_PREFIX%%popDouble();
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushDoubleArray(double* values, size_t count);
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%popDoubleArray(double** values, size_t* count);

	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushString(const char* str, size_t length);
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%popString(const char** strPtr, size_t* length);
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushStringArray(const char** strs, size_t* lengths, size_t count);
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%popStringArray(const char*** strs, size_t** lengths, size_t* count);

	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushBuffer(int id, bool isClientId, ni_BufferDescriptor* descriptor);
	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%popBuffer(int* id, bool* isClientId, ni_BufferDescriptor* descriptor);

	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushClientFunc(int id);
	DLLPROJECT_API int %%DLL_EXPORT_PREFIX%%popServerFunc();

	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%execServerFunc(int id);
	DLLPROJECT_API ni_ExceptionRef %%DLL_EXPORT_PREFIX%%execServerFuncWithExceptions(int id);

	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushClientInterface(int id);
	DLLPROJECT_API int %%DLL_EXPORT_PREFIX%%popClientInterface();

	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%pushNull();

	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%releaseServerResource(int id);

	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%clearServerSafetyArea(); // clear server resources from safety area (protects values being returned with no other shared_ptrs existing)

	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%dumpTables();

	DLLPROJECT_API void %%DLL_EXPORT_PREFIX%%setException(ni_ExceptionRef e);
}
