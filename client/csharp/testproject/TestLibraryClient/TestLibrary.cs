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
        private static ModuleMethodHandle _windowShow;
        private static ModuleMethodHandle _windowSetMenuBar;
        private static ModuleMethodHandle _windowInvalidate;
        private static ModuleMethodHandle _windowDestroy;
        private static ModuleMethodHandle _createAccelerator;
        private static ModuleMethodHandle _createAction;
        private static ModuleMethodHandle _createMenu;
        private static ModuleMethodHandle _menuAddAction;
        private static ModuleMethodHandle _menuAddSubmenu;
        private static ModuleMethodHandle _menuAddSeparator;
        private static ModuleMethodHandle _createMenuBar;
        private static ModuleMethodHandle _menuBarAddMenu;
        private static ModuleMethodHandle _cgContextSetRGBFillColor;
        private static ModuleMethodHandle _cgContextFillRect;
        private static ModuleMethodHandle _runloop;
        private static ModuleMethodHandle _exitRunloop;
        private static ModuleMethodHandle _moduleInit;
        private static ModuleMethodHandle _moduleShutdown;
        private static InterfaceHandle _windowDelegate;
        private static InterfaceMethodHandle _windowDelegate_onDestroyed;
        private static InterfaceMethodHandle _windowDelegate_onResized;
        private static InterfaceMethodHandle _windowDelegate_onRepaint;

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

        public struct CGContext
        {
            internal readonly IntPtr NativeHandle;
            internal CGContext(IntPtr nativeHandle)
            {
                NativeHandle = nativeHandle;
            }
        }

        public struct CGPoint {
            public double X;
            public double Y;
            public CGPoint(double x, double y)
            {
                this.X = x;
                this.Y = y;
            }
        }

        private static void CGPoint__Push(CGPoint value, bool isReturn)
        {
            NativeImplClient.PushDouble(value.Y);
            NativeImplClient.PushDouble(value.X);
        }

        private static CGPoint CGPoint__Pop()
        {
            var x = NativeImplClient.PopDouble();
            var y = NativeImplClient.PopDouble();
            return new CGPoint(x, y);
        }

        public struct CGSize {
            public double Width;
            public double Height;
            public CGSize(double width, double height)
            {
                this.Width = width;
                this.Height = height;
            }
        }

        private static void CGSize__Push(CGSize value, bool isReturn)
        {
            NativeImplClient.PushDouble(value.Height);
            NativeImplClient.PushDouble(value.Width);
        }

        private static CGSize CGSize__Pop()
        {
            var width = NativeImplClient.PopDouble();
            var height = NativeImplClient.PopDouble();
            return new CGSize(width, height);
        }

        public struct CGRect {
            public CGPoint Origin;
            public CGSize Size;
            public CGRect(CGPoint origin, CGSize size)
            {
                this.Origin = origin;
                this.Size = size;
            }
        }

        private static void CGRect__Push(CGRect value, bool isReturn)
        {
            CGSize__Push(value.Size, isReturn);
            CGPoint__Push(value.Origin, isReturn);
        }

        private static CGRect CGRect__Pop()
        {
            var origin = CGPoint__Pop();
            var size = CGSize__Pop();
            return new CGRect(origin, size);
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

        public struct Menu
        {
            internal readonly IntPtr NativeHandle;
            internal Menu(IntPtr nativeHandle)
            {
                NativeHandle = nativeHandle;
            }
        }

        public struct MenuBar
        {
            internal readonly IntPtr NativeHandle;
            internal MenuBar(IntPtr nativeHandle)
            {
                NativeHandle = nativeHandle;
            }
        }

        public struct Window
        {
            internal readonly IntPtr NativeHandle;
            internal Window(IntPtr nativeHandle)
            {
                NativeHandle = nativeHandle;
            }
        }


        public interface WindowDelegate : IDisposable
        {
            void OnDestroyed();
            void OnResized(int newWidth, int newHeight);
            void OnRepaint(CGContext context);
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
            public abstract void OnResized(int newWidth, int newHeight);
            public abstract void OnRepaint(CGContext context);
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

            public void OnResized(int newWidth, int newHeight)
            {
                NativeImplClient.PushInt(newHeight);
                NativeImplClient.PushInt(newWidth);
                NativeImplClient.InvokeInterfaceMethod(_windowDelegate_onResized, Id);
            }

            public void OnRepaint(CGContext context)
            {
                NativeImplClient.PushSizeT(context.NativeHandle);
                NativeImplClient.InvokeInterfaceMethod(_windowDelegate_onRepaint, Id);
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
            return new Window(NativeImplClient.PopSizeT());
        }

        public static void WindowShow(Window w)
        {
            NativeImplClient.PushSizeT(w.NativeHandle);
            NativeImplClient.InvokeModuleMethod(_windowShow);
        }

        public static void WindowSetMenuBar(Window w, MenuBar mb)
        {
            NativeImplClient.PushSizeT(mb.NativeHandle);
            NativeImplClient.PushSizeT(w.NativeHandle);
            NativeImplClient.InvokeModuleMethod(_windowSetMenuBar);
        }

        public static void WindowInvalidate(Window w, int x, int y, int width, int height)
        {
            NativeImplClient.PushInt(height);
            NativeImplClient.PushInt(width);
            NativeImplClient.PushInt(y);
            NativeImplClient.PushInt(x);
            NativeImplClient.PushSizeT(w.NativeHandle);
            NativeImplClient.InvokeModuleMethod(_windowInvalidate);
        }

        public static void WindowDestroy(Window w)
        {
            NativeImplClient.PushSizeT(w.NativeHandle);
            NativeImplClient.InvokeModuleMethod(_windowDestroy);
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
            return new Menu(NativeImplClient.PopSizeT());
        }

        public static void MenuAddAction(Menu menu, Action action)
        {
            NativeImplClient.PushSizeT(action.NativeHandle);
            NativeImplClient.PushSizeT(menu.NativeHandle);
            NativeImplClient.InvokeModuleMethod(_menuAddAction);
        }

        public static void MenuAddSubmenu(Menu menu, string label, Menu sub)
        {
            NativeImplClient.PushSizeT(sub.NativeHandle);
            NativeImplClient.PushString(label);
            NativeImplClient.PushSizeT(menu.NativeHandle);
            NativeImplClient.InvokeModuleMethod(_menuAddSubmenu);
        }

        public static void MenuAddSeparator(Menu menu)
        {
            NativeImplClient.PushSizeT(menu.NativeHandle);
            NativeImplClient.InvokeModuleMethod(_menuAddSeparator);
        }

        public static MenuBar CreateMenuBar()
        {
            NativeImplClient.InvokeModuleMethod(_createMenuBar);
            return new MenuBar(NativeImplClient.PopSizeT());
        }

        public static void MenuBarAddMenu(MenuBar mb, string label, Menu menu)
        {
            NativeImplClient.PushSizeT(menu.NativeHandle);
            NativeImplClient.PushString(label);
            NativeImplClient.PushSizeT(mb.NativeHandle);
            NativeImplClient.InvokeModuleMethod(_menuBarAddMenu);
        }

        public static void CgContextSetRGBFillColor(CGContext c, double red, double green, double blue, double alpha)
        {
            NativeImplClient.PushDouble(alpha);
            NativeImplClient.PushDouble(blue);
            NativeImplClient.PushDouble(green);
            NativeImplClient.PushDouble(red);
            NativeImplClient.PushSizeT(c.NativeHandle);
            NativeImplClient.InvokeModuleMethod(_cgContextSetRGBFillColor);
        }

        public static void CgContextFillRect(CGContext c, CGRect rect)
        {
            CGRect__Push(rect, false);
            NativeImplClient.PushSizeT(c.NativeHandle);
            NativeImplClient.InvokeModuleMethod(_cgContextFillRect);
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
            _windowShow = NativeImplClient.GetModuleMethod(_module, "windowShow");
            _windowSetMenuBar = NativeImplClient.GetModuleMethod(_module, "windowSetMenuBar");
            _windowInvalidate = NativeImplClient.GetModuleMethod(_module, "windowInvalidate");
            _windowDestroy = NativeImplClient.GetModuleMethod(_module, "windowDestroy");
            _createAccelerator = NativeImplClient.GetModuleMethod(_module, "createAccelerator");
            _createAction = NativeImplClient.GetModuleMethod(_module, "createAction");
            _createMenu = NativeImplClient.GetModuleMethod(_module, "createMenu");
            _menuAddAction = NativeImplClient.GetModuleMethod(_module, "menuAddAction");
            _menuAddSubmenu = NativeImplClient.GetModuleMethod(_module, "menuAddSubmenu");
            _menuAddSeparator = NativeImplClient.GetModuleMethod(_module, "menuAddSeparator");
            _createMenuBar = NativeImplClient.GetModuleMethod(_module, "createMenuBar");
            _menuBarAddMenu = NativeImplClient.GetModuleMethod(_module, "menuBarAddMenu");
            _cgContextSetRGBFillColor = NativeImplClient.GetModuleMethod(_module, "cgContextSetRGBFillColor");
            _cgContextFillRect = NativeImplClient.GetModuleMethod(_module, "cgContextFillRect");
            _runloop = NativeImplClient.GetModuleMethod(_module, "runloop");
            _exitRunloop = NativeImplClient.GetModuleMethod(_module, "exitRunloop");
            _moduleInit = NativeImplClient.GetModuleMethod(_module, "moduleInit");
            _moduleShutdown = NativeImplClient.GetModuleMethod(_module, "moduleShutdown");

            _windowDelegate = NativeImplClient.GetInterface(_module, "WindowDelegate");
            _windowDelegate_onDestroyed = NativeImplClient.GetInterfaceMethod(_windowDelegate, "onDestroyed");
            _windowDelegate_onResized = NativeImplClient.GetInterfaceMethod(_windowDelegate, "onResized");
            _windowDelegate_onRepaint = NativeImplClient.GetInterfaceMethod(_windowDelegate, "onRepaint");

            NativeImplClient.SetClientMethodWrapper(_windowDelegate_onDestroyed, delegate(ClientObject obj)
            {
                var inst = (ClientWindowDelegate) obj;
                inst.OnDestroyed();
            });

            NativeImplClient.SetClientMethodWrapper(_windowDelegate_onResized, delegate(ClientObject obj)
            {
                var inst = (ClientWindowDelegate) obj;
                var newWidth = NativeImplClient.PopInt();
                var newHeight = NativeImplClient.PopInt();
                inst.OnResized(newWidth, newHeight);
            });

            NativeImplClient.SetClientMethodWrapper(_windowDelegate_onRepaint, delegate(ClientObject obj)
            {
                var inst = (ClientWindowDelegate) obj;
                var context = new CGContext(NativeImplClient.PopSizeT());
                inst.OnRepaint(context);
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
