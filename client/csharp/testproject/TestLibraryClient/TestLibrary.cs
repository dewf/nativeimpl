using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TestLibraryClient
{
    public static class TestLibrary
    {
        private static ModuleHandle _module;
        private static ModuleMethodHandle _sumFunc;
        private static ModuleMethodHandle _execFunc01;
        private static ModuleMethodHandle _stashFunc;
        private static ModuleMethodHandle _releaseFunc;
        private static ModuleMethodHandle _returnStashedFunc;
        
        private static InterfaceHandle _ithing01;
        private static InterfaceMethodHandle _ithing01SomeMethod;
        private static InterfaceMethodHandle _ithing01StashThing;
        private static InterfaceMethodHandle _ithing01GetStashedThing;
        private static InterfaceMethodHandle _ithing01TestThrow;
        private static InterfaceMethodHandle _ithing01OnTimerTick;
        
        private static ModuleMethodHandle _makeThing;
        private static ModuleMethodHandle _operateOnThing;
        private static ModuleMethodHandle _saveThing;
        private static ModuleMethodHandle _getSavedThing;

        private static ModuleMethodHandle _clientStashTest;

        private static ExceptionHandle _negativeException;
        private static ModuleMethodHandle _testNegative;

        private static ExceptionHandle _interfaceMethodException; // from ithing->testThrow

        private static ModuleMethodHandle _testClientThrow;
        private static ModuleMethodHandle _startTimer;
        private static ModuleMethodHandle _stopTimer;
        
        // public globals
        public static int Global01 { get; private set; }
        public static int AnotherGlobal { get; private set; }
        public static int LastIntGlobal { get; private set; }

        public static void Init()
        {
            Debug.Assert(NativeImplClient.Init() == 0);
            
            _module = NativeImplClient.GetModule("TestLibrary");
            
            // global constants
            NativeImplClient.PushModuleConstants(_module);
            Global01 = NativeImplClient.PopInt();
            AnotherGlobal = NativeImplClient.PopInt();
            LastIntGlobal = NativeImplClient.PopInt();
            // end constants
            
            _sumFunc = NativeImplClient.GetModuleMethod(_module, "sum");
            _execFunc01 = NativeImplClient.GetModuleMethod(_module, "execFunc01");
            _stashFunc = NativeImplClient.GetModuleMethod(_module, "stashFunc");
            _releaseFunc = NativeImplClient.GetModuleMethod(_module, "releaseFunc");
            _returnStashedFunc = NativeImplClient.GetModuleMethod(_module, "returnStashedFunc");

            _ithing01 = NativeImplClient.GetInterface(_module, "IThing01");
            _ithing01SomeMethod = NativeImplClient.GetInterfaceMethod(_ithing01, "someMethod");
            _ithing01StashThing = NativeImplClient.GetInterfaceMethod(_ithing01, "stashThing");
            _ithing01GetStashedThing = NativeImplClient.GetInterfaceMethod(_ithing01, "getStashedThing");
            _ithing01TestThrow = NativeImplClient.GetInterfaceMethod(_ithing01, "testThrow");
            _ithing01OnTimerTick = NativeImplClient.GetInterfaceMethod(_ithing01, "onTimerTick");
            
            _makeThing = NativeImplClient.GetModuleMethod(_module, "makeThing");
            _operateOnThing = NativeImplClient.GetModuleMethod(_module, "operateOnThing");
            
            _saveThing = NativeImplClient.GetModuleMethod(_module, "saveThing");
            _getSavedThing = NativeImplClient.GetModuleMethod(_module, "getSavedThing");

            _clientStashTest = NativeImplClient.GetModuleMethod(_module, "clientStashTest");
            
            // method wrappers here
            NativeImplClient.SetClientMethodWrapper(_ithing01SomeMethod, delegate(NativeImplClient.ClientObject obj)
            {
                var thing = (ClientIThing01) obj;
                var x = NativeImplClient.PopInt();
                var res = thing.SomeMethod(x);
                NativeImplClient.PushInt(res);
            });
            
            NativeImplClient.SetClientMethodWrapper(_ithing01StashThing, delegate(NativeImplClient.ClientObject obj)
            {
                var thing = (ClientIThing01) obj;
                var thingParam = IThing01__Pop();
                thing.StashThing(thingParam);
                // no return value
            });
            
            NativeImplClient.SetClientMethodWrapper(_ithing01GetStashedThing, delegate(NativeImplClient.ClientObject obj)
            {
                var thing = (ClientIThing01) obj;
                // no params
                var ret = thing.GetStashedThing();
                ((NativeImplClient.IPushable)ret).Push(true); // safe push for return, remote side responsible for clearing
            });
            
            NativeImplClient.SetClientMethodWrapper(_ithing01TestThrow, delegate(NativeImplClient.ClientObject obj)
            {
                var thing = (ClientIThing01) obj;
                var doThrow = NativeImplClient.PopBool();
                try
                {
                    thing.TestThrow(doThrow);
                }
                catch (InterfaceMethodException e) // convert C# exception to NativeImpl :D
                {
                    NativeImplClient.SetException(_interfaceMethodException);
                    e.Push();
                }
            });
            
            NativeImplClient.SetClientMethodWrapper(_ithing01OnTimerTick, delegate(NativeImplClient.ClientObject obj)
            {
                var thing = (ClientIThing01) obj;
                // no params
                thing.OnTimerTick();
                // no return
            });
            
            // exception testing ...
            _negativeException = NativeImplClient.GetException(_module, "NegativeException");
            NativeImplClient.SetExceptionBuilder(_negativeException, NegativeException.Pop);
            
            _testNegative = NativeImplClient.GetModuleMethod(_module, "testNegative");

            _interfaceMethodException = NativeImplClient.GetException(_module, "InterfaceMethodException");
            NativeImplClient.SetExceptionBuilder(_interfaceMethodException, InterfaceMethodException.Pop);

            _testClientThrow = NativeImplClient.GetModuleMethod(_module, "testClientThrow");
            _startTimer = NativeImplClient.GetModuleMethod(_module, "startTimer");
            _stopTimer = NativeImplClient.GetModuleMethod(_module, "stopTimer");
        }
        public static void Shutdown()
        {
            NativeImplClient.Shutdown();
        }
        public static int Sum(int a, int b, int c)
        {
            NativeImplClient.PushInt(c);
            NativeImplClient.PushInt(b);
            NativeImplClient.PushInt(a);
            NativeImplClient.InvokeModuleMethod(_sumFunc);
            return NativeImplClient.PopInt();
        }
        public delegate int IntFuncDelegate(int x);
        public static int ExecFunc01(IntFuncDelegate callback)
        {
            void CallbackWrapper()
            {
                var x = NativeImplClient.PopInt();
                var res = callback(x);
                NativeImplClient.PushInt(res);
            }
            NativeImplClient.PushClientFuncVal(CallbackWrapper, Marshal.GetFunctionPointerForDelegate(callback)); // latter gets a unique signature for this closure (so we don't needlessly duplicate them)
            NativeImplClient.InvokeModuleMethod(_execFunc01);
            return NativeImplClient.PopInt();
        }
        public static void StashFunc(IntFuncDelegate callback)
        {
            void CallbackWrapper()
            {
                var x = NativeImplClient.PopInt();
                var res = callback(x);
                NativeImplClient.PushInt(res);
            }
            NativeImplClient.PushClientFuncVal(CallbackWrapper, Marshal.GetFunctionPointerForDelegate(callback)); // TODO: prevent callback from getting GC'ed while it's in there?
            NativeImplClient.InvokeModuleMethod(_stashFunc);
        }
        public static void ReleaseFunc()
        {
            NativeImplClient.InvokeModuleMethod(_releaseFunc);
        }
        public static IntFuncDelegate ReturnStashedFunc()
        {
            NativeImplClient.InvokeModuleMethod(_returnStashedFunc);
            var id = NativeImplClient.PopServerFuncValId();
            
            // wrap execution in a delegate, that is also self-freeing on GC finalization
            var remoteFunc = new NativeImplClient.ServerFunc(id);
            
            int Wrapper(int x)
            {
                NativeImplClient.PushInt(x);
                remoteFunc.Exec();
                return NativeImplClient.PopInt();
            }

            return Wrapper;
        }

        public interface IThing01 : IDisposable
        {
            // actual public methods =====
            int SomeMethod(int x);
            void StashThing(IThing01 subThing);
            IThing01 GetStashedThing();
            void TestThrow(bool doThrow);
            void OnTimerTick();
        }

        private static IThing01 IThing01__Pop()
        {
            NativeImplClient.PopInstanceId(out var id, out var isClientId);
            if (!isClientId)
            {
                return new ServerIThing01(id);
            }
            else
            {
                return (IThing01) NativeImplClient.ClientObject.GetById(id);
            }
        }

        public abstract class ClientIThing01 : NativeImplClient.ClientObject, IThing01
        {
            public virtual void Dispose()
            {
                // override if necessary
            }

            public abstract int SomeMethod(int x);
            public abstract void StashThing(IThing01 subThing);
            public abstract IThing01 GetStashedThing();
            public abstract void TestThrow(bool doThrow);
            public abstract void OnTimerTick();
        }
        
        private class ServerIThing01 : NativeImplClient.ServerObject, IThing01
        {
            public ServerIThing01(int id) : base(id)
            {
            }

            public int SomeMethod(int x)
            {
                NativeImplClient.PushInt(x);
                NativeImplClient.InvokeInterfaceMethod(_ithing01SomeMethod, Id);
                return NativeImplClient.PopInt();
            }

            public void StashThing(IThing01 subThing)
            {
                ((NativeImplClient.IPushable)subThing).Push(false);
                NativeImplClient.InvokeInterfaceMethod(_ithing01StashThing, Id);
            }

            public IThing01 GetStashedThing()
            {
                NativeImplClient.InvokeInterfaceMethod(_ithing01GetStashedThing, Id);
                var ret = IThing01__Pop();
                NativeImplClient.ServerClearSafetyArea(); // because remote returned an interface to us - was being kept safe on the remote side until we got it
                return ret;
            }

            public void TestThrow(bool doThrow)
            {
                NativeImplClient.PushBool(doThrow);
                NativeImplClient.InvokeInterfaceMethodWithExceptions(_ithing01TestThrow, Id); // will throw if necessary
            }

            public void OnTimerTick()
            {
                NativeImplClient.InvokeInterfaceMethod(_ithing01OnTimerTick, Id);
            }

            public void Dispose()
            {
                ServerDispose();
            }
        }

        public static IThing01 MakeThing()
        {
            NativeImplClient.InvokeModuleMethod(_makeThing);
            var ret = IThing01__Pop();
            NativeImplClient.ServerClearSafetyArea(); // because remote returned an interface to us - was being kept safe on the remote side until we got it
            return ret;
        }

        public static int OperateOnThing(IThing01 thing, int arg)
        {
            NativeImplClient.PushInt(arg);
            ((NativeImplClient.IPushable)thing).Push(false);
            NativeImplClient.InvokeModuleMethod(_operateOnThing);
            return NativeImplClient.PopInt();
        }

        public static void SaveThing(IThing01 thing)
        {
            ((NativeImplClient.IPushable)thing).Push(false);
            NativeImplClient.InvokeModuleMethod(_saveThing);
        }
        
        public static IThing01 GetSavedThing()
        {
            NativeImplClient.InvokeModuleMethod(_getSavedThing);
            var ret = IThing01__Pop();
            Console.WriteLine("C# IThing01 safely popped, before clearing safety area");
            NativeImplClient.ServerClearSafetyArea(); // because remote returned an interface to us
            return ret;
        }
        
        public static void ClientStashTest(IThing01 thing)
        {
            ((NativeImplClient.IPushable)thing).Push(false);
            NativeImplClient.InvokeModuleMethod(_clientStashTest);
            // no return value
        }
        
        public class NegativeException : Exception
        {
            public readonly int Value;
            private NegativeException(int value) : base($"Negative Value Exception")
            {
                Value = value;
            }
            public static NegativeException Pop()
            {
                return new NegativeException(NativeImplClient.PopInt());
            }
        }

        public static void TestNegative(int value)
        {
            NativeImplClient.PushInt(value);
            NativeImplClient.InvokeModuleMethodWithExceptions(_testNegative);
        }

        public class InterfaceMethodException : Exception
        {
            public readonly int Value;
            public InterfaceMethodException(int value) : base("Interface Method Exception!")
            {
                Value = value;
            }
            public void Push()
            {
                NativeImplClient.PushInt(Value);
            }
            public static InterfaceMethodException Pop()
            {
                return new InterfaceMethodException(NativeImplClient.PopInt());
            }
        }

        public static void TestClientThrow(IThing01 thing)
        {
            ((NativeImplClient.IPushable)thing).Push(false);
            NativeImplClient.InvokeModuleMethod(_testClientThrow);
        }

        public static void StartTimer(IThing01 thing, int frequency)
        {
            NativeImplClient.PushInt(frequency);
            ((NativeImplClient.IPushable)thing).Push(false);
            NativeImplClient.InvokeModuleMethod(_startTimer);
            // no return value
        }

        public static void StopTimer()
        {
            NativeImplClient.InvokeModuleMethod(_stopTimer);
        }
    }
}
