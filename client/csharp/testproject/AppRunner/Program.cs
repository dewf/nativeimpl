using System;
using TestLibraryClient;
using Action = TestLibraryClient.Action;

namespace AppRunner
{
    public class MainWindow : TopLevelWindow
    {
        private int _width;
        private int _height;
        public MainWindow(string title) : base(title)
        {
        }
        public override void OnRepaint(CGContext context)
        {
            context.SetRGBFillColor(0, 0, 0, 1);
            context.FillRect(CGRect.OfSize(_width, _height));
            context.SetRGBFillColor(1, 0, 0, 1);
            context.FillRect(new CGRect(100, 100, 100, 100));
            context.SetRGBFillColor(0, 0, 1, 1);
            context.FillRect(new CGRect(_width - 200, _height - 200, 100, 100));
        }
        public override void OnResize(int width, int height)
        {
            _width = width;
            _height = height;
        }
        public override void OnDestroyed()
        {
            Console.WriteLine("C# OnDestroyed() called! Shutting down ...");
            OpenWL.ExitRunloop();
        }
    }
    public static class Program
    {
        public static void Main(string[] args)
        {
            TestLibrary.Init();
            // =======================================
            var window = new MainWindow("window title!!!");
            
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
