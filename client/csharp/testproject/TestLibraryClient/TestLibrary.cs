using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace TestLibraryClient
{
    public static class TestLibrary
    {
        private static ModuleHandle _module;
        private static ModuleMethodHandle _takesThing;
        private static ModuleMethodHandle _returnsThing;
        private static ModuleMethodHandle _applyFunc;
        private static InterfaceHandle _myThing;
        private static InterfaceMethodHandle _myThing_doSomething;

        public delegate int IntFunc(int x);

        private static void IntFunc__Push(IntFunc callback)
        {
            void CallbackWrapper()
            {
                var x = NativeImplClient.PopInt();
                NativeImplClient.PushInt(callback(x));
            }
            NativeImplClient.PushClientFuncVal(CallbackWrapper, Marshal.GetFunctionPointerForDelegate(callback));
        }

        private static IntFunc IntFunc__Pop()
        {
            var id = NativeImplClient.PopServerFuncValId();
            var remoteFunc = new NativeImplClient.ServerFunc(id);
            int Wrapper(int x)
            {
                NativeImplClient.PushInt(x);
                remoteFunc.Exec();
                return NativeImplClient.PopInt();
            }
            return Wrapper;
        }


        public interface MyThing : IDisposable
        {
            int DoSomething(int x);
        }

        private static MyThing MyThing__Pop()
        {
            NativeImplClient.PopInstanceId(out var id, out var isClientId);
            if (!isClientId)
            {
                return new ServerMyThing(id);
            }
            else
            {
                return (MyThing) NativeImplClient.ClientObject.GetById(id);
            }
        }

        public abstract class ClientMyThing : NativeImplClient.ClientObject, MyThing
        {
            public virtual void Dispose()
            {
                // override if necessary
            }
            public abstract int DoSomething(int x);
        }

        private class ServerMyThing : NativeImplClient.ServerObject, MyThing
        {
            public ServerMyThing(int id) : base(id)
            {
            }

            public int DoSomething(int x)
            {
                NativeImplClient.PushInt(x);
                NativeImplClient.InvokeInterfaceMethod(_myThing_doSomething, Id);
                return NativeImplClient.PopInt();
            }

            public void Dispose()
            {
                ServerDispose();
            }
        }

        public static void TakesThing(MyThing thing)
        {
            ((NativeImplClient.IPushable)thing).Push(false);
            NativeImplClient.InvokeModuleMethod(_takesThing);
        }

        public static MyThing ReturnsThing()
        {
            NativeImplClient.InvokeModuleMethod(_returnsThing);
            var __ret = MyThing__Pop();
            NativeImplClient.ServerClearSafetyArea();
            return __ret;
        }

        public static void ApplyFunc(IntFunc f)
        {
            IntFunc__Push(f);
            NativeImplClient.InvokeModuleMethod(_applyFunc);
        }

        public static void Init()
        {
            Debug.Assert(NativeImplClient.Init() == 0);
            _module = NativeImplClient.GetModule("TestLibrary");

            _takesThing = NativeImplClient.GetModuleMethod(_module, "takesThing");
            _returnsThing = NativeImplClient.GetModuleMethod(_module, "returnsThing");
            _applyFunc = NativeImplClient.GetModuleMethod(_module, "applyFunc");

            _myThing = NativeImplClient.GetInterface(_module, "MyThing");
            _myThing_doSomething = NativeImplClient.GetInterfaceMethod(_myThing, "doSomething");

            NativeImplClient.SetClientMethodWrapper(_myThing_doSomething, delegate(NativeImplClient.ClientObject obj)
            {
                var inst = (ClientMyThing) obj;
                var x = NativeImplClient.PopInt();
                NativeImplClient.PushInt(inst.DoSomething(x));
            });
        }

        public static void Shutdown()
        {
            NativeImplClient.Shutdown();
        }
    }
}
