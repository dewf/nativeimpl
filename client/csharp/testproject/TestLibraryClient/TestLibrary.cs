using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TestLibraryClient
{
    public static class TestLibrary
    {
        private static ModuleHandle _module;
        private static ModuleMethodHandle _mapTest;

        private static InterfaceHandle _Heyas;
        private static InterfaceMethodHandle _Heyas_someMethod;

        private static ExceptionHandle _coolException;


        public static void Init()
        {
            Debug.Assert(NativeImplClient.Init() == 0);

            _module = NativeImplClient.GetModule("TestLibrary");

            _mapTest = NativeImplClient.GetModuleMethod(_module, "mapTest");

            _coolException = NativeImplClient.GetException(_module, "CoolException");
            NativeImplClient.SetExceptionBuilder(_coolException, CoolException.Pop);

            _Heyas = NativeImplClient.GetInterface(_module, "Heyas");
            _Heyas_someMethod = NativeImplClient.GetInterfaceMethod(_Heyas, "someMethod");

            NativeImplClient.SetClientMethodWrapper(_Heyas_someMethod, delegate(NativeImplClient.ClientObject obj)
            {
                var inst = (Client_Heyas) obj;
                var x = NativeImplClient.PopInt();
                try
                {
                    inst.SomeMethod(x);
                }
                catch (CoolException e)
                {
                    NativeImplClient.SetException(_coolException);
                    e.Push();
                }
            });
        }

        public static void Shutdown()
        {
            NativeImplClient.Shutdown();
        }

        public delegate int CoolDelegate(string x);

        private static void CoolDelegate__Push(CoolDelegate callback)
        {
            void CallbackWrapper()
            {
                var x = NativeImplClient.PopString();
                NativeImplClient.PushInt(callback(x));
            }
            NativeImplClient.PushClientFuncVal(CallbackWrapper, Marshal.GetFunctionPointerForDelegate(callback));
        }

        private static CoolDelegate CoolDelegate__Pop()
        {
            var id = NativeImplClient.PopServerFuncValId();
            var remoteFunc = new NativeImplClient.ServerFunc(id);
            int Wrapper(string x)
            {
                NativeImplClient.PushString(x);
                remoteFunc.Exec();
                return NativeImplClient.PopInt();
            }
            return Wrapper;
        }

        public delegate void FinalDelegate((typeToString: not yet supported) v);

        private static void FinalDelegate__Push(FinalDelegate callback)
        {
            void CallbackWrapper()
            {
                (popValue TypedefType "DeepMap" not yet supported)
                callback(v);
            }
            NativeImplClient.PushClientFuncVal(CallbackWrapper, Marshal.GetFunctionPointerForDelegate(callback));
        }

        private static FinalDelegate FinalDelegate__Pop()
        {
            var id = NativeImplClient.PopServerFuncValId();
            var remoteFunc = new NativeImplClient.ServerFunc(id);
            void Wrapper((typeToString: not yet supported) v)
            {
                (pushValue TypedefType "DeepMap" not yet supported)
                remoteFunc.Exec();
            }
            return Wrapper;
        }

        public static void MapTest(FinalDelegate whoa)
        {
            FinalDelegate__Push(whoa);
            NativeImplClient.InvokeModuleMethod(_mapTest);
        }

        public interface Heyas : IDisposable
        {
            void SomeMethod(int x); // throws CoolException
        }

        private static Heyas Heyas__Pop()
        {
            NativeImplClient.PopInstanceId(out var id, out var isClientId);
            if (!isClientId)
            {
                return new Server_Heyas(id);
            }
            else
            {
                return (Heyas) NativeImplClient.ClientObject.GetById(id);
            }
        }

        public abstract class Client_Heyas : NativeImplClient.ClientObject, Heyas
        {
            public virtual void Dispose()
            {
                // override if necessary
            }
            public abstract void SomeMethod(int x); // throws CoolException
        }

        private class Server_Heyas : NativeImplClient.ServerObject, Heyas
        {
            public Server_Heyas(int id) : base(id)
            {
            }

            public void SomeMethod(int x)
            {
                NativeImplClient.PushInt(x);
                NativeImplClient.InvokeInterfaceMethodWithExceptions(_Heyas_someMethod, Id);
            }

            public void Dispose()
            {
                ServerDispose();
            }
        }

        public class CoolException : Exception
        {
            public readonly int X;
            public readonly string Woot;
            public CoolException(int x, string woot) : base("CoolException")
            {
                X = x;
                Woot = woot;
            }
            public void Push()
            {
                NativeImplClient.PushString(Woot);
                NativeImplClient.PushInt(X);
            }
            public static CoolException Pop()
            {
                var x = NativeImplClient.PopInt();
                var woot = NativeImplClient.PopString();
                return new CoolException(x, woot);
            }
        }
    }
}
