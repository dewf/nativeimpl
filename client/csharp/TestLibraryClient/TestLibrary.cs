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
        private static ModuleMethodHandle _stringTest;
        private static ModuleMethodHandle _returnString;
        private static ModuleMethodHandle _sum;
        private static ModuleMethodHandle _execFunc01;
        private static ModuleMethodHandle _getServerFunc;
        private static ModuleMethodHandle _stashFunc;
        private static ModuleMethodHandle _releaseFunc;
        private static ModuleMethodHandle _returnStashedFunc;
        private static ModuleMethodHandle _makeThing;
        private static ModuleMethodHandle _operateOnThing;
        private static ModuleMethodHandle _saveThing;
        private static ModuleMethodHandle _getSavedThing;
        private static ModuleMethodHandle _clientStashTest;
        private static ModuleMethodHandle _testNegative;
        private static ModuleMethodHandle _testClientThrow;
        private static ModuleMethodHandle _startTimer;
        private static ModuleMethodHandle _stopTimer;
        private static ModuleMethodHandle _takeSumType;
        private static ModuleMethodHandle _makeSumType;
        private static InterfaceHandle _iThing01;
        private static InterfaceMethodHandle _iThing01_someMethod;
        private static InterfaceMethodHandle _iThing01_stashThing;
        private static InterfaceMethodHandle _iThing01_getStashedThing;
        private static InterfaceMethodHandle _iThing01_testThrow;
        private static InterfaceMethodHandle _iThing01_onTimerTick;
        private static ExceptionHandle _negativeException;
        private static ExceptionHandle _interfaceMethodException;
        public static int Global01 { get; private set; }
        public static int AnotherGlobal { get; private set; }
        public static int LastIntGlobal { get; private set; }

        public abstract record FancySumType
        {
            public sealed record Value1(int A, string B) : FancySumType
            {
                public int A { get; } = A;
                public string B { get; } = B;
            }
            public sealed record Value2 : FancySumType;
            public sealed record Value3(string X) : FancySumType
            {
                public string X { get; } = X;
            }
        }

        private enum FancySumType__Tag
        {
            Value1,
            Value2,
            Value3
        }

        private static void FancySumType__Push(FancySumType thing, bool isReturn)
        {
            FancySumType__Tag which;
            switch (thing)
            {
                case FancySumType.Value1 value1:
                    which = FancySumType__Tag.Value1;
                    NativeImplClient.PushString(value1.B);
                    NativeImplClient.PushInt32(value1.A);
                    break;
                case FancySumType.Value2 value2:
                    which = FancySumType__Tag.Value2;
                    break;
                case FancySumType.Value3 value3:
                    which = FancySumType__Tag.Value3;
                    NativeImplClient.PushString(value3.X);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(thing));
            }
            NativeImplClient.PushInt32((int)which);
        }

        private static FancySumType FancySumType__Pop()
        {
            var which = NativeImplClient.PopInt32();
            switch ((FancySumType__Tag)which)
            {
                case FancySumType__Tag.Value1:
                {
                    var a = NativeImplClient.PopInt32();
                    var b = NativeImplClient.PopString();
                    return new FancySumType.Value1(a, b);
                }
                case FancySumType__Tag.Value2:
                {
                    return new FancySumType.Value2();
                }
                case FancySumType__Tag.Value3:
                {
                    var x = NativeImplClient.PopString();
                    return new FancySumType.Value3(x);
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }


        public class InterfaceMethodException : Exception
        {
            public readonly int Value;
            public InterfaceMethodException(int value) : base("InterfaceMethodException")
            {
                Value = value;
            }
            internal void PushAndSet()
            {
                NativeImplClient.PushInt32(Value);
                NativeImplClient.SetException(_interfaceMethodException);
            }
            internal static void BuildAndThrow()
            {
                var value = NativeImplClient.PopInt32();
                throw new InterfaceMethodException(value);
            }
        }


        public interface IThing01 : IDisposable
        {
            int SomeMethod(int x);
            void StashThing(IThing01 subThing);
            IThing01 GetStashedThing();
            void TestThrow(bool doThrow); // throws InterfaceMethodException
            void OnTimerTick();
        }

        private static void IThing01__Push(IThing01 thing, bool isReturn)
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

        private static IThing01 IThing01__Pop()
        {
            NativeImplClient.PopInstanceId(out var id, out var isClientId);
            if (id != 0)
            {
                if (!isClientId)
                {
                    return new ServerIThing01(id);
                }
                else
                {
                    return (IThing01) ClientObject.GetById(id);
                }
            }
            else
            {
                return null;
            }
        }

        public abstract class ClientIThing01 : ClientObject, IThing01
        {
            public virtual void Dispose()
            {
                // override if necessary
            }
            public abstract int SomeMethod(int x);
            public abstract void StashThing(IThing01 subThing);
            public abstract IThing01 GetStashedThing();
            public abstract void TestThrow(bool doThrow); // throws InterfaceMethodException
            public abstract void OnTimerTick();
        }

        private class ServerIThing01 : ServerObject, IThing01
        {
            public ServerIThing01(int id) : base(id)
            {
            }

            public int SomeMethod(int x)
            {
                NativeImplClient.PushInt32(x);
                NativeImplClient.InvokeInterfaceMethod(_iThing01_someMethod, Id);
                return NativeImplClient.PopInt32();
            }

            public void StashThing(IThing01 subThing)
            {
                IThing01__Push(subThing, false);
                NativeImplClient.InvokeInterfaceMethod(_iThing01_stashThing, Id);
            }

            public IThing01 GetStashedThing()
            {
                NativeImplClient.InvokeInterfaceMethod(_iThing01_getStashedThing, Id);
                var __ret = IThing01__Pop();
                NativeImplClient.ServerClearSafetyArea();
                return __ret;
            }

            public void TestThrow(bool doThrow)
            {
                NativeImplClient.PushBool(doThrow);
                NativeImplClient.InvokeInterfaceMethodWithExceptions(_iThing01_testThrow, Id);
            }

            public void OnTimerTick()
            {
                NativeImplClient.InvokeInterfaceMethod(_iThing01_onTimerTick, Id);
            }

            public void Dispose()
            {
                ServerDispose();
            }
        }

        public class NegativeException : Exception
        {
            public readonly int Value;
            public NegativeException(int value) : base("NegativeException")
            {
                Value = value;
            }
            internal void PushAndSet()
            {
                NativeImplClient.PushInt32(Value);
                NativeImplClient.SetException(_negativeException);
            }
            internal static void BuildAndThrow()
            {
                var value = NativeImplClient.PopInt32();
                throw new NegativeException(value);
            }
        }

        public delegate int SimpleIntFunc(int x);

        private static void SimpleIntFunc__Push(SimpleIntFunc callback)
        {
            void CallbackWrapper()
            {
                var x = NativeImplClient.PopInt32();
                NativeImplClient.PushInt32(callback(x));
            }
            NativeImplClient.PushClientFuncVal(CallbackWrapper, Marshal.GetFunctionPointerForDelegate(callback));
        }

        private static SimpleIntFunc SimpleIntFunc__Pop()
        {
            var id = NativeImplClient.PopServerFuncValId();
            var remoteFunc = new ServerFuncVal(id);
            int Wrapper(int x)
            {
                NativeImplClient.PushInt32(x);
                remoteFunc.Exec();
                return NativeImplClient.PopInt32();
            }
            return Wrapper;
        }

        public static void StringTest(string str)
        {
            NativeImplClient.PushString(str);
            NativeImplClient.InvokeModuleMethod(_stringTest);
        }

        public static string ReturnString()
        {
            NativeImplClient.InvokeModuleMethod(_returnString);
            return NativeImplClient.PopString();
        }

        public static int Sum(int a, int b, int c)
        {
            NativeImplClient.PushInt32(c);
            NativeImplClient.PushInt32(b);
            NativeImplClient.PushInt32(a);
            NativeImplClient.InvokeModuleMethod(_sum);
            return NativeImplClient.PopInt32();
        }

        public static int ExecFunc01(SimpleIntFunc func1)
        {
            SimpleIntFunc__Push(func1);
            NativeImplClient.InvokeModuleMethod(_execFunc01);
            return NativeImplClient.PopInt32();
        }

        public static SimpleIntFunc GetServerFunc()
        {
            NativeImplClient.InvokeModuleMethod(_getServerFunc);
            return SimpleIntFunc__Pop();
        }

        public static void StashFunc(SimpleIntFunc func2)
        {
            SimpleIntFunc__Push(func2);
            NativeImplClient.InvokeModuleMethod(_stashFunc);
        }

        public static void ReleaseFunc()
        {
            NativeImplClient.InvokeModuleMethod(_releaseFunc);
        }

        public static SimpleIntFunc ReturnStashedFunc()
        {
            NativeImplClient.InvokeModuleMethod(_returnStashedFunc);
            return SimpleIntFunc__Pop();
        }

        public static IThing01 MakeThing()
        {
            NativeImplClient.InvokeModuleMethod(_makeThing);
            var __ret = IThing01__Pop();
            NativeImplClient.ServerClearSafetyArea();
            return __ret;
        }

        public static int OperateOnThing(IThing01 thing, int arg)
        {
            NativeImplClient.PushInt32(arg);
            IThing01__Push(thing, false);
            NativeImplClient.InvokeModuleMethod(_operateOnThing);
            return NativeImplClient.PopInt32();
        }

        public static void SaveThing(IThing01 thing)
        {
            IThing01__Push(thing, false);
            NativeImplClient.InvokeModuleMethod(_saveThing);
        }

        public static IThing01 GetSavedThing()
        {
            NativeImplClient.InvokeModuleMethod(_getSavedThing);
            var __ret = IThing01__Pop();
            NativeImplClient.ServerClearSafetyArea();
            return __ret;
        }

        public static void ClientStashTest(IThing01 thing)
        {
            IThing01__Push(thing, false);
            NativeImplClient.InvokeModuleMethod(_clientStashTest);
        }

        public static void TestNegative(int value)
        {
            NativeImplClient.PushInt32(value);
            NativeImplClient.InvokeModuleMethodWithExceptions(_testNegative);
        }

        public static void TestClientThrow(IThing01 thing)
        {
            IThing01__Push(thing, false);
            NativeImplClient.InvokeModuleMethod(_testClientThrow);
        }

        public static void StartTimer(IThing01 thing, int frequency)
        {
            NativeImplClient.PushInt32(frequency);
            IThing01__Push(thing, false);
            NativeImplClient.InvokeModuleMethod(_startTimer);
        }

        public static void StopTimer()
        {
            NativeImplClient.InvokeModuleMethod(_stopTimer);
        }

        public static void TakeSumType(FancySumType value)
        {
            FancySumType__Push(value, false);
            NativeImplClient.InvokeModuleMethod(_takeSumType);
        }

        public static FancySumType MakeSumType()
        {
            NativeImplClient.InvokeModuleMethod(_makeSumType);
            return FancySumType__Pop();
        }

        public static void Init()
        {
            Debug.Assert(NativeImplClient.Init() == 0);
            _module = NativeImplClient.GetModule("TestLibrary");

            NativeImplClient.PushModuleConstants(_module);
            Global01 = NativeImplClient.PopInt32();
            AnotherGlobal = NativeImplClient.PopInt32();
            LastIntGlobal = NativeImplClient.PopInt32();

            _stringTest = NativeImplClient.GetModuleMethod(_module, "stringTest");
            _returnString = NativeImplClient.GetModuleMethod(_module, "returnString");
            _sum = NativeImplClient.GetModuleMethod(_module, "sum");
            _execFunc01 = NativeImplClient.GetModuleMethod(_module, "execFunc01");
            _getServerFunc = NativeImplClient.GetModuleMethod(_module, "getServerFunc");
            _stashFunc = NativeImplClient.GetModuleMethod(_module, "stashFunc");
            _releaseFunc = NativeImplClient.GetModuleMethod(_module, "releaseFunc");
            _returnStashedFunc = NativeImplClient.GetModuleMethod(_module, "returnStashedFunc");
            _makeThing = NativeImplClient.GetModuleMethod(_module, "makeThing");
            _operateOnThing = NativeImplClient.GetModuleMethod(_module, "operateOnThing");
            _saveThing = NativeImplClient.GetModuleMethod(_module, "saveThing");
            _getSavedThing = NativeImplClient.GetModuleMethod(_module, "getSavedThing");
            _clientStashTest = NativeImplClient.GetModuleMethod(_module, "clientStashTest");
            _testNegative = NativeImplClient.GetModuleMethod(_module, "testNegative");
            _testClientThrow = NativeImplClient.GetModuleMethod(_module, "testClientThrow");
            _startTimer = NativeImplClient.GetModuleMethod(_module, "startTimer");
            _stopTimer = NativeImplClient.GetModuleMethod(_module, "stopTimer");
            _takeSumType = NativeImplClient.GetModuleMethod(_module, "takeSumType");
            _makeSumType = NativeImplClient.GetModuleMethod(_module, "makeSumType");

            _negativeException = NativeImplClient.GetException(_module, "NegativeException");
            NativeImplClient.SetExceptionBuilder(_negativeException, NegativeException.BuildAndThrow);
            _interfaceMethodException = NativeImplClient.GetException(_module, "InterfaceMethodException");
            NativeImplClient.SetExceptionBuilder(_interfaceMethodException, InterfaceMethodException.BuildAndThrow);

            _iThing01 = NativeImplClient.GetInterface(_module, "IThing01");
            _iThing01_someMethod = NativeImplClient.GetInterfaceMethod(_iThing01, "someMethod");
            _iThing01_stashThing = NativeImplClient.GetInterfaceMethod(_iThing01, "stashThing");
            _iThing01_getStashedThing = NativeImplClient.GetInterfaceMethod(_iThing01, "getStashedThing");
            _iThing01_testThrow = NativeImplClient.GetInterfaceMethod(_iThing01, "testThrow");
            _iThing01_onTimerTick = NativeImplClient.GetInterfaceMethod(_iThing01, "onTimerTick");

            NativeImplClient.SetClientMethodWrapper(_iThing01_someMethod, delegate(ClientObject obj)
            {
                var inst = (ClientIThing01) obj;
                var x = NativeImplClient.PopInt32();
                NativeImplClient.PushInt32(inst.SomeMethod(x));
            });

            NativeImplClient.SetClientMethodWrapper(_iThing01_stashThing, delegate(ClientObject obj)
            {
                var inst = (ClientIThing01) obj;
                var subThing = IThing01__Pop();
                inst.StashThing(subThing);
            });

            NativeImplClient.SetClientMethodWrapper(_iThing01_getStashedThing, delegate(ClientObject obj)
            {
                var inst = (ClientIThing01) obj;
                IThing01__Push(inst.GetStashedThing(), true);
            });

            NativeImplClient.SetClientMethodWrapper(_iThing01_testThrow, delegate(ClientObject obj)
            {
                var inst = (ClientIThing01) obj;
                var doThrow = NativeImplClient.PopBool();
                try
                {
                    inst.TestThrow(doThrow);
                }
                catch (InterfaceMethodException e)
                {
                    NativeImplClient.SetException(_interfaceMethodException);
                    e.PushAndSet();
                }
            });

            NativeImplClient.SetClientMethodWrapper(_iThing01_onTimerTick, delegate(ClientObject obj)
            {
                var inst = (ClientIThing01) obj;
                inst.OnTimerTick();
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
