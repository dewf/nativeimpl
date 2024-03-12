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
        internal static InterfaceHandle _coolThing;
        internal static InterfaceMethodHandle _coolThing_doSomething;
        internal static ExceptionHandle _wootProblem;

        public static CoolThing CreateThing()
        {
            NativeImplClient.InvokeModuleMethod(_createThing);
            var __ret = CoolThing__Pop();
            NativeImplClient.ServerClearSafetyArea();
            return __ret;
        }
        public class WootProblem : Exception
        {
            public readonly string S;
            public WootProblem(string s) : base("WootProblem")
            {
                S = s;
            }
            internal void PushAndSet()
            {
                NativeImplClient.PushString(S);
                NativeImplClient.SetException(_wootProblem);
            }
            internal static void BuildAndThrow()
            {
                var s = NativeImplClient.PopString();
                throw new WootProblem(s);
            }
        }

        public interface CoolThing : IDisposable
        {
            void DoSomething(int x); // throws WootProblem
        }

        internal static void CoolThing__Push(CoolThing thing, bool isReturn)
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

        internal static CoolThing CoolThing__Pop()
        {
            NativeImplClient.PopInstanceId(out var id, out var isClientId);
            if (id != 0)
            {
                if (!isClientId)
                {
                    return new ServerCoolThing(id);
                }
                else
                {
                    return (CoolThing) ClientObject.GetById(id);
                }
            }
            else
            {
                return null;
            }
        }

        public abstract class ClientCoolThing : ClientObject, CoolThing
        {
            public virtual void Dispose()
            {
                // override if necessary
            }
            public abstract void DoSomething(int x); // throws WootProblem
        }

        internal class ServerCoolThing : ServerObject, CoolThing
        {
            public ServerCoolThing(int id) : base(id)
            {
            }

            public void DoSomething(int x)
            {
                NativeImplClient.PushInt32(x);
                NativeImplClient.InvokeInterfaceMethodWithExceptions(_coolThing_doSomething, Id);
            }

            public void Dispose()
            {
                ServerDispose();
            }
        }

        internal static void __Init()
        {
            _module = NativeImplClient.GetModule("First");
            // assign module handles
            _createThing = NativeImplClient.GetModuleMethod(_module, "createThing");
            _wootProblem = NativeImplClient.GetException(_module, "WootProblem");
            NativeImplClient.SetExceptionBuilder(_wootProblem, WootProblem.BuildAndThrow);
            _coolThing = NativeImplClient.GetInterface(_module, "CoolThing");
            _coolThing_doSomething = NativeImplClient.GetInterfaceMethod(_coolThing, "doSomething");
            NativeImplClient.SetClientMethodWrapper(_coolThing_doSomething, delegate(ClientObject obj)
            {
                var inst = (ClientCoolThing) obj;
                var x = NativeImplClient.PopInt32();
                try
                {
                    inst.DoSomething(x);
                }
                catch (WootProblem e)
                {
                    NativeImplClient.SetException(_wootProblem);
                    e.PushAndSet();
                }
            });

            // no static init
        }

        internal static void __Shutdown()
        {
            // no static shutdown
        }
    }
}
