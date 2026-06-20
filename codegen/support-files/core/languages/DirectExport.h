#pragma once

#include "core/NativeImplCore.h"
#include <cstdint>

#ifdef _WIN32
#define DLLPROJECT_EXPORT __declspec(dllexport)
#else
#define DLLPROJECT_EXPORT __attribute__ ((visibility ("default")))
#endif

extern "C" {
    DLLPROJECT_EXPORT int nativeImplInit(
		niClientLogFunc clientLogFunc,
		niClientFuncExec clientFuncExec,
		niClientMethodExec clientMethodExec,
		niClientResourceRelease clientResourceRelease,
		niClientClearSafetyArea clientClearSafetyArea
	);
	DLLPROJECT_EXPORT void nativeImplShutdown();

	DLLPROJECT_EXPORT ni_ModuleRef getModule(const char* name);
	DLLPROJECT_EXPORT ni_ModuleMethodRef getModuleMethod(ni_ModuleRef m, const char* name);
	DLLPROJECT_EXPORT ni_InterfaceRef getInterface(ni_ModuleRef m, const char* name);
	DLLPROJECT_EXPORT ni_InterfaceMethodRef getInterfaceMethod(ni_InterfaceRef iface, const char* name);
	DLLPROJECT_EXPORT ni_ExceptionRef getException(ni_ModuleRef m, const char* name);
	DLLPROJECT_EXPORT void pushModuleConstants(ni_ModuleRef m);

	DLLPROJECT_EXPORT void invokeModuleMethod(ni_ModuleMethodRef method);
	DLLPROJECT_EXPORT ni_ExceptionRef invokeModuleMethodWithExceptions(ni_ModuleMethodRef method);

	DLLPROJECT_EXPORT void pushPtr(void* value);
	DLLPROJECT_EXPORT void* popPtr();
	DLLPROJECT_EXPORT void pushPtrArray(void** values, size_t count);
	DLLPROJECT_EXPORT void popPtrArray(void*** values, size_t* count);

	DLLPROJECT_EXPORT void pushSizeT(size_t value);
	DLLPROJECT_EXPORT size_t popSizeT();
	DLLPROJECT_EXPORT void pushSizeTArray(size_t* values, size_t count);
	DLLPROJECT_EXPORT void popSizeTArray(size_t** values, size_t* count);

	DLLPROJECT_EXPORT void pushBool(bool value);
	DLLPROJECT_EXPORT bool popBool();
	DLLPROJECT_EXPORT void pushBoolArray(bool* values, size_t count);
	DLLPROJECT_EXPORT void popBoolArray(bool** values, size_t* count);

	DLLPROJECT_EXPORT void pushInt8(int8_t x);
	DLLPROJECT_EXPORT int8_t popInt8();
	DLLPROJECT_EXPORT void pushInt8Array(int8_t* values, size_t count);
	DLLPROJECT_EXPORT void popInt8Array(int8_t** values, size_t* count);

	DLLPROJECT_EXPORT void pushUInt8(uint8_t x);
	DLLPROJECT_EXPORT uint8_t popUInt8();
	DLLPROJECT_EXPORT void pushUInt8Array(uint8_t* values, size_t count);
	DLLPROJECT_EXPORT void popUInt8Array(uint8_t** values, size_t* count);

	DLLPROJECT_EXPORT void pushInt16(int16_t x);
	DLLPROJECT_EXPORT int16_t popInt16();
	DLLPROJECT_EXPORT void pushInt16Array(int16_t* values, size_t count);
	DLLPROJECT_EXPORT void popInt16Array(int16_t** values, size_t* count);

	DLLPROJECT_EXPORT void pushUInt16(uint16_t x);
	DLLPROJECT_EXPORT uint16_t popUInt16();
	DLLPROJECT_EXPORT void pushUInt16Array(uint16_t* values, size_t count);
	DLLPROJECT_EXPORT void popUInt16Array(uint16_t** values, size_t* count);

	DLLPROJECT_EXPORT void pushInt32(int32_t x);
	DLLPROJECT_EXPORT int32_t popInt32();
	DLLPROJECT_EXPORT void pushInt32Array(int32_t* values, size_t count);
	DLLPROJECT_EXPORT void popInt32Array(int32_t** values, size_t* count);

	DLLPROJECT_EXPORT void pushUInt32(uint32_t x);
	DLLPROJECT_EXPORT uint32_t popUInt32();
	DLLPROJECT_EXPORT void pushUInt32Array(uint32_t* values, size_t count);
	DLLPROJECT_EXPORT void popUInt32Array(uint32_t** values, size_t* count);

	DLLPROJECT_EXPORT void pushInt64(int64_t x);
	DLLPROJECT_EXPORT int64_t popInt64();
	DLLPROJECT_EXPORT void pushInt64Array(int64_t* values, size_t count);
	DLLPROJECT_EXPORT void popInt64Array(int64_t** values, size_t* count);

	DLLPROJECT_EXPORT void pushUInt64(uint64_t x);
	DLLPROJECT_EXPORT uint64_t popUInt64();
	DLLPROJECT_EXPORT void pushUInt64Array(uint64_t* values, size_t count);
	DLLPROJECT_EXPORT void popUInt64Array(uint64_t** values, size_t* count);

	DLLPROJECT_EXPORT void pushFloat(float x);
	DLLPROJECT_EXPORT float popFloat();
	DLLPROJECT_EXPORT void pushFloatArray(float* values, size_t count);
	DLLPROJECT_EXPORT void popFloatArray(float** values, size_t* count);

	DLLPROJECT_EXPORT void pushDouble(double x);
	DLLPROJECT_EXPORT double popDouble();
	DLLPROJECT_EXPORT void pushDoubleArray(double* values, size_t count);
	DLLPROJECT_EXPORT void popDoubleArray(double** values, size_t* count);

	DLLPROJECT_EXPORT void pushString(const char* str, size_t length);
	DLLPROJECT_EXPORT void popString(const char** strPtr, size_t* length);
	DLLPROJECT_EXPORT void pushStringArray(const char** strs, size_t* lengths, size_t count);
	DLLPROJECT_EXPORT void popStringArray(const char*** strs, size_t** lengths, size_t* count);

	DLLPROJECT_EXPORT void pushBuffer(int id, bool isClientId, ni_BufferDescriptor* descriptor);
	DLLPROJECT_EXPORT void popBuffer(int* id, bool* isClientId, ni_BufferDescriptor* descriptor);

	DLLPROJECT_EXPORT void pushClientFunc(int id);
	DLLPROJECT_EXPORT int popServerFunc();

	DLLPROJECT_EXPORT void execServerFunc(int id);
	DLLPROJECT_EXPORT ni_ExceptionRef execServerFuncWithExceptions(int id);

	DLLPROJECT_EXPORT void invokeInterfaceMethod(ni_InterfaceMethodRef method, int serverID);
	DLLPROJECT_EXPORT ni_ExceptionRef invokeInterfaceMethodWithExceptions(ni_InterfaceMethodRef method, int serverID);

	DLLPROJECT_EXPORT void pushInstance(int id, bool isClientId);
	DLLPROJECT_EXPORT int popInstance(bool* isClientId);

	DLLPROJECT_EXPORT void pushNull();

	DLLPROJECT_EXPORT void releaseServerResource(int id);

	DLLPROJECT_EXPORT void clearServerSafetyArea(); // clear server resources from safety area (protects values being returned with no other shared_ptrs existing)

	DLLPROJECT_EXPORT void dumpTables();

	DLLPROJECT_EXPORT void setException(ni_ExceptionRef e);
}
