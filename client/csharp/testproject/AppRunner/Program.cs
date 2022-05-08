using System;
using TestLibraryClient;

namespace AppRunner
{
    public class Accelerator
    {
        internal readonly TestLibrary.Accelerator PrivAccel;
        public Accelerator(TestLibrary.KeyEnum key)
        {
            PrivAccel = TestLibrary.CreateAccelerator(key);
        }
    }
    public class Action
    {
        internal readonly TestLibrary.Action PrivAction;
        public Action(int id, string label, Accelerator accel)
        {
            PrivAction = TestLibrary.CreateAction(id, label, accel.PrivAccel);
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
    public struct CGPoint
    {
        public double X;
        public double Y;
        public CGPoint(double x, double y)
        {
            X = x;
            Y = y;
        }
        internal TestLibrary.CGPoint ToNative()
        {
            return new TestLibrary.CGPoint(X, Y);
        }
    }
    public struct CGSize
    {
        public double Width;
        public double Height;
        public CGSize(double width, double height)
        {
            Width = width;
            Height = height;
        }
        internal TestLibrary.CGSize ToNative()
        {
            return new TestLibrary.CGSize(Width, Height);
        }
    }
    public struct CGRect
    {
        public CGPoint Origin;
        public CGSize Size;
        public CGRect(CGPoint origin, CGSize size)
        {
            Origin = origin;
            Size = size;
        }
        public CGRect(double x, double y, double width, double height)
        {
            Origin = new CGPoint(x, y);
            Size = new CGSize(width, height);
        }
        public static CGRect OfSize(double width, double height)
        {
            return new CGRect(0, 0, width, height);
        }
        internal TestLibrary.CGRect ToNative()
        {
            return new TestLibrary.CGRect(Origin.ToNative(), Size.ToNative());
        }
    }
    public class CGContext
    {
        private readonly TestLibrary.CGContext _dlContext;
        public CGContext(TestLibrary.CGContext dlContext)
        {
            _dlContext = dlContext;
        }
        public void SetRGBFillColor(double red, double green, double blue, double alpha)
        {
            TestLibrary.CgContextSetRGBFillColor(_dlContext, red, green, blue, alpha);
        }
        public void FillRect(CGRect rect)
        {
            TestLibrary.CgContextFillRect(_dlContext, rect.ToNative());
        }
    }
    public class Window : TestLibrary.ClientWindowDelegate
    {
        private readonly bool _exitOnClose;
        private readonly TestLibrary.Window _privWindow;
        private int _width, _height;
        public Window(string title, bool exitOnClose)
        {
            _exitOnClose = exitOnClose;
            _privWindow = TestLibrary.CreateWindow(title, this);
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
        public override void OnDestroyed()
        {
            if (_exitOnClose)
            {
                Console.WriteLine("C# OnDestroyed() called! Shutting down ...");
                TestLibrary.ExitRunloop();
            }
        }
        public override void OnResized(int newWidth, int newHeight)
        {
            _width = newWidth;
            _height = newHeight;
            Console.WriteLine("Window resized to: {0} / {1}", newWidth, newHeight);
        }
        public override void OnRepaint(TestLibrary.CGContext context)
        {
            var c2 = new CGContext(context);
            c2.SetRGBFillColor(0, 0, 0, 1);
            c2.FillRect(CGRect.OfSize(_width, _height));

            c2.SetRGBFillColor(1, 0, 0, 1);
            c2.FillRect(new CGRect(100, 100, 100, 100));
        }
        public override void Dispose()
        {
            // hmmm ?
            Console.WriteLine("C# Window.Dispose() called");
            TestLibrary.WindowDestroy(_privWindow);
        }
    }
    
    public static class Program
    {
        public static void Main(string[] args)
        {
            TestLibrary.Init();

            var window = new Window("My title!!", true);
            
            var fileMenu = new Menu();
            var accel = new Accelerator(TestLibrary.KeyEnum.KeyE);
            var action = new Action(101, "E&xit", accel);
            fileMenu.AddAction(action);

            var menuBar = new MenuBar();
            menuBar.AddMenu("&File", fileMenu);
            window.SetMenuBar(menuBar);
                
            window.Show();
            TestLibrary.Runloop();

            // using (var buffer = new ClientBuffer<int>(50))
            // {
            //     var span = buffer.GetSpan(out var length);
            //     for (var i = 0; i < length; i++)
            //     {
            //         span[i] = i;
            //     }
            //     Console.WriteLine("Wrote {0} values to buffer", length);
            //     TestLibrary.TakesBuffer(buffer);
            //     Console.WriteLine("TakesBuffer() finished! Checking values:");
            //     // for (var i = 0; i < length; i++)
            //     // {
            //     //     Console.WriteLine("C# buffer[{0}] = {1}", i, span[i]);
            //     // }
            // }
            // Console.WriteLine("end ClientBuffer 'using' block");
            //
            // using (var serverBuffer = TestLibrary.ReturnsBuffer())
            // {
            //     var span = serverBuffer.GetSpan(out var length);
            //     Console.WriteLine("server length: {0}", length);
            //     for (var i = 0; i < length; i++)
            //     {
            //         Console.WriteLine("C# viewing server span: [{0}] = {1}", i, span[i]);
            //     }
            //     // send back to server to see what happens!
            //     TestLibrary.TakesBuffer(serverBuffer);
            // }
            // Console.WriteLine("end server buffer 'using' block");
            
            GC.Collect();
            TestLibrary.DumpTables();
            
            Console.WriteLine("TestLibrary.Shutdown:");
            TestLibrary.Shutdown();
        }
    }
}
