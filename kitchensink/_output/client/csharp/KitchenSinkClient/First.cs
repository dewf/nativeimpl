using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using Org.Prefixed.KitchenSink.Support;
using ModuleHandle = Org.Prefixed.KitchenSink.Support.ModuleHandle;

namespace Org.Prefixed.KitchenSink
{
    public static class First
    {
        private static ModuleHandle _module;

        internal static void __Thing2_Array__Push(Thing2[] values, bool isReturn)
        {
            foreach (var value in values.Reverse())
            {
                Thing2__Push(value, isReturn);
            }
            NativeImplClient.PushSizeT((IntPtr)values.Length);
        }

        internal static Thing2[] __Thing2_Array__Pop()
        {
            var count = (int)NativeImplClient.PopSizeT();
            var ret = new Thing2[count];
            for (var i = 0; i < count; i++)
            {
                ret[i] = Thing2__Pop();
            }
            return ret;
        }
        internal static ModuleMethodHandle _doSomething;

        public static void DoSomething(Thing2 root)
        {
            Thing2__Push(root, false);
            NativeImplClient.InvokeModuleMethod(_doSomething);
        }
        public abstract record Thing1
        {
            internal abstract void Push(bool isReturn);
            internal static Thing1 Pop()
            {
                return NativeImplClient.PopInt32() switch
                {
                    0 => Value1.PopDerived(),
                    1 => Value2.PopDerived(),
                    2 => Value3.PopDerived(),
                    3 => Value4.PopDerived(),
                    _ => throw new Exception("Thing1.Pop() - unknown tag!")
                };
            }
            public sealed record Value1 : Thing1
            {
                internal override void Push(bool isReturn)
                {
                    // kind
                    NativeImplClient.PushInt32(0);
                }
                internal static Value1 PopDerived()
                {
                    return new Value1();
                }
            }
            public sealed record Value2(int X) : Thing1
            {
                public int X { get; } = X;
                internal override void Push(bool isReturn)
                {
                    NativeImplClient.PushInt32(X);
                    // kind
                    NativeImplClient.PushInt32(1);
                }
                internal static Value2 PopDerived()
                {
                    var x = NativeImplClient.PopInt32();
                    return new Value2(x);
                }
            }
            public sealed record Value3(string Y) : Thing1
            {
                public string Y { get; } = Y;
                internal override void Push(bool isReturn)
                {
                    NativeImplClient.PushString(Y);
                    // kind
                    NativeImplClient.PushInt32(2);
                }
                internal static Value3 PopDerived()
                {
                    var y = NativeImplClient.PopString();
                    return new Value3(y);
                }
            }
            public sealed record Value4(Thing2 Other) : Thing1
            {
                public Thing2 Other { get; } = Other;
                internal override void Push(bool isReturn)
                {
                    Thing2__Push(Other, isReturn);
                    // kind
                    NativeImplClient.PushInt32(3);
                }
                internal static Value4 PopDerived()
                {
                    var other = Thing2__Pop();
                    return new Value4(other);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Thing1__Push(Thing1 thing, bool isReturn)
        {
            thing.Push(isReturn);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Thing1 Thing1__Pop()
        {
            return Thing1.Pop();
        }
        public abstract record Thing2
        {
            internal abstract void Push(bool isReturn);
            internal static Thing2 Pop()
            {
                return NativeImplClient.PopInt32() switch
                {
                    0 => Variant1.PopDerived(),
                    1 => Variant2.PopDerived(),
                    2 => Variant3.PopDerived(),
                    _ => throw new Exception("Thing2.Pop() - unknown tag!")
                };
            }
            public sealed record Variant1 : Thing2
            {
                internal override void Push(bool isReturn)
                {
                    // kind
                    NativeImplClient.PushInt32(0);
                }
                internal static Variant1 PopDerived()
                {
                    return new Variant1();
                }
            }
            public sealed record Variant2(Thing1 Thing) : Thing2
            {
                public Thing1 Thing { get; } = Thing;
                internal override void Push(bool isReturn)
                {
                    Thing1__Push(Thing, isReturn);
                    // kind
                    NativeImplClient.PushInt32(1);
                }
                internal static Variant2 PopDerived()
                {
                    var thing = Thing1__Pop();
                    return new Variant2(thing);
                }
            }
            public sealed record Variant3(Thing2[] Children) : Thing2
            {
                public Thing2[] Children { get; } = Children;
                internal override void Push(bool isReturn)
                {
                    __Thing2_Array__Push(Children, isReturn);
                    // kind
                    NativeImplClient.PushInt32(2);
                }
                internal static Variant3 PopDerived()
                {
                    var children = __Thing2_Array__Pop();
                    return new Variant3(children);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Thing2__Push(Thing2 thing, bool isReturn)
        {
            thing.Push(isReturn);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Thing2 Thing2__Pop()
        {
            return Thing2.Pop();
        }

        internal static void __Init()
        {
            _module = NativeImplClient.GetModule("First");
            // assign module handles
            _doSomething = NativeImplClient.GetModuleMethod(_module, "doSomething");

            // no static init
        }

        internal static void __Shutdown()
        {
            // no static shutdown
        }
    }
}
