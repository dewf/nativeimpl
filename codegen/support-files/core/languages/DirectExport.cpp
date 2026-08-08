#include "DirectExport.h"

#include "core/NativeImplCore.h"

int %%DLL_EXPORT_PREFIX%%nativeImplInit(
    niClientLogFunc clientLogFunc,
    niClientFuncExec clientFuncExec,
    niClientMethodExec clientMethodExec,
    niClientResourceRelease clientResourceRelease,
    niClientClearSafetyArea clientClearSafetyArea)
{
    return ni_nativeImplInit(
        clientLogFunc,
        clientFuncExec,
        clientMethodExec,
        clientResourceRelease,
        clientClearSafetyArea);
}

void %%DLL_EXPORT_PREFIX%%nativeImplShutdown()
{
    return ni_nativeImplShutdown();
}

ni_ModuleRef %%DLL_EXPORT_PREFIX%%getModule(const char* name)
{
    return ni_getModule(name);
}

ni_ModuleMethodRef %%DLL_EXPORT_PREFIX%%getModuleMethod(ni_ModuleRef m, const char* name)
{
    return ni_getModuleMethod(m, name);
}

ni_InterfaceRef %%DLL_EXPORT_PREFIX%%getInterface(ni_ModuleRef m, const char* name)
{
    return ni_getInterface(m, name);
}

ni_InterfaceMethodRef %%DLL_EXPORT_PREFIX%%getInterfaceMethod(ni_InterfaceRef iface, const char* name)
{
    return ni_getInterfaceMethod(iface, name);
}

ni_ExceptionRef %%DLL_EXPORT_PREFIX%%getException(ni_ModuleRef m, const char* name)
{
    return ni_getException(m, name);
}

void %%DLL_EXPORT_PREFIX%%pushModuleConstants(ni_ModuleRef m)
{
    ni_pushModuleConstants(m);
}

void %%DLL_EXPORT_PREFIX%%invokeModuleMethod(ni_ModuleMethodRef method)
{
    ni_invokeModuleMethod(method);
}

ni_ExceptionRef %%DLL_EXPORT_PREFIX%%invokeModuleMethodWithExceptions(ni_ModuleMethodRef method)
{
    return ni_invokeModuleMethodWithExceptions(method);
}

void %%DLL_EXPORT_PREFIX%%pushPtr(void* value)
{
    ni_pushPtr(value);
}

void* %%DLL_EXPORT_PREFIX%%popPtr()
{
    return ni_popPtr();
}

void %%DLL_EXPORT_PREFIX%%pushPtrArray(void** values, size_t count)
{
    ni_pushPtrArray(values, count);
}

void %%DLL_EXPORT_PREFIX%%popPtrArray(void*** values, size_t* count)
{
    ni_popPtrArray(values, count);
}

void %%DLL_EXPORT_PREFIX%%pushSizeT(size_t value)
{
    ni_pushSizeT(value);
}

size_t %%DLL_EXPORT_PREFIX%%popSizeT()
{
    return ni_popSizeT();
}

void %%DLL_EXPORT_PREFIX%%pushSizeTArray(size_t* values, size_t count)
{
    ni_pushSizeTArray(values, count);
}

void %%DLL_EXPORT_PREFIX%%popSizeTArray(size_t** values, size_t* count)
{
    ni_popSizeTArray(values, count);
}

void %%DLL_EXPORT_PREFIX%%pushBool(bool value)
{
    ni_pushBool(value);
}

bool %%DLL_EXPORT_PREFIX%%popBool()
{
    return ni_popBool();
}

void %%DLL_EXPORT_PREFIX%%pushBoolArray(bool* values, size_t count)
{
    ni_pushBoolArray(values, count);
}

void %%DLL_EXPORT_PREFIX%%popBoolArray(bool** values, size_t* count)
{
    ni_popBoolArray(values, count);
}

void %%DLL_EXPORT_PREFIX%%pushInt8(int8_t x)
{
    ni_pushInt8(x);
}

int8_t %%DLL_EXPORT_PREFIX%%popInt8()
{
    return ni_popInt8();
}

void %%DLL_EXPORT_PREFIX%%pushInt8Array(int8_t* values, size_t count)
{
    ni_pushInt8Array(values, count);
}

void %%DLL_EXPORT_PREFIX%%popInt8Array(int8_t** values, size_t* count)
{
    ni_popInt8Array(values, count);
}

void %%DLL_EXPORT_PREFIX%%pushUInt8(uint8_t x)
{
    ni_pushUInt8(x);
}

uint8_t %%DLL_EXPORT_PREFIX%%popUInt8()
{
    return ni_popUInt8();
}

void %%DLL_EXPORT_PREFIX%%pushUInt8Array(uint8_t* values, size_t count)
{
    ni_pushUInt8Array(values, count);
}

void %%DLL_EXPORT_PREFIX%%popUInt8Array(uint8_t** values, size_t* count)
{
    ni_popUInt8Array(values, count);
}

void %%DLL_EXPORT_PREFIX%%pushInt16(int16_t x)
{
    ni_pushInt16(x);
}

int16_t %%DLL_EXPORT_PREFIX%%popInt16()
{
    return ni_popInt16();
}

void %%DLL_EXPORT_PREFIX%%pushInt16Array(int16_t* values, size_t count)
{
    ni_pushInt16Array(values, count);
}

void %%DLL_EXPORT_PREFIX%%popInt16Array(int16_t** values, size_t* count)
{
    ni_popInt16Array(values, count);
}

void %%DLL_EXPORT_PREFIX%%pushUInt16(uint16_t x)
{
    ni_pushUInt16(x);
}

uint16_t %%DLL_EXPORT_PREFIX%%popUInt16()
{
    return ni_popUInt16();
}

void %%DLL_EXPORT_PREFIX%%pushUInt16Array(uint16_t* values, size_t count)
{
    ni_pushUInt16Array(values, count);
}

void %%DLL_EXPORT_PREFIX%%popUInt16Array(uint16_t** values, size_t* count)
{
    ni_popUInt16Array(values, count);
}

void %%DLL_EXPORT_PREFIX%%pushInt32(int32_t x)
{
    ni_pushInt32(x);
}

int32_t %%DLL_EXPORT_PREFIX%%popInt32()
{
    return ni_popInt32();
}

void %%DLL_EXPORT_PREFIX%%pushInt32Array(int32_t* values, size_t count)
{
    ni_pushInt32Array(values, count);
}

void %%DLL_EXPORT_PREFIX%%popInt32Array(int32_t** values, size_t* count)
{
    ni_popInt32Array(values, count);
}

void %%DLL_EXPORT_PREFIX%%pushUInt32(uint32_t x)
{
    ni_pushUInt32(x);
}

uint32_t %%DLL_EXPORT_PREFIX%%popUInt32()
{
    return ni_popUInt32();
}

void %%DLL_EXPORT_PREFIX%%pushUInt32Array(uint32_t* values, size_t count)
{
    ni_pushUInt32Array(values, count);
}

void %%DLL_EXPORT_PREFIX%%popUInt32Array(uint32_t** values, size_t* count)
{
    ni_popUInt32Array(values, count);
}

void %%DLL_EXPORT_PREFIX%%pushInt64(int64_t x)
{
    ni_pushInt64(x);
}

int64_t %%DLL_EXPORT_PREFIX%%popInt64()
{
    return ni_popInt64();
}

void %%DLL_EXPORT_PREFIX%%pushInt64Array(int64_t* values, size_t count)
{
    ni_pushInt64Array(values, count);
}

void %%DLL_EXPORT_PREFIX%%popInt64Array(int64_t** values, size_t* count)
{
    ni_popInt64Array(values, count);
}

void %%DLL_EXPORT_PREFIX%%pushUInt64(uint64_t x)
{
    ni_pushUInt64(x);
}

uint64_t %%DLL_EXPORT_PREFIX%%popUInt64()
{
    return ni_popUInt64();
}

void %%DLL_EXPORT_PREFIX%%pushUInt64Array(uint64_t* values, size_t count)
{
    ni_pushUInt64Array(values, count);
}

void %%DLL_EXPORT_PREFIX%%popUInt64Array(uint64_t** values, size_t* count)
{
    ni_popUInt64Array(values, count);
}

void %%DLL_EXPORT_PREFIX%%pushFloat(float x)
{
    ni_pushFloat(x);
}

float %%DLL_EXPORT_PREFIX%%popFloat()
{
    return ni_popFloat();
}

void %%DLL_EXPORT_PREFIX%%pushFloatArray(float* values, size_t count)
{
    ni_pushFloatArray(values, count);
}

void %%DLL_EXPORT_PREFIX%%popFloatArray(float** values, size_t* count)
{
    ni_popFloatArray(values, count);
}

void %%DLL_EXPORT_PREFIX%%pushDouble(double x)
{
    ni_pushDouble(x);
}

double %%DLL_EXPORT_PREFIX%%popDouble()
{
    return ni_popDouble();
}

void %%DLL_EXPORT_PREFIX%%pushDoubleArray(double* values, size_t count)
{
    ni_pushDoubleArray(values, count);
}

void %%DLL_EXPORT_PREFIX%%popDoubleArray(double** values, size_t* count)
{
    ni_popDoubleArray(values, count);
}

void %%DLL_EXPORT_PREFIX%%pushString(const char* str, size_t length) {
    ni_pushString(str, length);
}

void %%DLL_EXPORT_PREFIX%%popString(const char** strPtr, size_t* length) {
    ni_popString(strPtr, length);
}

void %%DLL_EXPORT_PREFIX%%pushStringArray(const char** strs, size_t* lengths, size_t count)
{
    ni_pushStringArray(strs, lengths, count);
}

void %%DLL_EXPORT_PREFIX%%popStringArray(const char*** strs, size_t** lengths, size_t* count)
{
    ni_popStringArray(strs, lengths, count);
}

void %%DLL_EXPORT_PREFIX%%pushBuffer(int id, bool isClientId, ni_BufferDescriptor* descriptor)
{
    ni_pushBuffer(id, isClientId, descriptor);
}

void %%DLL_EXPORT_PREFIX%%popBuffer(int* id, bool* isClientId, ni_BufferDescriptor* descriptor)
{
    ni_popBuffer(id, isClientId, descriptor);
}

void %%DLL_EXPORT_PREFIX%%pushClientFunc(int id)
{
    ni_pushClientFunc(id);
}

int %%DLL_EXPORT_PREFIX%%popServerFunc()
{
    return ni_popServerFunc();
}

void %%DLL_EXPORT_PREFIX%%execServerFunc(int id)
{
    ni_execServerFunc(id);
}

ni_ExceptionRef %%DLL_EXPORT_PREFIX%%execServerFuncWithExceptions(int id)
{
    return ni_execServerFuncWithExceptions(id);
}

void %%DLL_EXPORT_PREFIX%%invokeInterfaceMethod(ni_InterfaceMethodRef method, int serverID)
{
    ni_invokeInterfaceMethod(method, serverID);
}

ni_ExceptionRef %%DLL_EXPORT_PREFIX%%invokeInterfaceMethodWithExceptions(ni_InterfaceMethodRef method, int serverID)
{
    return ni_invokeInterfaceMethodWithExceptions(method, serverID);
}

void %%DLL_EXPORT_PREFIX%%pushInstance(int id, bool isClientId)
{
    ni_pushInstance(id, isClientId);
}

int %%DLL_EXPORT_PREFIX%%popInstance(bool* isClientID)
{
    return ni_popInstance(isClientID);
}

void %%DLL_EXPORT_PREFIX%%pushNull()
{
    ni_pushNull();
}

void %%DLL_EXPORT_PREFIX%%releaseServerResource(int id)
{
    ni_releaseServerResource(id);
}

void %%DLL_EXPORT_PREFIX%%clearServerSafetyArea()
{
    ni_clearServerSafetyArea();
}

void %%DLL_EXPORT_PREFIX%%dumpTables()
{
    ni_dumpTables();
}

void %%DLL_EXPORT_PREFIX%%setException(ni_ExceptionRef e)
{
    ni_setException(e);
}
