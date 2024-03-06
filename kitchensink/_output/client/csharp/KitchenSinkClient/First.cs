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
        internal static ModuleMethodHandle _operateOnThing;
        internal static ModuleMethodHandle _doSomething;
        internal static ModuleMethodHandle _throwException;
        internal static InterfaceHandle _whateverThing;
        internal static InterfaceMethodHandle _whateverThing_someMethod;
        internal static ExceptionHandle _coolException;

        public static void OperateOnThing(WhateverThing thing)
        {
            WhateverThing__Push(thing, false);
            NativeImplClient.InvokeModuleMethod(_operateOnThing);
        }

        public static string DoSomething(int a, int b)
        {
            NativeImplClient.PushInt32(b);
            NativeImplClient.PushInt32(a);
            NativeImplClient.InvokeModuleMethod(_doSomething);
            return NativeImplClient.PopString();
        }

        public static void ThrowException(string content)
        {
            NativeImplClient.PushString(content);
            NativeImplClient.InvokeModuleMethodWithExceptions(_throwException);
        }

        public interface WhateverThing : IDisposable
        {
            int SomeMethod(string s);
        }

        internal static void WhateverThing__Push(WhateverThing thing, bool isReturn)
        {
            if (thing != null)
            {
                ((IPushable)thing).Push(isReturn);
            }
            else
            {
                NativeImplClient.PushNull();
            }
        }

        internal static WhateverThing WhateverThing__Pop()
        {
            NativeImplClient.PopInstanceId(out var id, out var isClientId);
            if (id != 0)
            {
                if (!isClientId)
                {
                    return new ServerWhateverThing(id);
                }
                else
                {
                    return (WhateverThing) ClientObject.GetById(id);
                }
            }
            else
            {
                return null;
            }
        }

        public abstract class ClientWhateverThing : ClientObject, WhateverThing
        {
            public virtual void Dispose()
            {
                // override if necessary
            }
            public abstract int SomeMethod(string s);
        }

        internal class ServerWhateverThing : ServerObject, WhateverThing
        {
            public ServerWhateverThing(int id) : base(id)
            {
            }

            public int SomeMethod(string s)
            {
                NativeImplClient.PushString(s);
                NativeImplClient.InvokeInterfaceMethod(_whateverThing_someMethod, Id);
                return NativeImplClient.PopInt32();
            }

            public void Dispose()
            {
                ServerDispose();
            }
        }
        public class CoolException : Exception
        {
            public readonly string Reason;
            public CoolException(string reason) : base("CoolException")
            {
                Reason = reason;
            }
            internal void PushAndSet()
            {
                NativeImplClient.PushString(Reason);
                NativeImplClient.SetException(_coolException);
            }
            internal static void BuildAndThrow()
            {
                var reason = NativeImplClient.PopString();
                throw new CoolException(reason);
            }
        }

        internal static void __Init()
        {
            _module = NativeImplClient.GetModule("First");
            // assign module handles
            _operateOnThing = NativeImplClient.GetModuleMethod(_module, "operateOnThing");
            _doSomething = NativeImplClient.GetModuleMethod(_module, "doSomething");
            _throwException = NativeImplClient.GetModuleMethod(_module, "throwException");
            _coolException = NativeImplClient.GetException(_module, "CoolException");
            NativeImplClient.SetExceptionBuilder(_coolException, CoolException.BuildAndThrow);
            _whateverThing = NativeImplClient.GetInterface(_module, "WhateverThing");
            _whateverThing_someMethod = NativeImplClient.GetInterfaceMethod(_whateverThing, "someMethod");
            NativeImplClient.SetClientMethodWrapper(_whateverThing_someMethod, delegate(ClientObject obj)
            {
                var inst = (ClientWhateverThing) obj;
                var s = NativeImplClient.PopString();
                NativeImplClient.PushInt32(inst.SomeMethod(s));
            });

            // no static init
        }

        internal static void __Shutdown()
        {
            // no static shutdown
        }
    }
}
