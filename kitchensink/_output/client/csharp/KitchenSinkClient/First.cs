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
        internal static ModuleMethodHandle _serverCreate;
        internal static InterfaceHandle _iTestInterface;
        internal static InterfaceMethodHandle _iTestInterface_wootMethod;

        public static void OperateOnInterface(ITestInterface thing)
        {
            ITestInterface__Push(thing, false);
            NativeImplClient.InvokeModuleMethod(_operateOnInterface);
        }

        public static ITestInterface ServerCreate()
        {
            NativeImplClient.InvokeModuleMethod(_serverCreate);
            var __ret = ITestInterface__Pop();
            NativeImplClient.ServerClearSafetyArea();
            return __ret;
        }

        public interface ITestInterface : IDisposable
        {
            void IDisposable.Dispose()
            {
                // nothing by default
            }
            void WootMethod(int x);
        }

        private static Dictionary<ITestInterface, IPushable> __ITestInterfaceToPushable = new();
        private class __ITestInterfaceWrapper : ClientInterfaceWrapper<ITestInterface>
        {
            public __ITestInterfaceWrapper(ITestInterface rawInterface) : base(rawInterface)
            {
            }
            protected override void ReleaseExtra()
            {
                // remove the raw interface from the lookup table, no longer needed
                __ITestInterfaceToPushable.Remove(RawInterface);
            }
        }

        internal static void ITestInterface__Push(ITestInterface thing, bool isReturn)
        {
            if (thing != null)
            {
                if (__ITestInterfaceToPushable.TryGetValue(thing, out var pushable))
                {
                    // either an already-known client thing, or a server thing
                    pushable.Push(isReturn);
                }
                else
                {
                    // as-yet-unknown client thing - wrap and add to lookup table
                    pushable = new __ITestInterfaceWrapper(thing);
                    __ITestInterfaceToPushable.Add(thing, pushable);
                }
                pushable.Push(isReturn);
            }
            else
            {
                NativeImplClient.PushNull();
            }
        }

        internal static ITestInterface ITestInterface__Pop()
        {
            NativeImplClient.PopInstanceId(out var id, out var isClientId);
            if (id != 0)
            {
                if (isClientId)
                {
                    // we must have sent it over originally, so wrapper must exist
                    var wrapper = (__ITestInterfaceWrapper)ClientObject.GetById(id);
                    return wrapper.RawInterface;
                }
                else // server ID
                {
                    var thing = new ServerITestInterface(id);
                    // add to lookup table before returning
                    __ITestInterfaceToPushable.Add(thing, thing);
                    return thing;
                }
            }
            else
            {
                return null;
            }
        }

        internal class ServerITestInterface : ServerObject, ITestInterface
        {
            public ServerITestInterface(int id) : base(id)
            {
            }

            public void WootMethod(int x)
            {
                NativeImplClient.PushInt32(x);
                NativeImplClient.InvokeInterfaceMethod(_iTestInterface_wootMethod, Id);
            }

            public void Dispose()
            {
                // need to rethink how the disposal works for interfaces, esp w/r/t server implementations
                // because now we're actually doing something here, whereas before it was just
                // ServerDispose() which was also called by the finalizer
                // but now the table removal stuff is NOT happening via finalizer

                // remove from lookup table
                __ITestInterfaceToPushable.Remove(this);
                // regular disposal stuff
                ServerDispose();
            }
        }

        internal static void __Init()
        {
            _module = NativeImplClient.GetModule("First");
            // assign module handles
            _operateOnInterface = NativeImplClient.GetModuleMethod(_module, "operateOnInterface");
            _serverCreate = NativeImplClient.GetModuleMethod(_module, "serverCreate");
            _iTestInterface = NativeImplClient.GetInterface(_module, "ITestInterface");
            _iTestInterface_wootMethod = NativeImplClient.GetInterfaceMethod(_iTestInterface, "wootMethod");
            NativeImplClient.SetClientMethodWrapper(_iTestInterface_wootMethod, delegate(ClientObject obj)
            {
                var inst = ((__ITestInterfaceWrapper)obj).RawInterface;
                var x = NativeImplClient.PopInt32();
                inst.WootMethod(x);
            });

            // no static init
        }

        internal static void __Shutdown()
        {
            // no static shutdown
        }
    }
}
