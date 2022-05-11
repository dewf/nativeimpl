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
        private static ModuleMethodHandle _windowShow;
        private static ModuleMethodHandle _windowSetMenuBar;
        private static ModuleMethodHandle _windowInvalidate;
        private static ModuleMethodHandle _windowDestroy;
        private static ModuleMethodHandle _createWindow;
        private static ModuleMethodHandle _createAccelerator;
        private static ModuleMethodHandle _createAction;
        private static ModuleMethodHandle _menuAddAction;
        private static ModuleMethodHandle _menuAddSubmenu;
        private static ModuleMethodHandle _menuAddSeparator;
        private static ModuleMethodHandle _createMenu;
        private static ModuleMethodHandle _menuBarAddMenu;
        private static ModuleMethodHandle _createMenuBar;
        private static ModuleMethodHandle _cGContextSetRGBFillColor;
        private static ModuleMethodHandle _cGContextFillRect;
        private static ModuleMethodHandle _runloop;
        private static ModuleMethodHandle _exitRunloop;
        private static ModuleMethodHandle _moduleInit;
        private static ModuleMethodHandle _moduleShutdown;
        private static InterfaceHandle _windowDelegate;
        private static InterfaceMethodHandle _windowDelegate_onCloseRequest;
        private static InterfaceMethodHandle _windowDelegate_onDestroyed;
        private static InterfaceMethodHandle _windowDelegate_onResized;
        private static InterfaceMethodHandle _windowDelegate_onRepaint;
        private static InterfaceMethodHandle _windowDelegate_onAction;

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

        public struct CloseRequestReply {
            public bool CancelClose;
            public CloseRequestReply(bool cancelClose)
            {
                this.CancelClose = cancelClose;
            }
        }

        private static void CloseRequestReply__Push(CloseRequestReply value, bool isReturn)
        {
            NativeImplClient.PushBool(value.CancelClose);
        }

        private static CloseRequestReply CloseRequestReply__Pop()
        {
            var cancelClose = NativeImplClient.PopBool();
            return new CloseRequestReply(cancelClose);
        }

        public enum Key
        {
            Unknown,
            Escape,
            Tab,
            Backspace,
            Return,
            Space,
            F1,
            F2,
            F3,
            F4,
            F5,
            F6,
            F7,
            F8,
            F9,
            F10,
            F11,
            F12,
            F13,
            F14,
            F15,
            F16,
            F17,
            F18,
            F19,
            _0,
            _1,
            _2,
            _3,
            _4,
            _5,
            _6,
            _7,
            _8,
            _9,
            A,
            B,
            C,
            D,
            E,
            F,
            G,
            H,
            I,
            J,
            K,
            L,
            M,
            N,
            O,
            P,
            Q,
            R,
            S,
            T,
            U,
            V,
            W,
            X,
            Y,
            Z,
            Control,
            Shift,
            AltOption,
            WinCommand,
            Fn,
            Insert,
            Delete,
            PageUp,
            PageDown,
            Home,
            End,
            LeftArrow,
            UpArrow,
            RightArrow,
            DownArrow,
            KP0,
            KP1,
            KP2,
            KP3,
            KP4,
            KP5,
            KP6,
            KP7,
            KP8,
            KP9,
            KPClear,
            KPEquals,
            KPDivide,
            KPMultiply,
            KPSubtract,
            KPAdd,
            KPEnter,
            KPDecimal,
            CapsLock,
            NumLock,
            ScrollLock,
            PrintScreen,
            Pause,
            Cancel,
            MediaMute,
            MediaVolumeDown,
            MediaVolumeUp,
            MediaNext,
            MediaPrev,
            MediaStop,
            MediaPlayPause
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Key__Push(Key value)
        {
            NativeImplClient.PushInt((int)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Key Key__Pop()
        {
            return (Key)NativeImplClient.PopInt();
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

        public enum Modifier
        {
            Shift,
            Control,
            Alt,
            MacControl
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Modifier__Push(Modifier value)
        {
            NativeImplClient.PushInt((int)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Modifier Modifier__Pop()
        {
            return (Modifier)NativeImplClient.PopInt();
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
            CloseRequestReply OnCloseRequest();
            void OnDestroyed();
            void OnResized(int newWidth, int newHeight);
            void OnRepaint(CGContext context);
            void OnAction(int id, Action action);
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
            public abstract CloseRequestReply OnCloseRequest();
            public abstract void OnDestroyed();
            public abstract void OnResized(int newWidth, int newHeight);
            public abstract void OnRepaint(CGContext context);
            public abstract void OnAction(int id, Action action);
        }

        private class ServerWindowDelegate : ServerObject, WindowDelegate
        {
            public ServerWindowDelegate(int id) : base(id)
            {
            }

            public CloseRequestReply OnCloseRequest()
            {
                NativeImplClient.InvokeInterfaceMethod(_windowDelegate_onCloseRequest, Id);
                return CloseRequestReply__Pop();
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

            public void OnAction(int id, Action action)
            {
                NativeImplClient.PushSizeT(action.NativeHandle);
                NativeImplClient.PushInt(id);
                NativeImplClient.InvokeInterfaceMethod(_windowDelegate_onAction, Id);
            }

            public void Dispose()
            {
                ServerDispose();
            }
        }

        private static void __ModifierSet__Push(HashSet<Modifier> items, bool isReturn)
        {
            NativeImplClient.PushIntArray(items.Select(x => (int)x).ToArray());
        }

        private static HashSet<Modifier> __ModifierSet__Pop()
        {
            var intArray = NativeImplClient.PopIntArray();
            return intArray.Select(x => (Modifier)x).ToHashSet();
        }

        public static void WindowShow(Window _this)
        {
            NativeImplClient.PushSizeT(_this.NativeHandle);
            NativeImplClient.InvokeModuleMethod(_windowShow);
        }

        public static void WindowSetMenuBar(Window _this, MenuBar mb)
        {
            NativeImplClient.PushSizeT(mb.NativeHandle);
            NativeImplClient.PushSizeT(_this.NativeHandle);
            NativeImplClient.InvokeModuleMethod(_windowSetMenuBar);
        }

        public static void WindowInvalidate(Window _this, int x, int y, int width, int height)
        {
            NativeImplClient.PushInt(height);
            NativeImplClient.PushInt(width);
            NativeImplClient.PushInt(y);
            NativeImplClient.PushInt(x);
            NativeImplClient.PushSizeT(_this.NativeHandle);
            NativeImplClient.InvokeModuleMethod(_windowInvalidate);
        }

        public static void WindowDestroy(Window _this)
        {
            NativeImplClient.PushSizeT(_this.NativeHandle);
            NativeImplClient.InvokeModuleMethod(_windowDestroy);
        }

        public static Window CreateWindow(string title, WindowDelegate del)
        {
            ((IPushable)del).Push(false);
            NativeImplClient.PushString(title);
            NativeImplClient.InvokeModuleMethod(_createWindow);
            return new Window(NativeImplClient.PopSizeT());
        }

        public static Accelerator CreateAccelerator(Key key, HashSet<Modifier> modifiers)
        {
            __ModifierSet__Push(modifiers, false);
            Key__Push(key);
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

        public static void MenuAddAction(Menu _this, Action action)
        {
            NativeImplClient.PushSizeT(action.NativeHandle);
            NativeImplClient.PushSizeT(_this.NativeHandle);
            NativeImplClient.InvokeModuleMethod(_menuAddAction);
        }

        public static void MenuAddSubmenu(Menu _this, string label, Menu sub)
        {
            NativeImplClient.PushSizeT(sub.NativeHandle);
            NativeImplClient.PushString(label);
            NativeImplClient.PushSizeT(_this.NativeHandle);
            NativeImplClient.InvokeModuleMethod(_menuAddSubmenu);
        }

        public static void MenuAddSeparator(Menu _this)
        {
            NativeImplClient.PushSizeT(_this.NativeHandle);
            NativeImplClient.InvokeModuleMethod(_menuAddSeparator);
        }

        public static Menu CreateMenu()
        {
            NativeImplClient.InvokeModuleMethod(_createMenu);
            return new Menu(NativeImplClient.PopSizeT());
        }

        public static void MenuBarAddMenu(MenuBar _this, string label, Menu menu)
        {
            NativeImplClient.PushSizeT(menu.NativeHandle);
            NativeImplClient.PushString(label);
            NativeImplClient.PushSizeT(_this.NativeHandle);
            NativeImplClient.InvokeModuleMethod(_menuBarAddMenu);
        }

        public static MenuBar CreateMenuBar()
        {
            NativeImplClient.InvokeModuleMethod(_createMenuBar);
            return new MenuBar(NativeImplClient.PopSizeT());
        }

        public static void CGContextSetRGBFillColor(CGContext _this, double red, double green, double blue, double alpha)
        {
            NativeImplClient.PushDouble(alpha);
            NativeImplClient.PushDouble(blue);
            NativeImplClient.PushDouble(green);
            NativeImplClient.PushDouble(red);
            NativeImplClient.PushSizeT(_this.NativeHandle);
            NativeImplClient.InvokeModuleMethod(_cGContextSetRGBFillColor);
        }

        public static void CGContextFillRect(CGContext _this, CGRect rect)
        {
            CGRect__Push(rect, false);
            NativeImplClient.PushSizeT(_this.NativeHandle);
            NativeImplClient.InvokeModuleMethod(_cGContextFillRect);
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

            _windowShow = NativeImplClient.GetModuleMethod(_module, "windowShow");
            _windowSetMenuBar = NativeImplClient.GetModuleMethod(_module, "windowSetMenuBar");
            _windowInvalidate = NativeImplClient.GetModuleMethod(_module, "windowInvalidate");
            _windowDestroy = NativeImplClient.GetModuleMethod(_module, "windowDestroy");
            _createWindow = NativeImplClient.GetModuleMethod(_module, "createWindow");
            _createAccelerator = NativeImplClient.GetModuleMethod(_module, "createAccelerator");
            _createAction = NativeImplClient.GetModuleMethod(_module, "createAction");
            _menuAddAction = NativeImplClient.GetModuleMethod(_module, "menuAddAction");
            _menuAddSubmenu = NativeImplClient.GetModuleMethod(_module, "menuAddSubmenu");
            _menuAddSeparator = NativeImplClient.GetModuleMethod(_module, "menuAddSeparator");
            _createMenu = NativeImplClient.GetModuleMethod(_module, "createMenu");
            _menuBarAddMenu = NativeImplClient.GetModuleMethod(_module, "menuBarAddMenu");
            _createMenuBar = NativeImplClient.GetModuleMethod(_module, "createMenuBar");
            _cGContextSetRGBFillColor = NativeImplClient.GetModuleMethod(_module, "cGContextSetRGBFillColor");
            _cGContextFillRect = NativeImplClient.GetModuleMethod(_module, "cGContextFillRect");
            _runloop = NativeImplClient.GetModuleMethod(_module, "runloop");
            _exitRunloop = NativeImplClient.GetModuleMethod(_module, "exitRunloop");
            _moduleInit = NativeImplClient.GetModuleMethod(_module, "moduleInit");
            _moduleShutdown = NativeImplClient.GetModuleMethod(_module, "moduleShutdown");

            _windowDelegate = NativeImplClient.GetInterface(_module, "WindowDelegate");
            _windowDelegate_onCloseRequest = NativeImplClient.GetInterfaceMethod(_windowDelegate, "onCloseRequest");
            _windowDelegate_onDestroyed = NativeImplClient.GetInterfaceMethod(_windowDelegate, "onDestroyed");
            _windowDelegate_onResized = NativeImplClient.GetInterfaceMethod(_windowDelegate, "onResized");
            _windowDelegate_onRepaint = NativeImplClient.GetInterfaceMethod(_windowDelegate, "onRepaint");
            _windowDelegate_onAction = NativeImplClient.GetInterfaceMethod(_windowDelegate, "onAction");

            NativeImplClient.SetClientMethodWrapper(_windowDelegate_onCloseRequest, delegate(ClientObject obj)
            {
                var inst = (ClientWindowDelegate) obj;
                CloseRequestReply__Push(inst.OnCloseRequest(), true);
            });

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

            NativeImplClient.SetClientMethodWrapper(_windowDelegate_onAction, delegate(ClientObject obj)
            {
                var inst = (ClientWindowDelegate) obj;
                var id = NativeImplClient.PopInt();
                var action = new Action(NativeImplClient.PopSizeT());
                inst.OnAction(id, action);
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
