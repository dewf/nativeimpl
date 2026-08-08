module %%LIBRARY_PREFIX%%.support.nativemethods;

struct ni_Module;
struct ni_ModuleMethod;
struct ni_Interface;
struct ni_InterfaceMethod;
struct ni_Object;
struct ni_Exception;

alias ni_ModuleRef = ni_Module *;
alias ni_ModuleMethodRef = ni_ModuleMethod *;
alias ni_InterfaceRef = ni_Interface *;
alias ni_InterfaceMethodRef = ni_InterfaceMethod *;
alias ni_ObjectRef = ni_Object *;
alias ni_ExceptionRef = ni_Exception *;

enum ni_ClientLogLevel {
	Trace,
	Debug,
	Information,
	Warning,
	Error,
	Critical,
	None
}

alias niClientLogFunc = extern (C) void function(ni_ClientLogLevel level, const (char)* message, int length);
alias niClientFuncExec = extern (C) void function(int id);
alias niClientMethodExec = extern (C) void function(ni_InterfaceMethodRef method, int objID);
alias niClientResourceRelease = extern (C) void function(int id);
alias niClientClearSafetyArea = extern (C) void function();

// void loadMethods(string libraryname) {
//     import core.sys.windows.windows;
//     auto dll = LoadLibraryA((libraryname ~ ".dll").ptr);
//     assert(dll);
//     nativeImplInit = cast(fNativeImplInit) GetProcAddress(dll, "nativeImplInit".ptr);
//     nativeImplShutdown = cast(fNativeImplShutdown) GetProcAddress(dll, "nativeImplShutdown".ptr);
// }

extern(C)
{
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%nativeImplInit")
    int nativeImplInit(niClientLogFunc clientLogFunc,
                       niClientFuncExec clientFuncExec,
                       niClientMethodExec clientMethodExec,
                       niClientResourceRelease clientResourceRelease,
                       niClientClearSafetyArea clientClearSafetyArea);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%nativeImplShutdown")
    void nativeImplShutdown();

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%getModule")
    ni_ModuleRef getModule(const(char)* name);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%getModuleMethod")
    ni_ModuleMethodRef getModuleMethod(ni_ModuleRef m, const(char)* name);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%getInterface")
    ni_InterfaceRef getInterface(ni_ModuleRef m, const(char)* name);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%getInterfaceMethod")
    ni_InterfaceMethodRef getInterfaceMethod(ni_InterfaceRef iface, const(char)* name);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%getException")
    ni_ExceptionRef getException(ni_ModuleRef m, const(char)* name);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushModuleConstants")
    void pushModuleConstants(ni_ModuleRef m);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%invokeModuleMethod")
    void invokeModuleMethod(ni_ModuleMethodRef method);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%invokeModuleMethodWithExceptions")
    ni_ExceptionRef invokeModuleMethodWithExceptions(ni_ModuleMethodRef method);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushPtr")
    void pushPtr(void* value);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popPtr")
    void* popPtr();
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushPtrArray")
    void pushPtrArray(void** values, size_t count);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popPtrArray")
    void popPtrArray(void*** values, size_t* count);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushSizeT")
    void pushSizeT(size_t value);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popSizeT")
    size_t popSizeT();
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushSizeTArray")
    void pushSizeTArray(size_t* values, size_t count);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popSizeTArray")
    void popSizeTArray(size_t** values, size_t* count);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushBool")
    void pushBool(bool value);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popBool")
    bool popBool();
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushBoolArray")
    void pushBoolArray(bool* values, size_t count);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popBoolArray")
    void popBoolArray(bool** values, size_t* count);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushInt8")
    void pushInt8(byte x);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popInt8")
    byte popInt8();
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushInt8Array")
    void pushInt8Array(byte* values, size_t count);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popInt8Array")
    void popInt8Array(byte** values, size_t* count);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushUInt8")
    void pushUInt8(ubyte x);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popUInt8")
    ubyte popUInt8();
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushUInt8Array")
    void pushUInt8Array(ubyte* values, size_t count);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popUInt8Array")
    void popUInt8Array(ubyte** values, size_t* count);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushInt16")
    void pushInt16(short x);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popInt16")
    short popInt16();
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushInt16Array")
    void pushInt16Array(short* values, size_t count);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popInt16Array")
    void popInt16Array(short** values, size_t* count);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushUInt16")
    void pushUInt16(ushort x);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popUInt16")
    ushort popUInt16();
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushUInt16Array")
    void pushUInt16Array(ushort* values, size_t count);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popUInt16Array")
    void popUInt16Array(ushort** values, size_t* count);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushInt32")
    void pushInt32(int x);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popInt32")
    int popInt32();
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushInt32Array")
    void pushInt32Array(int* values, size_t count);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popInt32Array")
    void popInt32Array(int** values, size_t* count);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushUInt32")
    void pushUInt32(uint x);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popUInt32")
    uint popUInt32();
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushUInt32Array")
    void pushUInt32Array(uint* values, size_t count);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popUInt32Array")
    void popUInt32Array(uint** values, size_t* count);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushInt64")
    void pushInt64(long x);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popInt64")
    long popInt64();
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushInt64Array")
    void pushInt64Array(long* values, size_t count);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popInt64Array")
    void popInt64Array(long** values, size_t* count);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushUInt64")
    void pushUInt64(ulong x);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popUInt64")
    ulong popUInt64();
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushUInt64Array")
    void pushUInt64Array(ulong* values, size_t count);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popUInt64Array")
    void popUInt64Array(ulong** values, size_t* count);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushFloat")
    void pushFloat(float x);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popFloat")
    float popFloat();
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushFloatArray")
    void pushFloatArray(float* values, size_t count);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popFloatArray")
    void popFloatArray(float** values, size_t* count);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushDouble")
    void pushDouble(double x);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popDouble")
    double popDouble();
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushDoubleArray")
    void pushDoubleArray(double* values, size_t count);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popDoubleArray")
    void popDoubleArray(double** values, size_t* count);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushString")
    void pushString(const(char)* str, size_t length);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popString")
    void popString(const(char)** strPtr, size_t* length);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushStringArray")
    void pushStringArray(const(char)** strs, size_t* lengths, size_t count);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popStringArray")
    void popStringArray(const(char)*** strs, size_t** lengths, size_t* count);

	struct ni_BufferDescriptor {
		// TODO: provisions for multi-dimensional, indirect pointers, instead of contiguous, stride, etc?
		void* start;
		int elementSize;
		size_t totalCount;
		size_t totalSize;
	}
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushBuffer")
    void pushBuffer(int id, bool isClientId, ni_BufferDescriptor* descriptor);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popBuffer")
    void popBuffer(int* id, bool* isClientId, ni_BufferDescriptor* descriptor);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushClientFunc")
    void pushClientFunc(int id);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popServerFunc")
    int popServerFunc();

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%execServerFunc")
    void execServerFunc(int id);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%execServerFuncWithExceptions")
    ni_ExceptionRef execServerFuncWithExceptions(int id);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%invokeInterfaceMethod")
    void invokeInterfaceMethod(ni_InterfaceMethodRef method, int serverID);
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%invokeInterfaceMethodWithExceptions")
    ni_ExceptionRef invokeInterfaceMethodWithExceptions(
        ni_InterfaceMethodRef method,
        int serverID
    );

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushInstance")
    void pushInstance(int id, bool isClientId);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%popInstance")
    int popInstance(bool* isClientId);

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%pushNull")
    void pushNull();

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%releaseServerResource")
    void releaseServerResource(int id);

    // Clear server resources from safety area (protects values being
    // returned with no other shared_ptrs existing).
    pragma(mangle, "%%DLL_IMPORT_PREFIX%%clearServerSafetyArea")
    void clearServerSafetyArea();

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%dumpTables")
    void dumpTables();

    pragma(mangle, "%%DLL_IMPORT_PREFIX%%setException")
    void setException(ni_ExceptionRef e);
}
