using System;
using TestLibraryClient;
using Action = TestLibraryClient.Action;

namespace AppRunner
{
    public static class Program
    {
        private class Window : IWindowDelegate
        {
            private readonly WLWindow _wlWindow;
            private int _width;
            private int _height;
            public Window(string title)
            {
                _wlWindow = new WLWindow(title, this);
            }
            public void Show()
            {
                _wlWindow.Show();
            }
            public void SetMenuBar(MenuBar mb)
            {
                _wlWindow.SetMenuBar(mb);
            }
            // interface implementation
            public void OnRepaint(CGContext context)
            {
                context.SetRGBFillColor(0, 0, 0, 1);
                context.FillRect(CGRect.OfSize(_width, _height));
                context.SetRGBFillColor(1, 0, 0, 1);
                context.FillRect(new CGRect(100, 100, 100, 100));
            }
            public void OnResize(int width, int height)
            {
                _width = width;
                _height = height;
                Console.WriteLine("Window resized to: {0} / {1}", width, height);
            }
            public void OnDestroyed()
            {
                Console.WriteLine("C# OnDestroyed() called! Shutting down ...");
                OpenWL.ExitRunloop();
            }
        }
        public static void Main(string[] args)
        {
            TestLibrary.Init();
            // =======================================
            var window = new Window("window title!!!");
            
            var fileMenu = new Menu();
            var accel = new Accelerator(TestLibrary.Key.E, new[] { Modifier.Control, Modifier.Shift });
            var action = new Action("E&xit", accel, () =>
            {
                Console.WriteLine("Exit Action!!!");
                TestLibrary.ExitRunloop();
            });
            fileMenu.AddAction(action);

            var menuBar = new MenuBar();
            menuBar.AddMenu("&File", fileMenu);
            window.SetMenuBar(menuBar);
                
            window.Show();
            OpenWL.Runloop();
            
            // =======================================
            GC.Collect();
            TestLibrary.DumpTables();
            
            Console.WriteLine("TestLibrary.Shutdown:");
            TestLibrary.Shutdown();
        }
    }
}
