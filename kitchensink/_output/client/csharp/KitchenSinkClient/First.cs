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
            public sealed record Value1 : Thing1;
            public sealed record Value2(int X) : Thing1
            {
                public int X { get; } = X;
            }
            public sealed record Value3(string Y) : Thing1
            {
                public string Y { get; } = Y;
            }
        }

        private enum Thing1__Tag
        {
            Value1,
            Value2,
            Value3
        }

        internal static void Thing1__Push(Thing1 thing, bool isReturn)
        {
            Thing1__Tag which;
            switch (thing)
            {
                case Thing1.Value1 value1:
                    which = Thing1__Tag.Value1;
                    break;
                case Thing1.Value2 value2:
                    which = Thing1__Tag.Value2;
                    NativeImplClient.PushInt32(value2.X);
                    break;
                case Thing1.Value3 value3:
                    which = Thing1__Tag.Value3;
                    NativeImplClient.PushString(value3.Y);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(thing));
            }
            NativeImplClient.PushInt32((int)which);
        }

        internal static Thing1 Thing1__Pop()
        {
            var which = NativeImplClient.PopInt32();
            switch ((Thing1__Tag)which)
            {
                case Thing1__Tag.Value1:
                {
                    return new Thing1.Value1();
                }
                case Thing1__Tag.Value2:
                {
                    var x = NativeImplClient.PopInt32();
                    return new Thing1.Value2(x);
                }
                case Thing1__Tag.Value3:
                {
                    var y = NativeImplClient.PopString();
                    return new Thing1.Value3(y);
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        public abstract record Thing2
        {
            public sealed record Variant1 : Thing2;
            public sealed record Variant2(Thing1 Thing) : Thing2
            {
                public Thing1 Thing { get; } = Thing;
            }
            public sealed record Variant3(Thing2[] Children) : Thing2
            {
                public Thing2[] Children { get; } = Children;
            }
        }

        private enum Thing2__Tag
        {
            Variant1,
            Variant2,
            Variant3
        }

        internal static void Thing2__Push(Thing2 thing, bool isReturn)
        {
            Thing2__Tag which;
            switch (thing)
            {
                case Thing2.Variant1 variant1:
                    which = Thing2__Tag.Variant1;
                    break;
                case Thing2.Variant2 variant2:
                    which = Thing2__Tag.Variant2;
                    Thing1__Push(variant2.Thing, isReturn);
                    break;
                case Thing2.Variant3 variant3:
                    which = Thing2__Tag.Variant3;
                    __Thing2_Array__Push(variant3.Children, isReturn);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(thing));
            }
            NativeImplClient.PushInt32((int)which);
        }

        internal static Thing2 Thing2__Pop()
        {
            var which = NativeImplClient.PopInt32();
            switch ((Thing2__Tag)which)
            {
                case Thing2__Tag.Variant1:
                {
                    return new Thing2.Variant1();
                }
                case Thing2__Tag.Variant2:
                {
                    var thing = Thing1__Pop();
                    return new Thing2.Variant2(thing);
                }
                case Thing2__Tag.Variant3:
                {
                    var children = __Thing2_Array__Pop();
                    return new Thing2.Variant3(children);
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
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
