using System.Runtime.InteropServices;

namespace %%TARGET_NAMESPACE%%.Support
{
    internal static partial class NativeMethods
    {
        internal enum ClientLogLevel {
            Trace,
            Debug,
            Information,
            Warning,
            Error,
            Critical,
            None
        }
        internal delegate void ClientLogFuncDelegate(ClientLogLevel level, IntPtr strData, int length);
        internal delegate void ExecFunctionDelegate(int id);
        internal delegate void ClientMethodDelegate(IntPtr methodHandle, int objectId);
        internal delegate void ClientResourceReleaseDelegate(int id);
        internal delegate void ClientClearSafetyAreaDelegate();

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%nativeImplInit")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial int nativeImplInit(
            ClientLogFuncDelegate logFuncDelegate,
            ExecFunctionDelegate execFunctionDelegate,
            ClientMethodDelegate methodDelegate,
            ClientResourceReleaseDelegate resourceReleaseDelegate,
            ClientClearSafetyAreaDelegate clientClearSafetyAreaDelegate
            );

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%nativeImplShutdown")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void nativeImplShutdown();

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%getModule", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial IntPtr getModule(string name);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%getModuleMethod", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial IntPtr getModuleMethod(IntPtr module, string name);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%getInterface", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial IntPtr getInterface(IntPtr module, string name);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%getInterfaceMethod", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial IntPtr getInterfaceMethod(IntPtr iface, string name);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%getException", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial IntPtr getException(IntPtr module, string name);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%invokeModuleMethod")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void invokeModuleMethod(IntPtr method);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%invokeModuleMethodWithExceptions")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial IntPtr invokeModuleMethodWithExceptions(IntPtr method);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushPtr")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushPtr(IntPtr value);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popPtr")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial IntPtr popPtr();

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushPtrArray")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushPtrArray(IntPtr[] values, IntPtr count);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popPtrArray")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void popPtrArray(out IntPtr valuesPtr, out IntPtr count); // void***, size_t*

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushSizeT")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushSizeT(IntPtr value);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popSizeT")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial IntPtr popSizeT();

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushSizeTArray")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushSizeTArray(IntPtr[] values, IntPtr count);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popSizeTArray")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void popSizeTArray(out IntPtr valuesPtr, out IntPtr count); // size_t**, size_t*

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushBool")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushBool([MarshalAs(UnmanagedType.U1)] bool value);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popBool")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static partial bool popBool();

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushBoolArray")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushBoolArray(byte[] values, IntPtr count); // bool*, size_t

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popBoolArray")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void popBoolArray(out IntPtr valuesPtr, out IntPtr count); // bool**, size_t*

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushInt8")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushInt8(sbyte x);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popInt8")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial sbyte popInt8();

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushInt8Array")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushInt8Array(sbyte[] values, IntPtr count); // size_t count

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popInt8Array")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void popInt8Array(out IntPtr values, out IntPtr count); // both args are pointers

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushUInt8")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushUInt8(byte x);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popUInt8")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial byte popUInt8();

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushUInt8Array")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushUInt8Array(byte[] values, IntPtr count); // size_t count

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popUInt8Array")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void popUInt8Array(out IntPtr values, out IntPtr count); // both args are pointers

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushInt16")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushInt16(short x);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popInt16")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial short popInt16();

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushInt16Array")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushInt16Array(short[] values, IntPtr count); // size_t count

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popInt16Array")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void popInt16Array(out IntPtr values, out IntPtr count); // both args are pointers

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushUInt16")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushUInt16(ushort x);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popUInt16")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial ushort popUInt16();

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushUInt16Array")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushUInt16Array(ushort[] values, IntPtr count); // size_t count

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popUInt16Array")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void popUInt16Array(out IntPtr values, out IntPtr count); // both args are pointers

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushInt32")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushInt32(int x);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popInt32")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial int popInt32();

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushInt32Array")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushInt32Array(int[] values, IntPtr count); // size_t count

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popInt32Array")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void popInt32Array(out IntPtr values, out IntPtr count); // both args are pointers

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushUInt32")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushUInt32(uint x);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popUInt32")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial uint popUInt32();

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushUInt32Array")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushUInt32Array(uint[] values, IntPtr count); // size_t count

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popUInt32Array")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void popUInt32Array(out IntPtr values, out IntPtr count); // both args are pointers

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushInt64")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushInt64(long x);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popInt64")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial long popInt64();

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushInt64Array")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushInt64Array(long[] values, IntPtr count); // size_t count

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popInt64Array")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void popInt64Array(out IntPtr values, out IntPtr count); // both args are pointers

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushUInt64")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushUInt64(ulong x);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popUInt64")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial ulong popUInt64();

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushUInt64Array")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushUInt64Array(ulong[] values, IntPtr count); // size_t count

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popUInt64Array")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void popUInt64Array(out IntPtr values, out IntPtr count); // both args are pointers

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushFloat")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushFloat(float x);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popFloat")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial float popFloat();

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushFloatArray")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushFloatArray(float[] values, IntPtr count);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popFloatArray")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void popFloatArray(out IntPtr values, out IntPtr count);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushDouble")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushDouble(double x);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popDouble")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial double popDouble();

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushDoubleArray")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushDoubleArray(double[] values, IntPtr count);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popDoubleArray")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void popDoubleArray(out IntPtr values, out IntPtr count);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushString")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushString(IntPtr str, IntPtr length); // size_t length

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popString")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void popString(out IntPtr strPtr, out IntPtr length); // size_t* length

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushStringArray")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushStringArray(IntPtr[] strs, IntPtr[] lengths, IntPtr count); // const char **, size_t*, size_t

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popStringArray")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void popStringArray(out IntPtr strsPtr, out IntPtr lengthsPtr, out IntPtr count); // const char ***, size_t**, size_t*

        public struct BufferDescriptor
        {
            public IntPtr Start;
            public int ElementSize;
            public IntPtr TotalCount;
            public IntPtr TotalSize;
        }

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushBuffer")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushBuffer(int id, [MarshalAs(UnmanagedType.U1)] bool isClientId, ref BufferDescriptor descriptor);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popBuffer")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void popBuffer(out int id, [MarshalAs(UnmanagedType.U1)] out bool isClientId, out BufferDescriptor descriptor);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushClientFunc")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushClientFunc(int id);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popServerFunc")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial int popServerFunc();

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%execServerFunc")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void execServerFunc(int id);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%execServerFuncWithExceptions")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial IntPtr execServerFuncWithExceptions(int id);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%invokeInterfaceMethod")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void invokeInterfaceMethod(IntPtr method, int serverId);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%invokeInterfaceMethodWithExceptions")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial IntPtr invokeInterfaceMethodWithExceptions(IntPtr method, int serverId);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushInstance")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushInstance(int id, [MarshalAs(UnmanagedType.U1)] bool isClientId);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%popInstance")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial int popInstance([MarshalAs(UnmanagedType.U1)] out bool isClientId);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushNull")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushNull();

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%releaseServerResource")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial int releaseServerResource(int id);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%clearServerSafetyArea")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void clearServerSafetyArea();

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%dumpTables")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void dumpTables();

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%pushModuleConstants")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pushModuleConstants(IntPtr module);

        [LibraryImport("%%SERVER_DLL_NAME%%", EntryPoint = "%%DLL_EXPORT_PREFIX%%setException")]
        [UnmanagedCallConv(CallConvs = new [] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void setException(IntPtr exceptionHandle);
    }
}
