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
        private static ModuleMethodHandle _doSomething;
        private static InterfaceHandle _coolThing;
        private static InterfaceMethodHandle _coolThing_method1;


        public interface CoolThing : IDisposable
        {
            int Method1(string x);
        }

        private static void CoolThing__Push(CoolThing thing, bool isReturn)
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

        private static CoolThing CoolThing__Pop()
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
            public abstract int Method1(string x);
        }

        private class ServerCoolThing : ServerObject, CoolThing
        {
            public ServerCoolThing(int id) : base(id)
            {
            }

            public int Method1(string x)
            {
                NativeImplClient.PushString(x);
                NativeImplClient.InvokeInterfaceMethod(_coolThing_method1, Id);
                return NativeImplClient.PopInt32();
            }

            public void Dispose()
            {
                ServerDispose();
            }
        }

        public static int DoSomething(int x, string y)
        {
            NativeImplClient.PushString(y);
            NativeImplClient.PushInt32(x);
            NativeImplClient.InvokeModuleMethod(_doSomething);
            return NativeImplClient.PopInt32();
        }

        public static void Init()
        {
            Debug.Assert(NativeImplClient.Init() == 0);
            _module = NativeImplClient.GetModule("TestLibrary");

            _doSomething = NativeImplClient.GetModuleMethod(_module, "doSomething");

            _coolThing = NativeImplClient.GetInterface(_module, "CoolThing");
            _coolThing_method1 = NativeImplClient.GetInterfaceMethod(_coolThing, "method1");

            NativeImplClient.SetClientMethodWrapper(_coolThing_method1, delegate(ClientObject obj)
            {
                var inst = (ClientCoolThing) obj;
                var x = NativeImplClient.PopString();
                NativeImplClient.PushInt32(inst.Method1(x));
            });
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
