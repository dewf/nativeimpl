using Org.Prefixed.KitchenSink;
namespace AppRunner;

internal class MyThing : First.ITestInterface
{
    public void Dispose()
    {
        Console.WriteLine("C# MyThing disposed!");
    }
    public void WootMethod(int x)
    {
        Console.WriteLine($"C# MyThing.WootMethod({x})!");
    }
}

internal static class Program
{
    private static void Main(string[] args)
    {
        Library.Init();

        using (var thing = new MyThing())
        {
            First.OperateOnInterface(thing);
        }
        Console.WriteLine("C# should have disposed client thing");

        using (var thing2 = First.ServerCreate())
        {
            Console.WriteLine("C# about to invoke server thing's .WootMethod...");
            thing2.WootMethod(9876);
        }
        Console.WriteLine("C# should have disposed thing2");
        
        Library.DumpTables();
        Library.Shutdown();
    }
}
