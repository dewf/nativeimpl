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
        internal static ModuleMethodHandle _doSomething;

        private static void __Result_int_string__Push(Result<int, string> result)
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

        private static Result<int, string> __Result_int_string__Pop()
        {
            var success = NativeImplClient.PopBool();
            if (success)
            {
                return NativeImplClient.PopInt32();
            }
            return NativeImplClient.PopString();
        }

        private static void __Option_string__Push(Maybe<string> maybeString)
        {
            if (maybeString.TryGetValue(out var value))
            {
                NativeImplClient.PushString(value);
            }
            NativeImplClient.PushBool(maybeString.HasValue);
        }
        private static Maybe<string> __Option_string__Pop()
        {
            var hasValue = NativeImplClient.PopBool();
            if (hasValue)
            {
                return NativeImplClient.PopString();
            }
            return Maybe<string>.None;
        }

        public static void DoSomething(Maybe<string> maybeString, Result<int,string> result)
        {
            __Result_int_string__Push(result);
            __Option_string__Push(maybeString);
            NativeImplClient.InvokeModuleMethod(_doSomething);
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
