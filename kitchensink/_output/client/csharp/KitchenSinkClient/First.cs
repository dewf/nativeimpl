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
        private static ModuleMethodHandle _createThing;
        
        public static WhateverThing CreateThing()
        {
            NativeImplClient.InvokeModuleMethod(_createThing);
            return WhateverThing.__Pop();
        }
        
        public class WhateverThing : IDisposable
        {
            private readonly IntPtr _nativeHandle;
            protected bool _disposed;
            
            internal static ModuleMethodHandle _methodSomeMethod;
            internal static ModuleMethodHandle _methodDispose;
            internal WhateverThing(IntPtr nativeHandle)
            {
                _nativeHandle = nativeHandle;
            }
            public virtual void Dispose()
            {
                if (!_disposed)
                {
                    __Push(this);
                    NativeImplClient.InvokeModuleMethod(_methodDispose);
                    _disposed = true;
                }
            }
            public void SomeMethod(int x)
            {
                NativeImplClient.PushInt32(x);
                __Push(this);
                NativeImplClient.InvokeModuleMethod(_methodSomeMethod);
            }
            internal static void __Push(WhateverThing thing)
            {
                NativeImplClient.PushPtr(thing?._nativeHandle ?? IntPtr.Zero);
            }
            internal static WhateverThing __Pop()
            {
                var ptr = NativeImplClient.PopPtr();
                return ptr != IntPtr.Zero ? new WhateverThing(ptr) : null;
            }
        }
        internal static void __Init()
        {
            _module = NativeImplClient.GetModule("First");
            // assign module handles
            _createThing = NativeImplClient.GetModuleMethod(_module, "createThing");
            WhateverThing._methodSomeMethod = NativeImplClient.GetModuleMethod(_module, "WhateverThing_someMethod");
            WhateverThing._methodDispose = NativeImplClient.GetModuleMethod(_module, "WhateverThing_dispose");
            
            // no static init
        }
        internal static void __Shutdown()
        {
            // no static shutdown
        }
    }
}
