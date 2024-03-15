using Org.Prefixed.KitchenSink;
namespace AppRunner;

internal class MyThing : First.ITestInterface, IDisposable
{
    public void Dispose()
    {
        Console.WriteLine("client disposing, woot!");
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
        
        Library.Shutdown();
    }
}
