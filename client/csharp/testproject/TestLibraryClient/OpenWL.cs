using System;
using System.Collections.Generic;
using System.Linq;

namespace TestLibraryClient
{
    public interface IWindowDelegate
    {
        void OnRepaint(CGContext context);
        void OnResize(int width, int height);
        void OnCloseRequest(ref bool cancelClose);
        void OnDestroyed();
    }
    [Flags]
    public enum Modifiers
    {
        Shift = 1,
        Control = 2,
        Alt = 4,
        MacControl = 8,
    }
    public class Accelerator
    {
        internal readonly TestLibrary.Accelerator PrivAccel;
        public Accelerator(TestLibrary.Key key, Modifiers modifiers)
        {
            PrivAccel = TestLibrary.CreateAccelerator(key, (TestLibrary.Modifiers)modifiers);
        }
    }
    public class Action
    {
        public delegate void ActionDelegate();
        private static readonly Dictionary<int, Action> Lookup = new ();
        private static int _nextId = 1;
        //
        internal readonly TestLibrary.Action PrivAction;
        private readonly ActionDelegate _todo;
        public Action(string label, Accelerator accel, ActionDelegate todo)
        {
            var id = _nextId++;
            PrivAction = TestLibrary.CreateAction(id, label, accel?.PrivAccel);
            _todo = todo;
            Lookup.Add(id, this);
        }
        public void Apply()
        {
            _todo();
        }
        public static Action GetById(int id)
        {
            return Lookup[id];
        }
    }
    public class Menu
    {
        internal readonly TestLibrary.Menu PrivMenu;
        public Menu()
        {
            PrivMenu = TestLibrary.CreateMenu();
        }
        public void AddAction(Action action)
        {
            TestLibrary.MenuAddAction(PrivMenu, action.PrivAction);
        }
        public void AddSeparator()
        {
            TestLibrary.MenuAddSeparator(PrivMenu);
        }
        public void AddSubmenu(string label, Menu sub)
        {
            TestLibrary.MenuAddSubmenu(PrivMenu, label, sub.PrivMenu);
        }
    }
    public class MenuBar
    {
        internal readonly TestLibrary.MenuBar PrivMenuBar;
        public MenuBar()
        {
            PrivMenuBar = TestLibrary.CreateMenuBar();
        }
        public void AddMenu(string label, Menu menu)
        {
            TestLibrary.MenuBarAddMenu(PrivMenuBar, label, menu.PrivMenu);
        }
    }
    // WLWindow: immediate wrapper around native WL methods / immediate callback delegate
    // still want another layer around this (IWindowDelegate) to shield user from raw CG context etc
    public class WLWindow : TestLibrary.ClientWindowDelegate
    {
        private readonly TestLibrary.Window _privWindow;
        private readonly IWindowDelegate _windowDelegate;
        public WLWindow(string title, IWindowDelegate windowDelegate)
        {
            _privWindow = TestLibrary.CreateWindow(title, this);
            _windowDelegate = windowDelegate;
        }
        public void SetMenuBar(MenuBar mb)
        {
            TestLibrary.WindowSetMenuBar(_privWindow, mb.PrivMenuBar);
        }
        public void Show()
        {
            TestLibrary.WindowShow(_privWindow);
        }
        public void Invalidate(int x, int y, int width, int height)
        {
            TestLibrary.WindowInvalidate(_privWindow, x, y, width, height);
        }
        public override TestLibrary.CloseRequestReply OnCloseRequest()
        {
            TestLibrary.CloseRequestReply reply;
            reply.CancelClose = false;
            _windowDelegate.OnCloseRequest(ref reply.CancelClose);
            return reply;
        }
        public override void OnDestroyed()
        {
            _windowDelegate.OnDestroyed();
        }
        public override void OnResized(int newWidth, int newHeight)
        {
            _windowDelegate.OnResize(newWidth, newHeight);
        }
        public override void OnRepaint(TestLibrary.CGContext context)
        {
            var c2 = new CGContext(context);
            _windowDelegate.OnRepaint(c2);
        }
        public override void OnAction(int id, TestLibrary.Action action)
        {
            Action.GetById(id).Apply();
        }
        public override void OnMouseEvent(TestLibrary.MouseEvent mouseEvent)
        {
            //Console.WriteLine("WLWindow OnMouseEvent (not yet forwarded to IWindowDelegate)");
        }
        public override void Dispose()
        {
            // hmmm ?
            Console.WriteLine("C# Window.Dispose() called");
            TestLibrary.WindowDestroy(_privWindow);
        }
    }
    public static class OpenWL
    {
        public static void Runloop()
        {
            TestLibrary.Runloop();
        }
        public static void ExitRunloop()
        {
            TestLibrary.ExitRunloop();
        }
    }
}
