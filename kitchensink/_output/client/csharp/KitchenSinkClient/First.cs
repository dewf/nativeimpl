using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Org.Prefixed.KitchenSink.Support;
using ModuleHandle = Org.Prefixed.KitchenSink.Support.ModuleHandle;

namespace Org.Prefixed.KitchenSink
{
    public static class First
    {
        private static ModuleHandle _module;
        internal static ModuleMethodHandle _doSomething;

        public static string DoSomething(int a, int b)
        {
            NativeImplClient.PushInt32(b);
            NativeImplClient.PushInt32(a);
            NativeImplClient.InvokeModuleMethod(_doSomething);
            return NativeImplClient.PopString();
        }

        internal static void __Init()
        {
            _module = NativeImplClient.GetModule("First");
            // assign module handles
            _doSomething = NativeImplClient.GetModuleMethod(_module, "doSomething");

            // no static init
        }

        internal static void __Shutdown()
        {
            // no static shutdown
        }
    }
}
