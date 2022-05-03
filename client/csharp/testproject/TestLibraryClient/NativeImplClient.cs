using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace TestLibraryClient
{
    public class NativeHandle
    {
        internal IntPtr Handle;
        protected NativeHandle(IntPtr handle)
        {
            Handle = handle;
        }
    }

    public class ModuleHandle : NativeHandle
    {
        internal ModuleHandle(IntPtr handle) : base(handle)
        {
        }
    }

    public class ModuleMethodHandle : NativeHandle
    {
        internal ModuleMethodHandle(IntPtr handle) : base(handle)
        {
        }
    }

    public class InterfaceHandle : NativeHandle
    {
        internal InterfaceHandle(IntPtr handle) : base(handle)
        {
        }
    }

    public class InterfaceMethodHandle : NativeHandle
    {
        internal InterfaceMethodHandle(IntPtr handle) : base(handle)
        {
        }
    }

    public class ExceptionHandle : NativeHandle
    {
        internal ExceptionHandle(IntPtr handle) : base(handle)
        {
        }
    }

    public static class NativeImplClient
    {
        // these MUST be saved, else GC will take the delegates out from under us. ohnononono!
        private static NativeMethods.ExecFunctionDelegate _savedExecFunctionDelegate;
        private static NativeMethods.ReleaseFunctionDelegate _savedReleaseFunctionDelegate;
        private static NativeMethods.ClientMethodDelegate _savedClientMethodDelegate;
        private static NativeMethods.ClientObjReleaseDelegate _savedClientObjReleaseDelegate;
        private static NativeMethods.ClientClearSafetyAreaDelegate _savedClientClearSafetyAreaDelegate;

        // TODO: all this below won't work with threads ...
        private static bool _remoteIsShutdown = false;

        private static readonly Dictionary<IntPtr, int> ReverseFuncValDict = new ();

        private static int _nextClientResourceId = 1;
        private static readonly Dictionary<int, ClientResource> ClientResources = new();
        public delegate void MethodWrapper(ClientObject obj);
        private static readonly Dictionary<IntPtr, MethodWrapper> MethodWrappers = new();

        private static readonly List<ServerResource> SafeArea = new(); // place to stash server objects until the other side has finished popping them from stack (in case they only exist as a return value)

        public delegate void ExceptionBuildAndThrowDelegate();
        private static readonly Dictionary<IntPtr, ExceptionBuildAndThrowDelegate> ExceptionBuildAndThrowDelegates = new();

        public delegate void FuncValWrapper();
        private class ClientFuncVal : ClientResource
        {
            public readonly FuncValWrapper Wrapper;
            private readonly IntPtr _key;
            private ClientFuncVal(FuncValWrapper wrapper, IntPtr key)
            {
                Wrapper = wrapper;
                _key = key;
            }
            protected override void NativePush()
            {
                NativeMethods.pushClientFunc(Id);
            }
            protected override void ReleaseExtra()
            {
                ReverseFuncValDict.Remove(_key);
            }
            public static ClientFuncVal AddFuncVal(FuncValWrapper wrapper, IntPtr uniqueKey)
            {
                Console.WriteLine($"Func Val Unique Key: {uniqueKey}");
                if (ReverseFuncValDict.ContainsKey(uniqueKey))
                {
                    // return existing
                    var id = ReverseFuncValDict[uniqueKey];
                    return (ClientFuncVal)ClientResources[id];
                }
                // else create anew
                var clientFunc = new ClientFuncVal(wrapper, uniqueKey);
                ReverseFuncValDict[uniqueKey] = clientFunc.Id;
                return clientFunc;
            }
        }
        public static void PushClientFuncVal(FuncValWrapper wrapper, IntPtr uniqueKey)
        {
            var funcVal = ClientFuncVal.AddFuncVal(wrapper, uniqueKey);
            ((IPushable)funcVal).Push(false); // isReturn = false (more correctly, "not applicable") - client resources don't need to be protected on return
        }
        private static void FuncValExecutor(int id)
        {
            ((ClientFuncVal)ClientResources[id]).Wrapper();
        }
        private static void ReleaseFuncVal(int id)
        {
            ClientResources[id].Release();
        }

        public static int Init()
        {
            // delegates must be saved from GC until the end of the program, kthx
            _savedExecFunctionDelegate = FuncValExecutor;
            _savedReleaseFunctionDelegate = ReleaseFuncVal;
            _savedClientMethodDelegate = InvokeClientMethod;
            _savedClientObjReleaseDelegate = ReleaseClientObjectId;
            _savedClientClearSafetyAreaDelegate = ClientClearSafetyArea;

            return NativeMethods.nativeImplInit(
                _savedExecFunctionDelegate,
                _savedReleaseFunctionDelegate,
                _savedClientMethodDelegate,
                _savedClientObjReleaseDelegate,
                _savedClientClearSafetyAreaDelegate
                );
        }

        public static void Shutdown()
        {
            _remoteIsShutdown = true; // don't free any remote delegates after this!
            Console.WriteLine("C# NativeImplCLient Shutdown (before native)");
            NativeMethods.nativeImplShutdown();
            Console.WriteLine("C# NativeImplCLient Shutdown (after native)");
        }

        public static ModuleHandle GetModule(string name)
        {
            return new ModuleHandle(NativeMethods.getModule(name));
        }

        public static ModuleMethodHandle GetModuleMethod(ModuleHandle module, string name)
        {
            return new ModuleMethodHandle(NativeMethods.getModuleMethod(module.Handle, name));
        }

        public static InterfaceHandle GetInterface(ModuleHandle module, string name)
        {
            return new InterfaceHandle(NativeMethods.getInterface(module.Handle, name));
        }

        public static InterfaceMethodHandle GetInterfaceMethod(InterfaceHandle iface, string name)
        {
            return new InterfaceMethodHandle(NativeMethods.getInterfaceMethod(iface.Handle, name));
        }

        public static ExceptionHandle GetException(ModuleHandle module, string name)
        {
            return new ExceptionHandle(NativeMethods.getException(module.Handle, name));
        }

        public static void PushModuleConstants(ModuleHandle module)
        {
            NativeMethods.pushModuleConstants(module.Handle);
        }

        public static void InvokeModuleMethod(ModuleMethodHandle method)
        {
            NativeMethods.invokeModuleMethod(method.Handle);
        }

        public static void InvokeModuleMethodWithExceptions(ModuleMethodHandle method)
        {
            var exceptionHandle = NativeMethods.invokeModuleMethodWithExceptions(method.Handle);
            if (exceptionHandle != IntPtr.Zero)
            {
                ExceptionBuildAndThrowDelegates[exceptionHandle]();
            }
        }

        public static void PushSizeT(IntPtr value)
        {
            NativeMethods.pushSizeT(value);
        }

        public static IntPtr PopSizeT()
        {
            return NativeMethods.popSizeT();
        }

        public static void PushSizeTArray(IntPtr[] values)
        {
            NativeMethods.pushSizeTArray(values, (IntPtr)values.Length);
        }

        public static IntPtr[] PopSizeTArray()
        {
            NativeMethods.popSizeTArray(out var valuesPtr, out var count);
            var ret = new IntPtr[(int)count];
            Marshal.Copy(valuesPtr, ret, 0, (int)count);
            return ret;
        }

        public static void PushBool(bool b)
        {
            NativeMethods.pushBool(b);
        }

        public static bool PopBool()
        {
            return NativeMethods.popBool();
        }

        public static void PushBoolArray(bool[] values)
        {
            var bytes = values.Select(b => (byte)(b ? 1 : 0)).ToArray();
            NativeMethods.pushBoolArray(bytes, (IntPtr)values.Length);
        }

        public static bool[] PopBoolArray()
        {
            NativeMethods.popBoolArray(out var valuesPtr, out var count);
            var ret = new byte[(int)count]; // bool[] won't work for the Copy below
            Marshal.Copy(valuesPtr, ret, 0, (int)count);
            return ret.Select(v => v != 0).ToArray();
        }

        public static void PushInt(int x)
        {
            NativeMethods.pushInt(x);
        }

        public static int PopInt()
        {
            return NativeMethods.popInt();
        }

        public static void PushIntArray(int[] values)
        {
            NativeMethods.pushIntArray(values, (IntPtr)values.Length);
        }

        public static int[] PopIntArray()
        {
            NativeMethods.popIntArray(out var valuesPtr, out var count);
            var ret = new int[(int)count];
            Marshal.Copy(valuesPtr, ret, 0, (int)count);
            return ret;
        }

        public static void PushString(string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            var ptr = Marshal.AllocHGlobal(str.Length);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            NativeMethods.pushString(ptr, (IntPtr)bytes.Length);
            Marshal.FreeHGlobal(ptr);
        }

        public static string PopString()
        {
            NativeMethods.popString(out var ptr, out var length);
            return Marshal.PtrToStringUTF8(ptr, (int)length);
        }

        public static void PushStringArray(string[] strs)
        {
            IntPtr[] pointers = new IntPtr[strs.Length];
            IntPtr[] lengths = new IntPtr[strs.Length];
            for (var i = 0; i < strs.Length; i++)
            {
                var bytes = Encoding.UTF8.GetBytes(strs[i]);
                var ptr = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, ptr, bytes.Length);
                pointers[i] = ptr;
                lengths[i] = (IntPtr)bytes.Length;
            }
            NativeMethods.pushStringArray(pointers, lengths, (IntPtr)strs.Length);
            foreach (var p in pointers)
            {
                Marshal.FreeHGlobal(p);
            }
        }

        public static string[] PopStringArray()
        {
            NativeMethods.popStringArray(out var ptrsPtr, out var lengthsPtr, out var count);
            var intCount = (int)count;
            var ptrs = new IntPtr[intCount];
            var lengths = new IntPtr[intCount];
            Marshal.Copy(ptrsPtr, ptrs, 0, intCount);
            Marshal.Copy(lengthsPtr, lengths, 0, intCount);
            var result = new string[intCount];
            for (var i = 0; i < intCount; i++)
            {
                result[i] = Marshal.PtrToStringUTF8(ptrs[i], (int)lengths[i]);
            }
            return result;
        }
        
        public static int PopServerFuncValId()
        {
            return NativeMethods.popServerFunc();
        }

        public static void PopInstanceId(out int id, out bool isClientId)
        {
            id = NativeMethods.popInstance(out isClientId, out var inSafetyArea); // do something with 'inSafetyArea' ?
        }

        public class ServerFunc : ServerResource
        {
            public ServerFunc(int id) : base(id)
            {
            }
            protected override void NativePush(bool inSafeArea)
            {
                // ServerFuncs currently will never be pushed - we don't support "short-circuiting" of function values,
                // they are always wrapped afresh from each side (so a server function returned from our side,
                // becomes a client function - use a single method interface for that kind of thing)
                throw new NotImplementedException();
            }
            public void Exec()
            {
                NativeMethods.execServerFunc(Id);
            }
            public void ExecWithExceptions()
            {
                var exceptionHandle = NativeMethods.execServerFuncWithExceptions(Id);
                if (exceptionHandle != IntPtr.Zero)
                {
                    ExceptionBuildAndThrowDelegates[exceptionHandle]();
                }
            }
            protected override void CustomRelease() // TODO: get rid of this soon!
            {
                NativeMethods.releaseServerFunc(Id);
            }
        }
        
        public static void InvokeInterfaceMethod(InterfaceMethodHandle method, int serverId)
        {
            NativeMethods.invokeInterfaceMethod(method.Handle, serverId);
        }

        public static void InvokeInterfaceMethodWithExceptions(InterfaceMethodHandle method, int serverId)
        {
            var exceptionHandle = NativeMethods.invokeInterfaceMethodWithExceptions(method.Handle, serverId);
            if (exceptionHandle != IntPtr.Zero)
            {
                ExceptionBuildAndThrowDelegates[exceptionHandle]();
            }
        }

        public static void ReleaseServerInstanceId(int id)
        {
            if (!_remoteIsShutdown)
            {
                NativeMethods.releaseServerInst(id);
            }
        }

        public interface IPushable
        {
            void Push(bool isReturn);
        }

        public abstract class ClientResource : IPushable
        {
            internal int Id { get; private set; } = -1;
            internal int RefCount { get; private set; }
            protected abstract void NativePush();

            protected virtual void ReleaseExtra()
            {
                // anything else you might want to do here, when refcount = 0? (used by ClientFunc to remove from reverse dict)
            }

            void IPushable.Push(bool isReturn) // isReturn unused here
            {
                if (Id == -1)
                {
                    // unset as yet
                    Id = _nextClientResourceId++;
                    RefCount = 1;
                    ClientResources[Id] = this;
                    Console.WriteLine($"C# ClientObject: pushed {Id}");
                }
                else
                {
                    // just increase refcount as we pass it over
                    RefCount++;
                    Console.WriteLine($"C# ClientObject: increased refcount to {RefCount}");
                }
                NativePush();
            }

            public void Release()
            {
                RefCount -= 1;
                if (RefCount == 0)
                {
                    Console.WriteLine($"C# Release on ClientResource {Id} - refCount 0, removing from clientObjects table");
                    ClientResources.Remove(Id);
                    Id = -1; // reset ID, since we're not in the object table anymore
                    // all this does is remove this object from the 'checked-out' table -
                    // things we were keeping alive as long as the remote (server) side had reference to them
                    // but that doesn't mean we're ready to dispose it - might still be useful to us on this side!
                    
                    ReleaseExtra(); // currently just for clientfuncval, which needs to remove the reverse mapping
                }
            }

            protected static ClientResource GetResourceById(int id)
            {
                return ClientResources[id];
            }
        }

        public abstract class ClientObject : ClientResource
        {
            protected override void NativePush()
            {
                NativeMethods.pushInstance(Id, true, false);
            }
            public static ClientObject GetById(int id)
            {
                return (ClientObject)GetResourceById(id);
            }
        }

        public static void SetClientMethodWrapper(InterfaceMethodHandle method, MethodWrapper wrapper)
        {
            MethodWrappers[method.Handle] = wrapper;
        }

        // these two need to be passed to remote
        private static void ReleaseClientObjectId(int id) // called from remote
        {
            // should we check for _remoteIsShutdown here?
            ClientResources[id].Release();
        }
        private static void InvokeClientMethod(IntPtr methodHandle, int clientId)
        {
            var obj = ClientObject.GetById(clientId);
            MethodWrappers[methodHandle](obj);
        }

        public abstract class ServerResource : IPushable
        {
            protected readonly int Id;
            private bool _remoteReleased;
            protected ServerResource(int id)
            {
                Id = id;
            }

            ~ServerResource()
            {
                // backstop in case the derived class not manually disposed
                Console.WriteLine($"ServerResource id {Id} - dtor invoked, calling ServerDispose()");
                ServerDispose();
            }

            protected abstract void NativePush(bool inSafeArea);
            protected virtual void CustomRelease() // TODO: only keep until we've fixed up both sides to use the new resources for funcvals, then don't need this anymore
            {
                ReleaseServerInstanceId(Id);
            }
            
            protected void ServerDispose()
            {
                if (_remoteReleased || _remoteIsShutdown) return;
                CustomRelease();
                _remoteReleased = true;
            }

            private void MakeSafe()
            {
                SafeArea.Add(this);
            }

            void IPushable.Push(bool isReturn)
            {
                if (isReturn)
                {
                    // save this object to the stack safety area, until the other side has pulled it off
                    // otherwise, if this is the last proxy on this side,
                    // could remotely detonate the server's real copy before it's done taking 'ownership'
                    // (dependent on probably-unlikely GC collection, but better safe than sorry)
                    MakeSafe();
                }
                NativePush(isReturn);
            }
        }

        public abstract class ServerObject : ServerResource
        {
            protected ServerObject(int id) : base(id)
            {
            }
            protected override void NativePush(bool inSafeArea)
            {
                NativeMethods.pushInstance(Id, false, inSafeArea);
            }
        }

        // call this after receiving an interface (maybe even a function, eventually?) as a return value
        public static void ServerClearSafetyArea()
        {
            // TODO: in the future, perhaps check to see if the remote safety area is 'dirty' -- we'll know from the flags returned on popInstace()
            // (have given that a lot of thought and will probably go in the direction of not even having the 'inSafetyArea' flag on resource pops,
            // could potentially require lots of branching for return types with lots of resource refs, is it really worth it?
            NativeMethods.clearServerSafetyArea();
        }

        private static void ClientClearSafetyArea()
        {
            Console.WriteLine("C#: before ClientClearSafetyArea");
            SafeArea.Clear();
            Console.WriteLine("C#: after ClientClearSafetyArea");
        }
        
        public static void DumpTables()
        {
            Console.WriteLine("============Dump Tables==================");
            Console.WriteLine("Native tables----------------------------");
            NativeMethods.dumpTables();
            
            Console.WriteLine("Local tables-----------------------------");
            
            // Console.WriteLine("== C# server func vals:");
            // foreach (var (key, value) in FuncValDict)
            // {
            //     Console.WriteLine($" - fun {key}: refcount {value.RefCount}");
            // }

            Console.WriteLine("== C# client resources:");
            foreach (var (key, value) in ClientResources)
            {
                Console.WriteLine($" - obj {value.Id}: refcount {value.RefCount}");
            }
            
            Console.WriteLine("=========================================");
        }

        public static void SetExceptionBuilder(ExceptionHandle eh, ExceptionBuildAndThrowDelegate builder)
        {
            ExceptionBuildAndThrowDelegates[eh.Handle] = builder;
        }
        public static void SetException(ExceptionHandle eh)
        {
            NativeMethods.setException(eh.Handle);
        }
    }
}
