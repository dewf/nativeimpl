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
        internal static ModuleMethodHandle _createThing;

        public static Nested.WhateverThing CreateThing()
        {
            NativeImplClient.InvokeModuleMethod(_createThing);
            return Nested.WhateverThing__Pop();
        }
        public static class Nested
        {
            internal static ModuleMethodHandle _whateverThing_someMethod;
            internal static ModuleMethodHandle _whateverThing_dispose;
            public class WhateverThing : IDisposable
            {
                internal readonly IntPtr NativeHandle;
                protected bool _disposed;
                internal WhateverThing(IntPtr nativeHandle)
                {
                    NativeHandle = nativeHandle;
                }
                public virtual void Dispose()
                {
                    if (!_disposed)
                    {
                        WhateverThing__Push(this);
                        NativeImplClient.InvokeModuleMethod(_whateverThing_dispose);
                        _disposed = true;
                    }
                }
                public void SomeMethod(int x)
                {
                    NativeImplClient.PushInt32(x);
                    WhateverThing__Push(this);
                    NativeImplClient.InvokeModuleMethod(_whateverThing_someMethod);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static void WhateverThing__Push(WhateverThing thing)
            {
                NativeImplClient.PushPtr(thing?.NativeHandle ?? IntPtr.Zero);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static WhateverThing WhateverThing__Pop()
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
            Nested._whateverThing_someMethod = NativeImplClient.GetModuleMethod(_module, "Nested.WhateverThing.someMethod");
            Nested._whateverThing_dispose = NativeImplClient.GetModuleMethod(_module, "Nested.WhateverThing.dispose");

            // no static init
        }

        internal static void __Shutdown()
        {
            // no static shutdown
        }
    }
}
