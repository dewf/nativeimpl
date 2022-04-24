using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TestLibraryClient
{
    public static class TestLibrary
    {
        private static ModuleHandle _module;
        private static ModuleMethodHandle _simpleFunction;



        public static void Init()
        {
            Debug.Assert(NativeImplClient.Init() == 0);

            _module = NativeImplClient.GetModule("TestLibrary");

            _simpleFunction = NativeImplClient.GetModuleMethod(_module, "simpleFunction");

        }

        public static void Shutdown()
        {
            NativeImplClient.Shutdown();
        }

        public static int SimpleFunction(int x)
        {
            NativeImplClient.PushInt(x);
            NativeImplClient.InvokeModuleMethod(_simpleFunction);
            return NativeImplClient.PopInt();
        }
    }
}
