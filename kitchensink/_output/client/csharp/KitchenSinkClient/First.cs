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
        public static class WhateverThing
        {
            internal static ModuleMethodHandle _create;
            internal static ModuleMethodHandle _handle_someMethod;
            internal static ModuleMethodHandle _handle_dispose;

            public static Handle Create()
            {
                NativeImplClient.InvokeModuleMethod(_create);
                return Handle__Pop();
            }
            public class Handle : IDisposable
            {
                internal readonly IntPtr NativeHandle;
                protected bool _disposed;
                internal Handle(IntPtr nativeHandle)
                {
                    NativeHandle = nativeHandle;
                }
                public virtual void Dispose()
                {
                    if (!_disposed)
                    {
                        Handle__Push(this);
                        NativeImplClient.InvokeModuleMethod(_handle_dispose);
                        _disposed = true;
                    }
                }
                public void SomeMethod(int x)
                {
                    NativeImplClient.PushInt32(x);
                    Handle__Push(this);
                    NativeImplClient.InvokeModuleMethod(_handle_someMethod);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static void Handle__Push(Handle thing)
            {
                NativeImplClient.PushPtr(thing?.NativeHandle ?? IntPtr.Zero);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static Handle Handle__Pop()
            {
                var ptr = NativeImplClient.PopPtr();
                return ptr != IntPtr.Zero ? new Handle(ptr) : null;
            }
        }

        internal static void __Init()
        {
            _module = NativeImplClient.GetModule("First");
            // assign module handles
            WhateverThing._create = NativeImplClient.GetModuleMethod(_module, "WhateverThing.create");
            WhateverThing._handle_someMethod = NativeImplClient.GetModuleMethod(_module, "WhateverThing.Handle_someMethod");
            WhateverThing._handle_dispose = NativeImplClient.GetModuleMethod(_module, "WhateverThing.Handle_dispose");

            // no static init
        }

        internal static void __Shutdown()
        {
            // no static shutdown
        }
    }
}
