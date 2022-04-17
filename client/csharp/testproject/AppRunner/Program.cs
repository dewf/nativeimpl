using System;
using TestLibraryClient;

namespace AppRunner
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            TestLibrary.Init();

            TestLibrary.ArrayTest3(new[] { true, false, true, false, false, false, true });
            var returned = TestLibrary.ReturnArrayTest3();
            foreach (var b in returned)
            {
                Console.WriteLine(" == {0}", b);
            }

            GC.Collect();
            NativeImplClient.DumpTables();
            
            Console.WriteLine("TestLibrary.Shutdown:");
            TestLibrary.Shutdown();
        }
    }
}