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

        private static void __Int_Option__Push(Maybe<int> maybeValue, bool isReturn)
        {
            if (maybeValue.TryGetValue(out var value))
            {
                NativeImplClient.PushInt32(value);
                NativeImplClient.PushBool(true);
            }
            else
            {
                NativeImplClient.PushBool(false);
            }
        }

        private static Maybe<int> __Int_Option__Pop()
        {
            var hasValue = NativeImplClient.PopBool();
            if (hasValue)
            {
                return NativeImplClient.PopInt32();
            }
            return Maybe<int>.None;
        }

        private static void __Int_Option_Array__Push(Maybe<int>[] values, bool isReturn)
        {
            var valueArray = values.Where(v => v.HasValue).Select(v => v.Value).ToArray();
            var hasValueArray = values.Select(v => v.HasValue).ToArray();
            NativeImplClient.PushInt32Array(valueArray);
            NativeImplClient.PushBoolArray(hasValueArray);
        }

        private static Maybe<int>[] __Int_Option_Array__Pop()
        {
            var hasValueArray = NativeImplClient.PopBoolArray();
            var valueArray = NativeImplClient.PopInt32Array();
            var valuePtr = 0;
            return hasValueArray.Select(hasValue => hasValue ? valueArray[valuePtr++] : Maybe<int>.None).ToArray();
        }
        internal static ModuleMethodHandle _neatoMethod;
        internal static ModuleMethodHandle _returnsArray;

        public static void NeatoMethod(Maybe<int>[] maybeArray)
        {
            __Int_Option_Array__Push(maybeArray, false);
            NativeImplClient.InvokeModuleMethod(_neatoMethod);
        }

        public static Maybe<int>[] ReturnsArray()
        {
            NativeImplClient.InvokeModuleMethod(_returnsArray);
            return __Int_Option_Array__Pop();
        }

        internal static void __Init()
        {
            _module = NativeImplClient.GetModule("First");
            // assign module handles
            _neatoMethod = NativeImplClient.GetModuleMethod(_module, "neatoMethod");
            _returnsArray = NativeImplClient.GetModuleMethod(_module, "returnsArray");

            // no static init
        }

        internal static void __Shutdown()
        {
            // no static shutdown
        }
    }
}
