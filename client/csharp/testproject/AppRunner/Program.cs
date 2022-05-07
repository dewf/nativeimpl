using System;
using TestLibraryClient;

namespace AppRunner
{
    public class Window : TestLibrary.ClientWindowDelegate
    {
        private readonly bool _exitOnClose;
        private readonly TestLibrary.Window _privWindow;
        public Window(string title, bool exitOnClose)
        {
            _exitOnClose = exitOnClose;
            _privWindow = TestLibrary.CreateWindow(title, this);
        }
        public void SetMenuBar(TestLibrary.MenuBar mb)
        {
            _privWindow.SetMenuBar(mb);
        }
        public void Show()
        {
            _privWindow.Show();
        }
        public override void OnDestroyed()
        {
            if (_exitOnClose)
            {
                Console.WriteLine("C# OnDestroyed() called! Shutting down ...");
                TestLibrary.ExitRunloop();
            }
        }
        public override void Dispose()
        {
            _privWindow.Dispose();
        }
    }
    
    public static class Program
    {
        public static void Main(string[] args)
        {
            TestLibrary.Init();

            var window = new Window("My title!!", true);
            var fileMenu = TestLibrary.CreateMenu();
            var accel = TestLibrary.CreateAccelerator(TestLibrary.KeyEnum.KeyE);
            var action1 = TestLibrary.CreateAction(101, "E&xit", accel);
            fileMenu.AddAction(action1);

            var menuBar = TestLibrary.CreateMenuBar();
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
