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

        private static void __Int_String_Result__Push(Result<int,string> result, bool isReturn)
        {
            if (result.IsSuccess)
            {
                NativeImplClient.PushInt32(result.Value);
                NativeImplClient.PushBool(true);
            }
            else
            {
                NativeImplClient.PushString(result.Error);
                NativeImplClient.PushBool(false);
            }
        }
        private static Result<int,string> __Int_String_Result__Pop()
        {
            var success = NativeImplClient.PopBool();
            if (success)
            {
                return NativeImplClient.PopInt32();
            }
            return NativeImplClient.PopString();
        }

        private static void __Int_String_Result_Array__Push(Result<int, string>[] values, bool isReturn)
        {
            var isSuccessArray = values.Select(v => v.IsSuccess).ToArray();
            var successValues = values.Where(v => v.IsSuccess).Select(v => v.Value).ToArray();
            var errorValues = values.Where(v => v.IsFailure).Select(v => v.Error).ToArray();
            NativeImplClient.PushStringArray(errorValues);
            NativeImplClient.PushInt32Array(successValues);
            NativeImplClient.PushBoolArray(isSuccessArray);
        }

        private static Result<int, string>[] __Int_String_Result_Array__Pop()
        {
            var isSuccessArray = NativeImplClient.PopBoolArray();
            var successValues = NativeImplClient.PopInt32Array();
            var errorValues = NativeImplClient.PopStringArray();
            var successPtr = 0;
            var errorPtr = 0;
            return isSuccessArray.Select<bool, Result<int, string>>(isSuccess =>
            {
                if (isSuccess)
                {
                    return successValues[successPtr++];
                }
                else
                {
                    return errorValues[errorPtr++];
                }
            }).ToArray();
        }

        internal static ModuleMethodHandle _method1;
        internal static ModuleMethodHandle _method2;

        public static void Method1(Result<int,string>[] resultArray)
        {
            __Int_String_Result_Array__Push(resultArray, false);
            NativeImplClient.InvokeModuleMethod(_method1);
        }

        public static Result<int,string>[] Method2()
        {
            NativeImplClient.InvokeModuleMethod(_method2);
            return __Int_String_Result_Array__Pop();
        }

        internal static void __Init()
        {
            _module = NativeImplClient.GetModule("First");
            // assign module handles
            _method1 = NativeImplClient.GetModuleMethod(_module, "method1");
            _method2 = NativeImplClient.GetModuleMethod(_module, "method2");

            // no static init
        }

        internal static void __Shutdown()
        {
            // no static shutdown
        }
    }
}
