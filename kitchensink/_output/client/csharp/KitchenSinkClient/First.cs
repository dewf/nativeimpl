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
        internal static ModuleMethodHandle _operateOnInterface;

        public static void OperateOnInterface(InterfaceGroup.MyInterface thing)
        {
            InterfaceGroup.MyInterface__Push(thing, false);
            NativeImplClient.InvokeModuleMethod(_operateOnInterface);
        }
        public static class InterfaceGroup
        {
            internal static InterfaceHandle _myInterface;
            internal static InterfaceMethodHandle _myInterface_doesSomething;

            public interface MyInterface : IDisposable
            {
                void DoesSomething(string s);
            }

            internal static void MyInterface__Push(MyInterface thing, bool isReturn)
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

            internal static MyInterface MyInterface__Pop()
            {
                NativeImplClient.PopInstanceId(out var id, out var isClientId);
                if (id != 0)
                {
                    if (!isClientId)
                    {
                        return new ServerMyInterface(id);
                    }
                    else
                    {
                        return (MyInterface) ClientObject.GetById(id);
                    }
                }
                else
                {
                    return null;
                }
            }

            public abstract class ClientMyInterface : ClientObject, MyInterface
            {
                public virtual void Dispose()
                {
                    // override if necessary
                }
                public abstract void DoesSomething(string s);
            }

            internal class ServerMyInterface : ServerObject, MyInterface
            {
                public ServerMyInterface(int id) : base(id)
                {
                }

                public void DoesSomething(string s)
                {
                    NativeImplClient.PushString(s);
                    NativeImplClient.InvokeInterfaceMethod(_myInterface_doesSomething, Id);
                }

                public void Dispose()
                {
                    ServerDispose();
                }
            }
        }

        internal static void __Init()
        {
            _module = NativeImplClient.GetModule("First");
            // assign module handles
            _operateOnInterface = NativeImplClient.GetModuleMethod(_module, "operateOnInterface");
            InterfaceGroup._myInterface = NativeImplClient.GetInterface(_module, "InterfaceGroup.MyInterface");
            InterfaceGroup._myInterface_doesSomething = NativeImplClient.GetInterfaceMethod(InterfaceGroup._myInterface, "doesSomething");
            NativeImplClient.SetClientMethodWrapper(InterfaceGroup._myInterface_doesSomething, delegate(ClientObject obj)
            {
                var inst = (InterfaceGroup.ClientMyInterface) obj;
                var s = NativeImplClient.PopString();
                inst.DoesSomething(s);
            });

            // no static init
        }

        internal static void __Shutdown()
        {
            // no static shutdown
        }
    }
}
