using System;
using TestLibraryClient;

namespace AppRunner
{
    internal class Thing : TestLibrary.ClientMyThing
    {
        public override int DoSomething(int x)
        {
            Console.WriteLine("C# Thing.DoSomething({0})", x);
            return x + 1001;
        }
    }
    
    public static class Program
    {
        public static void Main(string[] args)
        {
            TestLibrary.Init();
            
            TestLibrary.ApplyFunc(x => x * 123);
            TestLibrary.TakesThing(new Thing());
            using (var thing = TestLibrary.ReturnsThing())
            {
                Console.WriteLine("C# returned thing .DoSomething(1234) = {0}", thing.DoSomething(1234));
            }

            using (var buffer = new ClientBuffer<int>(50))
            {
                var span = buffer.GetSpan(out var length);
                for (var i = 0; i < length; i++)
                {
                    span[i] = i;
                }
                Console.WriteLine("Wrote {0} values to buffer", length);
                TestLibrary.TakesBuffer(buffer);
                Console.WriteLine("TakesBuffer() finished! Checking values:");
                // for (var i = 0; i < length; i++)
                // {
                //     Console.WriteLine("C# buffer[{0}] = {1}", i, span[i]);
                // }
            }
            Console.WriteLine("end ClientBuffer 'using' block");
            
            GC.Collect();
            TestLibrary.DumpTables();
            
            Console.WriteLine("TestLibrary.Shutdown:");
            TestLibrary.Shutdown();
        }
    }
}
