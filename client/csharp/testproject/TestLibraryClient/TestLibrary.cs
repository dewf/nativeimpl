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
        private static ModuleMethodHandle _createWindow;
        private static ModuleMethodHandle _runloop;
        private static ModuleMethodHandle _exitRunloop;
        private static ModuleMethodHandle _moduleInit;
        private static ModuleMethodHandle _moduleShutdown;
        private static InterfaceHandle _windowDelegate;
        private static InterfaceMethodHandle _windowDelegate_onDestroyed;
        private static InterfaceHandle _window;
        private static InterfaceMethodHandle _window_show;


        public interface Window : IDisposable
        {
            void Show();
        }

        private static Window Window__Pop()
        {
            NativeImplClient.PopInstanceId(out var id, out var isClientId);
            if (!isClientId)
            {
                return new ServerWindow(id);
            }
            else
            {
                return (Window) ClientObject.GetById(id);
            }
        }

        public abstract class ClientWindow : ClientObject, Window
        {
            public virtual void Dispose()
            {
                // override if necessary
            }
            public abstract void Show();
        }

        private class ServerWindow : ServerObject, Window
        {
            public ServerWindow(int id) : base(id)
            {
            }

            public void Show()
            {
                NativeImplClient.InvokeInterfaceMethod(_window_show, Id);
            }

            public void Dispose()
            {
                ServerDispose();
            }
        }


        public interface WindowDelegate : IDisposable
        {
            void OnDestroyed();
        }

        private static WindowDelegate WindowDelegate__Pop()
        {
            NativeImplClient.PopInstanceId(out var id, out var isClientId);
            if (!isClientId)
            {
                return new ServerWindowDelegate(id);
            }
            else
            {
                return (WindowDelegate) ClientObject.GetById(id);
            }
        }

        public abstract class ClientWindowDelegate : ClientObject, WindowDelegate
        {
            public virtual void Dispose()
            {
                // override if necessary
            }
            public abstract void OnDestroyed();
        }

        private class ServerWindowDelegate : ServerObject, WindowDelegate
        {
            public ServerWindowDelegate(int id) : base(id)
            {
            }

            public void OnDestroyed()
            {
                NativeImplClient.InvokeInterfaceMethod(_windowDelegate_onDestroyed, Id);
            }

            public void Dispose()
            {
                ServerDispose();
            }
        }

        public static Window CreateWindow(string title, WindowDelegate del)
        {
            ((IPushable)del).Push(false);
            NativeImplClient.PushString(title);
            NativeImplClient.InvokeModuleMethod(_createWindow);
            var __ret = Window__Pop();
            NativeImplClient.ServerClearSafetyArea();
            return __ret;
        }

        public static int Runloop()
        {
            NativeImplClient.InvokeModuleMethod(_runloop);
            return NativeImplClient.PopInt();
        }

        public static void ExitRunloop()
        {
            NativeImplClient.InvokeModuleMethod(_exitRunloop);
        }

        public static int ModuleInit()
        {
            NativeImplClient.InvokeModuleMethod(_moduleInit);
            return NativeImplClient.PopInt();
        }

        public static void ModuleShutdown()
        {
            NativeImplClient.InvokeModuleMethod(_moduleShutdown);
        }

        public static int Init()
        {
            Debug.Assert(NativeImplClient.Init() == 0);
            _module = NativeImplClient.GetModule("TestLibrary");

            _createWindow = NativeImplClient.GetModuleMethod(_module, "createWindow");
            _runloop = NativeImplClient.GetModuleMethod(_module, "runloop");
            _exitRunloop = NativeImplClient.GetModuleMethod(_module, "exitRunloop");
            _moduleInit = NativeImplClient.GetModuleMethod(_module, "moduleInit");
            _moduleShutdown = NativeImplClient.GetModuleMethod(_module, "moduleShutdown");

            _windowDelegate = NativeImplClient.GetInterface(_module, "WindowDelegate");
            _windowDelegate_onDestroyed = NativeImplClient.GetInterfaceMethod(_windowDelegate, "onDestroyed");

            NativeImplClient.SetClientMethodWrapper(_windowDelegate_onDestroyed, delegate(ClientObject obj)
            {
                var inst = (ClientWindowDelegate) obj;
                inst.OnDestroyed();
            });

            _window = NativeImplClient.GetInterface(_module, "Window");
            _window_show = NativeImplClient.GetInterfaceMethod(_window, "show");

            NativeImplClient.SetClientMethodWrapper(_window_show, delegate(ClientObject obj)
            {
                var inst = (ClientWindow) obj;
                inst.Show();
            });
            return ModuleInit();
        }

        public static void Shutdown()
        {
            ModuleShutdown();
            NativeImplClient.Shutdown();
        }

        public static void DumpTables()
        {
            NativeImplClient.DumpTables();
        }
    }
}
