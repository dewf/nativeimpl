#include "TestLibrary.h"

#include "../../../../core/NativeImplCore.h"

int nativeImplInit(niClientFuncExec clientFuncExec, niClientFuncRelease clientFuncRelease, niClientMethodExec clientMethodExec, niClientObjRelease clientObjRelease, niClientClearSafetyArea clientClearSafetyArea)
{
    return ni_nativeImplInit(clientFuncExec, clientFuncRelease, clientMethodExec, clientObjRelease, clientClearSafetyArea);
}

void nativeImplShutdown()
{
    return ni_nativeImplShutdown();
}

ni_ModuleRef getModule(const char* name)
{
    return ni_getModule(name);
}

ni_ModuleMethodRef getModuleMethod(ni_ModuleRef m, const char* name)
{
    return ni_getModuleMethod(m, name);
}

ni_InterfaceRef getInterface(ni_ModuleRef m, const char* name)
{
    return ni_getInterface(m, name);
}

ni_InterfaceMethodRef getInterfaceMethod(ni_InterfaceRef iface, const char* name)
{
    return ni_getInterfaceMethod(iface, name);
}

ni_ExceptionRef getException(ni_ModuleRef m, const char* name)
{
    return ni_getException(m, name);
}

void pushModuleConstants(ni_ModuleRef m)
{
    ni_pushModuleConstants(m);
}

void invokeModuleMethod(ni_ModuleMethodRef method)
{
    ni_invokeModuleMethod(method);
}

ni_ExceptionRef invokeModuleMethodWithExceptions(ni_ModuleMethodRef method)
{
    return ni_invokeModuleMethodWithExceptions(method);
}

void pushBool(bool value)
{
    ni_pushBool(value);
}

bool popBool()
{
    return ni_popBool();
}

void pushBoolArray(bool* values, size_t count)
{
    ni_pushBoolArray(values, count);
}

void popBoolArray(bool** values, size_t* count)
{
    ni_popBoolArray(values, count);
}

void pushInt(int x)
{
    ni_pushInt(x);
}

int popInt()
{
    return ni_popInt();
}

void pushIntArray(int* values, size_t count)
{
    ni_pushIntArray(values, count);
}

void popIntArray(int** values, size_t* count)
{
    ni_popIntArray(values, count);
}

void pushString(const char* str, size_t length) {
    ni_pushString(str, length);
}

void popString(const char** strPtr, size_t* length) {
    ni_popString(strPtr, length);
}

void pushStringArray(const char** strs, size_t* lengths, size_t count)
{
    ni_pushStringArray(strs, lengths, count);
}

void popStringArray(const char*** strs, size_t** lengths, size_t* count)
{
    ni_popStringArray(strs, lengths, count);
}

void pushClientFunc(int id)
{
    ni_pushClientFunc(id);
}

int popServerFunc()
{
    return ni_popServerFunc();
}

void execServerFunc(int id)
{
    ni_execServerFunc(id);
}

void releaseServerFunc(int id)
{
    ni_releaseServerFunc(id);
}

void invokeInterfaceMethod(ni_InterfaceMethodRef method, int serverID)
{
    ni_invokeInterfaceMethod(method, serverID);
}

ni_ExceptionRef invokeInterfaceMethodWithExceptions(ni_InterfaceMethodRef method, int serverID)
{
    return ni_invokeInterfaceMethodWithExceptions(method, serverID);
}

void pushClientInst(int id)
{
    ni_pushClientInst(id);
}

void pushServerInst(int id)
{
    ni_pushServerInst(id);
}

int popInstance(bool* isClientID)
{
    return ni_popInstance(isClientID);
}

void releaseServerInst(int id)
{
    ni_releaseServerInst(id);
}

void clearServerSafetyArea()
{
    ni_clearServerSafetyArea();
}

void dumpTables()
{
    ni_dumpTables();
}

void setException(ni_ExceptionRef e)
{
    ni_setException(e);
}
