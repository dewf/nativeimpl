using System;
using System.Runtime.InteropServices;

namespace TestLibraryClient.Support
{
    internal static class NativeMethods
    {
        internal delegate void ExecFunctionDelegate(int id);
        internal delegate void ClientMethodDelegate(IntPtr methodHandle, int objectId);
        internal delegate void ClientResourceReleaseDelegate(int id);
        internal delegate void ClientClearSafetyAreaDelegate();

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int nativeImplInit(
            ExecFunctionDelegate execFunctionDelegate,
            ClientMethodDelegate methodDelegate,
            ClientResourceReleaseDelegate resourceReleaseDelegate,
            ClientClearSafetyAreaDelegate clientClearSafetyAreaDelegate
            );

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void nativeImplShutdown();

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr getModule(
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Utf8Marshaler))]
            string name);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr getModuleMethod(
            IntPtr module,
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Utf8Marshaler))]
            string name);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr getInterface(
            IntPtr module,
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Utf8Marshaler))]
            string name);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr getInterfaceMethod(
            IntPtr iface,
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Utf8Marshaler))]
            string name);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr getException(
            IntPtr module,
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Utf8Marshaler))]
            string name);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void invokeModuleMethod(IntPtr method);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr invokeModuleMethodWithExceptions(IntPtr method);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void pushSizeT(IntPtr value);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr popSizeT();

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void pushSizeTArray(IntPtr[] values, IntPtr count);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void popSizeTArray(out IntPtr valuesPtr, out IntPtr count); // size_t**, size_t*

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void pushBool(bool value);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool popBool();

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void pushBoolArray(byte[] values, IntPtr count); // bool*, size_t

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void popBoolArray(out IntPtr valuesPtr, out IntPtr count); // bool**, size_t*

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void pushInt(int x);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int popInt();

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void pushIntArray(int[] values, IntPtr count); // size_t count

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void popIntArray(out IntPtr values, out IntPtr count); // both args are pointers

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void pushFloat(float x);
        
        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern float popFloat();
        
        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void pushFloatArray(float[] values, IntPtr count);
        
        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void popFloatArray(out IntPtr values, out IntPtr count);
        
        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void pushDouble(double x);
        
        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern double popDouble();
        
        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void pushDoubleArray(double[] values, IntPtr count);
        
        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void popDoubleArray(out IntPtr values, out IntPtr count);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void pushString(IntPtr str, IntPtr length); // size_t length

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void popString(out IntPtr strPtr, out IntPtr length); // size_t* length

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void pushStringArray(IntPtr[] strs, IntPtr[] lengths, IntPtr count); // const char **, size_t*, size_t

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void popStringArray(out IntPtr strsPtr, out IntPtr lengthsPtr, out IntPtr count); // const char ***, size_t**, size_t*

        public struct BufferDescriptor
        {
            public IntPtr Start;
            public int ElementSize;
            public IntPtr TotalCount;
            public IntPtr TotalSize;
        }

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void pushBuffer(int id, bool isClientId, bool inSafetyArea, ref BufferDescriptor descriptor);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void popBuffer(out int id, out bool isClientId, out bool inSafetyArea, out BufferDescriptor descriptor);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void pushClientFunc(int id);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int popServerFunc();

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void execServerFunc(int id);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr execServerFuncWithExceptions(int id);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void invokeInterfaceMethod(IntPtr method, int serverId);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr invokeInterfaceMethodWithExceptions(IntPtr method, int serverId);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void pushInstance(int id, bool isClientId, bool inSafetyArea);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int popInstance(out bool isClientId, out bool inSafetyArea);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int releaseServerResource(int id);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void clearServerSafetyArea();

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void dumpTables();

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void pushModuleConstants(IntPtr module);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void setException(IntPtr exceptionHandle);
    }
}