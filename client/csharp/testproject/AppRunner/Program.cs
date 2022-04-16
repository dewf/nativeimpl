using System;
using System.Threading;
using TestLibraryClient;

namespace AppRunner
{
    public static class Program
    {
        private class MyIThingClass : TestLibrary.Client_IThing01
        {
            private TestLibrary.IThing01 _stashed;
            
            public override int SomeMethod(int x)
            {
                Console.WriteLine($"C# MyIThingClass.SomeMethod({x})");
                return x + 101;
            }

            public override void StashThing(TestLibrary.IThing01 subThing)
            {
                _stashed = subThing;
            }

            public override TestLibrary.IThing01 GetStashedThing()
            {
                // clear out _stashed ?
                var ret = _stashed;
                _stashed = null;
                // if this were a language like C++, we'd have to be concerned
                // if 'ret' were the only living proxy to the remote side ---
                // because after the calling code (client method wrapper in TestLibrary.cs) pushes the object ID,
                // the proxy would be destroyed, releasing the remote side (again, remote stack only holds ID)
                // nonetheless because GC is unpredictable, we still do a 'safe push' in the wrapper code
                return ret;
            }
            
            public override void TestThrow(bool doThrow)
            {
                Console.WriteLine($"### Hello from C# MyIThingClass.TestThrow! {doThrow}");
                if (doThrow)
                {
                    throw new TestLibrary.InterfaceMethodException(98765);
                }
            }
            
            public override void OnTimerTick()
            {
                Console.WriteLine($"C# On Timer Tick!!! (thread: {Thread.CurrentThread.GetHashCode()})");
            }

            public override void Dispose()
            {
                Console.WriteLine("C# MyIThingClass - perfunctory Dispose()");
                //base.Dispose(); // optional - base doesn't do anything yet
            }
        }
        
        public static void Main(string[] args)
        {
            TestLibrary.Init();

            // begin nested scope
            {
                TestLibrary.StringTest("wahooooo string test!!!!@@");
                Console.WriteLine("Returned string is: [{0}]", TestLibrary.ReturnString());
                Console.WriteLine("Sum is: {0}", TestLibrary.Sum(1, 2, 3));
                Console.WriteLine("ExecFunc01: {0}", TestLibrary.ExecFunc01(i => i * 3));
                
                TestLibrary.StashFunc(x => x + 1);
                TestLibrary.ReturnStashedFunc(); // wraps our func with a C++ func
                TestLibrary.ReleaseFunc();

                var x = new MyIThingClass();
                x.SomeMethod(101);
                //TestLibrary.OperateOnThing(x, 101);
                TestLibrary.SaveThing(x);
                var q = TestLibrary.GetSavedThing();
                
                using var y = TestLibrary.MakeThing();
                y.TestThrow(false);
                try
                {
                    y.TestThrow(true);
                }
                catch (TestLibrary.InterfaceMethodException e)
                {
                    Console.WriteLine($"Caught InterfaceMethodException in .TestThrow! (message {e.Message} value {e.Value})");
                }
            }
            // end of nested scope
            Console.WriteLine("Module constants =================");
            Console.WriteLine(" - Global01: {0}", TestLibrary.Global01);
            Console.WriteLine(" - AnotherGlobal: {0}", TestLibrary.AnotherGlobal);
            Console.WriteLine(" - LastIntGlobal: {0}", TestLibrary.LastIntGlobal);
            
            
            // exceptions
            TestLibrary.TestNegative(101);
            TestLibrary.TestNegative(1);
            TestLibrary.TestNegative(0);
            try
            {
                TestLibrary.TestNegative(-1);
            }
            catch (TestLibrary.NegativeException e)
            {
                Console.WriteLine($"Caught negative exception! {e.Message} ({e.Value})");
            }
            
            // client throw test
            using (var myThing = new MyIThingClass())
            {
                TestLibrary.TestClientThrow(myThing);
                
                TestLibrary.StartTimer(myThing, 2);

                Console.WriteLine($"C# sleeping 5 seconds (thread: {Thread.CurrentThread.GetHashCode()})");
                Thread.Sleep(5_000);
                
                TestLibrary.StopTimer();
                Console.WriteLine($"*** finishing thread: {Thread.CurrentThread.GetHashCode()}");
            }

            GC.Collect();
            NativeImplClient.DumpTables();
            
            Console.WriteLine("TestLibrary.Shutdown:");
            TestLibrary.Shutdown();
        }
    }
}