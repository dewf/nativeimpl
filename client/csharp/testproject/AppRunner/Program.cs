using System;
using TestLibraryClient;
using Action = TestLibraryClient.Action;

namespace AppRunner
{
    public static class Program
    {
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
