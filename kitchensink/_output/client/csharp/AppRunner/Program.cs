using Org.Prefixed.KitchenSink;
namespace AppRunner;

internal static class Program
{
    private static void Main(string[] args)
    {
        Library.Init();

        var result = First.DoSomething(101, 202);
        Console.WriteLine($"returned string was [{result}]");
        
        Library.Shutdown();
    }
}


