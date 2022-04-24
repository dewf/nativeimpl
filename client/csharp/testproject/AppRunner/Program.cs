using System;
using TestLibraryClient;

namespace AppRunner
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            TestLibrary.Init();

            var x = TestLibrary.SimpleFunction(100);

            GC.Collect();
            NativeImplClient.DumpTables();
            
            Console.WriteLine("TestLibrary.Shutdown:");
            TestLibrary.Shutdown();
        }
    }
}