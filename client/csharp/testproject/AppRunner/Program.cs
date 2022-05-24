using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestLibraryClient;

namespace AppRunner
{
    public static class Program
    {
        private static void ThreadFunc(int id)
        {
            var tid = $"T:{id}";
            var rand = new Random();
            for (var i = 0; i < 1000; i++)
            {
                // Console.WriteLine("TID {0} iter {1}", tid, i);
                var count = rand.Next(100, 1000);
                var items = new int[count];
                for (var j = 0; j < count; j++)
                {
                    items[j] = rand.Next(100);
                }
                var sum = items.Sum();
                var res = TestLibrary.SumValues(items, tid);
                Debug.Assert(res == sum, "sums don't match!");
                // Thread.Sleep(rand.Next(300));
            }
        }
        
        public static void Main(string[] args)
        {
            TestLibrary.Init();
            // =======================================
            var ids = Enumerable.Range(1, 40);
            Parallel.ForEach(ids, ThreadFunc);
            Console.WriteLine("#### woot done!!! #####");
            
            // =======================================
            GC.Collect();
            TestLibrary.DumpTables();
            
            Console.WriteLine("TestLibrary.Shutdown:");
            TestLibrary.Shutdown();
        }
    }
}
