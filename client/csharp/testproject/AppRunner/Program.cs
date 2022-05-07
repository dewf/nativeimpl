using System;
using TestLibraryClient;

namespace AppRunner
{
    public class Window : TestLibrary.ClientWindowDelegate
    {
        private readonly bool _exitOnClose;
        private readonly TestLibrary.Window _handle;
        public Window(string title, bool exitOnClose)
        {
            _exitOnClose = exitOnClose;
            _handle = TestLibrary.CreateWindow(title, this);
        }
        public void Show()
        {
            _handle.Show();
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
            _handle.Dispose();
        }
    }
    
    public static class Program
    {
        public static void Main(string[] args)
        {
            TestLibrary.Init();

            using (var window = new Window("My title!!", true))
            {
                window.Show();
                TestLibrary.Runloop();
            }
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
