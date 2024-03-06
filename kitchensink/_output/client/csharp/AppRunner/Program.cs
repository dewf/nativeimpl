using Org.Prefixed.KitchenSink;
namespace AppRunner;

internal class MyThing : First.ClientWhateverThing
{
    public override int SomeMethod(string s)
    {
        Console.WriteLine($"C# implementation of SomeMethod({s})");
        return 1001;
    }
}

internal static class Program
{
    private static void Main(string[] args)
    {
        Library.Init();

        var result = First.DoSomething(101, 202);
        Console.WriteLine($"returned string was [{result}]");

        using (var thing = new MyThing())
        {
            First.OperateOnThing(thing);
        }

        try
        {
            First.ThrowException("C#: something went wrong!");
            Console.WriteLine("*** no exception was caught??");
        }
        catch (First.CoolException e)
        {
            Console.WriteLine($"caught exception as intended! reason: [{e.Reason}]");
        }
        
        Library.Shutdown();
    }
}
