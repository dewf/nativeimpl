using Org.Prefixed.KitchenSink;
namespace AppRunner;

internal class MyThing : First.InterfaceGroup.ClientMyInterface
{
    public override void DoesSomething(string s)
    {
        Console.WriteLine($"C# MyThing.DoesSomething({s})");
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

