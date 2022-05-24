using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using TestLibraryClient.Support;
using ModuleHandle = TestLibraryClient.Support.ModuleHandle;

namespace TestLibraryClient
{
    public static class TestLibrary
    {
        private static ModuleHandle _module;
        private static ModuleMethodHandle _sumValues;

        // built-in array type: int[]

        public static int SumValues(int[] values, string threadId)
        {
            NativeImplClient.PushString(threadId);
            NativeImplClient.PushInt32Array(values);
            NativeImplClient.InvokeModuleMethod(_sumValues);
            return NativeImplClient.PopInt32();
        }

        public static void Init()
        {
            Debug.Assert(NativeImplClient.Init() == 0);
            _module = NativeImplClient.GetModule("TestLibrary");

            _sumValues = NativeImplClient.GetModuleMethod(_module, "sumValues");
        }

        public static void Shutdown()
        {
            NativeImplClient.Shutdown();
        }

        public static void DumpTables()
        {
            NativeImplClient.DumpTables();
        }
    }
}
