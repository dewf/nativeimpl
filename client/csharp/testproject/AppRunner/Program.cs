using System;
using TestLibraryClient;

namespace AppRunner
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            TestLibrary.Init();
            // =======================================
            Console.WriteLine("DoSomething result was: {0}", TestLibrary.DoSomething(123, "hallo hallo!"));
            // =======================================
            GC.Collect();
            TestLibrary.DumpTables();
            
            Console.WriteLine("TestLibrary.Shutdown:");
            TestLibrary.Shutdown();
        }
    }
}
