using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

    internal static class NativeMethods
    {
        internal delegate void ExecFunctionDelegate(int id);
        internal delegate void ReleaseFunctionDelegate(int id);
        internal delegate void ClientMethodDelegate(IntPtr methodHandle, int objectId);
        internal delegate void ClientObjReleaseDelegate(int objectId);
        internal delegate void ClientClearSafetyAreaDelegate();

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int nativeImplInit(
            ExecFunctionDelegate execFunctionDelegate,
            ReleaseFunctionDelegate releaseFunctionDelegate,
            ClientMethodDelegate methodDelegate,
            ClientObjReleaseDelegate objReleaseDelegate,
            ClientClearSafetyAreaDelegate clientClearSafetyAreaDelegate
            );

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void nativeImplShutdown();
        
        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr getModule(
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Utf8Marshaler))]
            string name);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr getModuleMethod(
            IntPtr module,
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Utf8Marshaler))]
            string name);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr getInterface(
            IntPtr module,
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Utf8Marshaler))]
            string name);
        
        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr getInterfaceMethod(
            IntPtr iface,
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Utf8Marshaler))]
            string name);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr getException(
            IntPtr module,
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Utf8Marshaler))]
            string name);
        
        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void invokeModuleMethod(IntPtr method);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr invokeModuleMethodWithExceptions(IntPtr method);
        
        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void pushBool(bool value);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool popBool();

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void pushInt(int x);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int popInt();

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void pushClientFunc(int id);

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int popServerFunc();

        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void execServerFunc(int id);
        
        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void releaseServerFunc(int id);
        
        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void invokeInterfaceMethod(IntPtr method, int serverId);
        
        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr invokeInterfaceMethodWithExceptions(IntPtr method, int serverId);
        
        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int popInstance(out bool isClientId);
        
        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int releaseServerInst(int id);
        
        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void pushClientInst(int id);
        
        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void pushServerInst(int id);
        
        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void clearServerSafetyArea();
        
        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void dumpTables();
        
        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void pushModuleConstants(IntPtr module);
        
        [DllImport("TestLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void setException(IntPtr exceptionHandle);
    }

    public static class NativeImplClient
    {
        // these MUST be saved, else GC will take the delegates out from under us. ohnononono!
        private static NativeMethods.ExecFunctionDelegate _savedExecFunctionDelegate;
        private static NativeMethods.ReleaseFunctionDelegate _savedReleaseFunctionDelegate;
        private static NativeMethods.ClientMethodDelegate _savedClientMethodDelegate;
        private static NativeMethods.ClientObjReleaseDelegate _savedClientObjReleaseDelegate;
        private static NativeMethods.ClientClearSafetyAreaDelegate _savedClientClearSafetyAreaDelegate;

        private static bool _remoteIsShutdown = false; // TODO: probably needs to be thread-safe
        
        // callback-related stuff
        public delegate void FuncValWrapper();
        private struct FuncValInfo
        {
            public FuncValWrapper Wrapper;
            public IntPtr Key;
            public int RefCount;
        }
        // TODO: these definitely need to be thread-safe
        private static readonly Dictionary<int, FuncValInfo> FuncValDict = new Dictionary<int, FuncValInfo>();
        private static readonly Dictionary<IntPtr, int> ReverseFuncValDict = new Dictionary<IntPtr, int>();
        private static int _nextFuncValId = 1;
        private static void FuncValExecutor(int id)
        {
            FuncValDict[id].Wrapper();
        }
        private static void ReleaseFuncVal(int id)
        {
            Console.WriteLine($"C# ReleaseFuncVal {id}");
            // decrement
            var info = FuncValDict[id];
            info.RefCount -= 1;
            if (info.RefCount == 0)
            {
                // truly delete
                ReverseFuncValDict.Remove(info.Key);
                FuncValDict.Remove(id);
                Console.WriteLine("C# callback {0} removed", id);
            }
            else
            {
                FuncValDict[id] = info; // store w/ updated refcount
                Console.WriteLine($" - updated refcount: {info.RefCount}");
            }
        }
        private static int AddFuncVal(FuncValWrapper wrapper, IntPtr uniqueKey)
        {
            Console.WriteLine($"Func Val Unique Key: {uniqueKey}");
            // insert if this delegate unique
            if (!ReverseFuncValDict.ContainsKey(uniqueKey))
            {
                var id = _nextFuncValId++;
                FuncValDict[id] = new FuncValInfo { Wrapper = wrapper, Key = uniqueKey, RefCount = 1 };
                ReverseFuncValDict[uniqueKey] = id;
                return id;
            }
            // else increment the refcount of existing
            var id2 = ReverseFuncValDict[uniqueKey];
            var fv = FuncValDict[id2];
            fv.RefCount += 1;
            FuncValDict[id2] = fv;
            Console.WriteLine($" - AddFuncVal refcount: {fv.RefCount}");
            return id2;
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
                throw ExceptionBuilderDelegates[exceptionHandle]();
            }
        }

        public static void PushBool(bool b)
        {
            NativeMethods.pushBool(b);
        }

        public static bool PopBool()
        {
            return NativeMethods.popBool();
        }

        public static void PushInt(int x)
        {
            NativeMethods.pushInt(x);
        }

        public static int PopInt()
        {
            return NativeMethods.popInt();
        }
        
        public static void PushClientFuncVal(FuncValWrapper del, IntPtr key)
        {
            var id = AddFuncVal(del, key);
            Console.WriteLine($"AddFuncVal id: {id}");
            NativeMethods.pushClientFunc(id);
        }
        
        public static int PopServerFuncValId()
        {
            return NativeMethods.popServerFunc();
        }

        public static void PopInstanceId(out int id, out bool isClientId)
        {
            id = NativeMethods.popInstance(out isClientId);
        }
        
        public class ServerFunc
        {
            private readonly int _id;
            public ServerFunc(int id)
            {
                _id = id;
            }
            ~ServerFunc()
            {
                // verify remote not shut down to avoid problems
                if (!_remoteIsShutdown)
                {
                    NativeMethods.releaseServerFunc(_id);
                }
            }
            public void Exec()
            {
                NativeMethods.execServerFunc(_id);
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
                throw ExceptionBuilderDelegates[exceptionHandle]();
            }
        }

        public static void ReleaseServerInstanceId(int id)
        {
            if (!_remoteIsShutdown)
            {
                NativeMethods.releaseServerInst(id);
            }
        }

        public static void PushServerInstanceId(int id)
        {
            NativeMethods.pushServerInst(id);
        }

        public interface IPushable
        {
            void Push(bool isReturn);
        }

        public abstract class ClientObject : IPushable
        {
            private int _id = -1;
            private int _refCount = 0;

            internal int Id => _id;
            internal int RefCount => _refCount;

            void IPushable.Push(bool isReturn) // isReturn unused here
            {
                if (_id == -1)
                {
                    // unset as yet
                    _id = _nextClientObjectId++;
                    _refCount = 1;
                    ClientObjects[_id] = this;
                    Console.WriteLine($"C# ClientObject: pushed {_id}");
                }
                else
                {
                    // just increase refcount as we pass it over
                    _refCount++;
                    Console.WriteLine($"C# ClientObject: increased refcount to {_refCount}");
                }
                // actually push, durr
                NativeMethods.pushClientInst(_id);
            }

            public void Release()
            {
                _refCount -= 1;
                if (_refCount == 0)
                {
                    Console.WriteLine($"C# ReleaseClientObjectID {_id} - refCount 0, removing from clientObjects table");
                    ClientObjects.Remove(_id);
                    _id = -1; // reset ID, since we're not in the object table anymore
                    // all this does is remove this object from the 'checked-out' table -
                    // things we were keeping alive as long as the remote (server) side had reference to them
                    // but that doesn't mean we're ready to dispose it - might still be useful to us on this side!
                }
            }
            public static ClientObject GetById(int id)
            {
                return ClientObjects[id];
            }
        }
        // TODO: these definitely need to be thread-safe
        private static int _nextClientObjectId = 1;
        private static readonly Dictionary<int, ClientObject> ClientObjects = new Dictionary<int, ClientObject>();

        public delegate void MethodWrapper(ClientObject obj);
        private static readonly Dictionary<IntPtr, MethodWrapper> MethodWrappers =
            new Dictionary<IntPtr, MethodWrapper>();
        public static void SetClientMethodWrapper(InterfaceMethodHandle method, MethodWrapper wrapper)
        {
            MethodWrappers[method.Handle] = wrapper;
        }
        
        // these two need to be passed to remote
        private static void ReleaseClientObjectId(int id) // called from remote
        {
            // should we check for _remoteIsShutdown here?
            ClientObjects[id].Release();
        }
        private static void InvokeClientMethod(IntPtr methodHandle, int clientId)
        {
            var obj = ClientObject.GetById(clientId);
            MethodWrappers[methodHandle](obj);
        }

        public class ServerObject : IPushable
        {
            protected readonly int Id;
            private bool _remoteReleased;
            protected ServerObject(int id)
            {
                Id = id;
            }

            ~ServerObject()
            {
                // backstop in case the derived class not manually disposed
                Console.WriteLine($"ServerObject id {Id} - dtor invoked, calling ServerDispose()");
                ServerDispose();
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            protected void ServerDispose()
            {
                if (_remoteReleased) return;
                ReleaseServerInstanceId(Id);
                _remoteReleased = true;
            }

            private void MakeSafe()
            {
                SafeArea.Add(this);
            }

            void IPushable.Push(bool isReturn)
            {
                PushServerInstanceId(Id);
                if (isReturn)
                {
                    // save this object to the stack safety area, until the other side has pulled it off
                    // otherwise, if this is the last proxy on this side,
                    // could remotely detonate the server's real copy before it's done taking 'ownership'
                    // (dependent on probably-unlikely GC collection, but better safe than sorry)
                    MakeSafe();
                }
            }
        }
        // todo: needs to be thread-safe
        private static readonly List<ServerObject> SafeArea = new List<ServerObject>(); // place to stash server objects until the other side has finished popping them from stack (in case they only exist as a return value)

        // call this after receiving an interface (maybe even a function, eventually?) as a return value
        public static void ServerClearSafetyArea()
        {
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
            
            Console.WriteLine("== C# server func vals:");
            foreach (var (key, value) in FuncValDict)
            {
                Console.WriteLine($" - fun {key}: refcount {value.RefCount}");
            }

            Console.WriteLine("== C# server objects:");
            foreach (var (key, value) in ClientObjects)
            {
                Console.WriteLine($" - obj {value.Id}: refcount {value.RefCount}");
            }
            
            Console.WriteLine("=========================================");
        }

        public delegate Exception ExceptionBuilderDelegate();

        private static readonly Dictionary<IntPtr, ExceptionBuilderDelegate> ExceptionBuilderDelegates =
            new Dictionary<IntPtr, ExceptionBuilderDelegate>();
        public static void SetExceptionBuilder(ExceptionHandle eh, ExceptionBuilderDelegate builder)
        {
            ExceptionBuilderDelegates[eh.Handle] = builder;
        }
        public static void SetException(ExceptionHandle eh)
        {
            NativeMethods.setException(eh.Handle);
        }
    }
}
