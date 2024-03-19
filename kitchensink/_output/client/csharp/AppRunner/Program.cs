using CSharpFunctionalExtensions;
using Org.Prefixed.KitchenSink;
namespace AppRunner;

internal static class Program
{
    private static void Main(string[] args)
    {
        Library.Init();
        
        First.Method1([10, "woot", 30, "woot2!", 50, "niiiice!", "oops", 80]);
        Console.WriteLine("C# got:");
        foreach (var x in First.Method2())
        {
            Console.WriteLine(x.TryGetValue(out var value) ? $" - success: {value}" : $" - error: {x.Error}");
        }
        
        Library.DumpTables();
        Library.Shutdown();
    }
}
