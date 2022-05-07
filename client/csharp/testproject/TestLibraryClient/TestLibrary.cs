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
        private static ModuleMethodHandle _createAccelerator;
        private static ModuleMethodHandle _createAction;
        private static ModuleMethodHandle _createMenu;
        private static ModuleMethodHandle _createMenuBar;
        private static ModuleMethodHandle _moduleInit;
        private static ModuleMethodHandle _moduleShutdown;
        private static InterfaceHandle _windowDelegate;
        private static InterfaceMethodHandle _windowDelegate_onDestroyed;
        private static InterfaceHandle _window;
        private static InterfaceMethodHandle _window_show;
        private static InterfaceMethodHandle _window_setMenuBar;
        private static InterfaceHandle _menu;
        private static InterfaceMethodHandle _menu_addAction;
        private static InterfaceMethodHandle _menu_addSubmenu;
        private static InterfaceMethodHandle _menu_addSeparator;
        private static InterfaceHandle _menuBar;
        private static InterfaceMethodHandle _menuBar_addMenu;

        public struct Accelerator
        {
            internal readonly IntPtr NativeHandle;
            internal Accelerator(IntPtr nativeHandle)
            {
                NativeHandle = nativeHandle;
            }
        }

        public struct Action
        {
            internal readonly IntPtr NativeHandle;
            internal Action(IntPtr nativeHandle)
            {
                NativeHandle = nativeHandle;
            }
        }

        public enum KeyEnum
        {
            KeyUnknown,
            KeyEscape,
            KeyTab,
            KeyBackspace,
            KeyReturn,
            KeySpace,
            KeyF1,
            KeyF2,
            KeyF3,
            KeyF4,
            KeyF5,
            KeyF6,
            KeyF7,
            KeyF8,
            KeyF9,
            KeyF10,
            KeyF11,
            KeyF12,
            KeyF13,
            KeyF14,
            KeyF15,
            KeyF16,
            KeyF17,
            KeyF18,
            KeyF19,
            Key0,
            Key1,
            Key2,
            Key3,
            Key4,
            Key5,
            Key6,
            Key7,
            Key8,
            Key9,
            KeyA,
            KeyB,
            KeyC,
            KeyD,
            KeyE,
            KeyF,
            KeyG,
            KeyH,
            KeyI,
            KeyJ,
            KeyK,
            KeyL,
            KeyM,
            KeyN,
            KeyO,
            KeyP,
            KeyQ,
            KeyR,
            KeyS,
            KeyT,
            KeyU,
            KeyV,
            KeyW,
            KeyX,
            KeyY,
            KeyZ,
            KeyControl,
            KeyShift,
            KeyAltOption,
            KeyWinCommand,
            KeyFn,
            KeyInsert,
            KeyDelete,
            KeyPageUp,
            KeyPageDown,
            KeyHome,
            KeyEnd,
            KeyLeftArrow,
            KeyUpArrow,
            KeyRightArrow,
            KeyDownArrow,
            KeyKP0,
            KeyKP1,
            KeyKP2,
            KeyKP3,
            KeyKP4,
            KeyKP5,
            KeyKP6,
            KeyKP7,
            KeyKP8,
            KeyKP9,
            KeyKPClear,
            KeyKPEquals,
            KeyKPDivide,
            KeyKPMultiply,
            KeyKPSubtract,
            KeyKPAdd,
            KeyKPEnter,
            KeyKPDecimal,
            KeyCapsLock,
            KeyNumLock,
            KeyScrollLock,
            KeyPrintScreen,
            KeyPause,
            KeyCancel,
            KeyMediaMute,
            KeyMediaVolumeDown,
            KeyMediaVolumeUp,
            KeyMediaNext,
            KeyMediaPrev,
            KeyMediaStop,
            KeyMediaPlayPause
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void KeyEnum__Push(KeyEnum value)
        {
            NativeImplClient.PushInt((int)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static KeyEnum KeyEnum__Pop()
        {
            return (KeyEnum)NativeImplClient.PopInt();
        }



        public interface Menu : IDisposable
        {
            void AddAction(Action action);
            void AddSubmenu(string label, Menu sub);
            void AddSeparator();
        }

        private static Menu Menu__Pop()
        {
            NativeImplClient.PopInstanceId(out var id, out var isClientId);
            if (!isClientId)
            {
                return new ServerMenu(id);
            }
            else
            {
                return (Menu) ClientObject.GetById(id);
            }
        }

        public abstract class ClientMenu : ClientObject, Menu
        {
            public virtual void Dispose()
            {
                // override if necessary
            }
            public abstract void AddAction(Action action);
            public abstract void AddSubmenu(string label, Menu sub);
            public abstract void AddSeparator();
        }

        private class ServerMenu : ServerObject, Menu
        {
            public ServerMenu(int id) : base(id)
            {
            }

            public void AddAction(Action action)
            {
                NativeImplClient.PushSizeT(action.NativeHandle);
                NativeImplClient.InvokeInterfaceMethod(_menu_addAction, Id);
            }

            public void AddSubmenu(string label, Menu sub)
            {
                ((IPushable)sub).Push(false);
                NativeImplClient.PushString(label);
                NativeImplClient.InvokeInterfaceMethod(_menu_addSubmenu, Id);
            }

            public void AddSeparator()
            {
                NativeImplClient.InvokeInterfaceMethod(_menu_addSeparator, Id);
            }

            public void Dispose()
            {
                ServerDispose();
            }
        }


        public interface MenuBar : IDisposable
        {
            void AddMenu(string label, Menu menu);
        }

        private static MenuBar MenuBar__Pop()
        {
            NativeImplClient.PopInstanceId(out var id, out var isClientId);
            if (!isClientId)
            {
                return new ServerMenuBar(id);
            }
            else
            {
                return (MenuBar) ClientObject.GetById(id);
            }
        }

        public abstract class ClientMenuBar : ClientObject, MenuBar
        {
            public virtual void Dispose()
            {
                // override if necessary
            }
            public abstract void AddMenu(string label, Menu menu);
        }

        private class ServerMenuBar : ServerObject, MenuBar
        {
            public ServerMenuBar(int id) : base(id)
            {
            }

            public void AddMenu(string label, Menu menu)
            {
                ((IPushable)menu).Push(false);
                NativeImplClient.PushString(label);
                NativeImplClient.InvokeInterfaceMethod(_menuBar_addMenu, Id);
            }

            public void Dispose()
            {
                ServerDispose();
            }
        }



        public interface Window : IDisposable
        {
            void Show();
            void SetMenuBar(MenuBar mb);
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
            public abstract void SetMenuBar(MenuBar mb);
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

            public void SetMenuBar(MenuBar mb)
            {
                ((IPushable)mb).Push(false);
                NativeImplClient.InvokeInterfaceMethod(_window_setMenuBar, Id);
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

        public static Accelerator CreateAccelerator(KeyEnum key)
        {
            KeyEnum__Push(key);
            NativeImplClient.InvokeModuleMethod(_createAccelerator);
            return new Accelerator(NativeImplClient.PopSizeT());
        }

        public static Action CreateAction(int id, string label, Accelerator acc)
        {
            NativeImplClient.PushSizeT(acc.NativeHandle);
            NativeImplClient.PushString(label);
            NativeImplClient.PushInt(id);
            NativeImplClient.InvokeModuleMethod(_createAction);
            return new Action(NativeImplClient.PopSizeT());
        }

        public static Menu CreateMenu()
        {
            NativeImplClient.InvokeModuleMethod(_createMenu);
            var __ret = Menu__Pop();
            NativeImplClient.ServerClearSafetyArea();
            return __ret;
        }

        public static MenuBar CreateMenuBar()
        {
            NativeImplClient.InvokeModuleMethod(_createMenuBar);
            var __ret = MenuBar__Pop();
            NativeImplClient.ServerClearSafetyArea();
            return __ret;
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
            _createAccelerator = NativeImplClient.GetModuleMethod(_module, "createAccelerator");
            _createAction = NativeImplClient.GetModuleMethod(_module, "createAction");
            _createMenu = NativeImplClient.GetModuleMethod(_module, "createMenu");
            _createMenuBar = NativeImplClient.GetModuleMethod(_module, "createMenuBar");
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
            _window_setMenuBar = NativeImplClient.GetInterfaceMethod(_window, "setMenuBar");

            NativeImplClient.SetClientMethodWrapper(_window_show, delegate(ClientObject obj)
            {
                var inst = (ClientWindow) obj;
                inst.Show();
            });

            NativeImplClient.SetClientMethodWrapper(_window_setMenuBar, delegate(ClientObject obj)
            {
                var inst = (ClientWindow) obj;
                var mb = MenuBar__Pop();
                inst.SetMenuBar(mb);
            });

            _menu = NativeImplClient.GetInterface(_module, "Menu");
            _menu_addAction = NativeImplClient.GetInterfaceMethod(_menu, "addAction");
            _menu_addSubmenu = NativeImplClient.GetInterfaceMethod(_menu, "addSubmenu");
            _menu_addSeparator = NativeImplClient.GetInterfaceMethod(_menu, "addSeparator");

            NativeImplClient.SetClientMethodWrapper(_menu_addAction, delegate(ClientObject obj)
            {
                var inst = (ClientMenu) obj;
                var action = new Action(NativeImplClient.PopSizeT());
                inst.AddAction(action);
            });

            NativeImplClient.SetClientMethodWrapper(_menu_addSubmenu, delegate(ClientObject obj)
            {
                var inst = (ClientMenu) obj;
                var label = NativeImplClient.PopString();
                var sub = Menu__Pop();
                inst.AddSubmenu(label, sub);
            });

            NativeImplClient.SetClientMethodWrapper(_menu_addSeparator, delegate(ClientObject obj)
            {
                var inst = (ClientMenu) obj;
                inst.AddSeparator();
            });

            _menuBar = NativeImplClient.GetInterface(_module, "MenuBar");
            _menuBar_addMenu = NativeImplClient.GetInterfaceMethod(_menuBar, "addMenu");

            NativeImplClient.SetClientMethodWrapper(_menuBar_addMenu, delegate(ClientObject obj)
            {
                var inst = (ClientMenuBar) obj;
                var label = NativeImplClient.PopString();
                var menu = Menu__Pop();
                inst.AddMenu(label, menu);
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
