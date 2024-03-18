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

        private static void __String_Option__Push(Maybe<string> maybeValue, bool isReturn)
        {
            if (maybeValue.TryGetValue(out var value))
            {
                NativeImplClient.PushString(value);
                NativeImplClient.PushBool(true);
            }
            else
            {
                NativeImplClient.PushBool(false);
            }
        }

        private static Maybe<string> __String_Option__Pop()
        {
            var hasValue = NativeImplClient.PopBool();
            if (hasValue)
            {
                return NativeImplClient.PopString();
            }
            return Maybe<string>.None;
        }
        internal static ModuleMethodHandle _doSomething;

        public static void DoSomething(Maybe<string> maybeString, Result<int,string> result)
        {
            __Int_String_Result__Push(result, false);
            __String_Option__Push(maybeString, false);
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
