using CSharpFunctionalExtensions;
using Org.Prefixed.KitchenSink;
namespace AppRunner;

internal static class Program
{
    private static void Main(string[] args)
    {
        Library.Init();
        
        First.NeatoMethod([10, 20, 30, Maybe<int>.None, 50, 60, Maybe<int>.None, 80, 90, Maybe<int>.None]);
        
        Console.WriteLine("C# receiving array:");
        foreach (var woot in First.ReturnsArray())
        {
            Console.WriteLine(woot.TryGetValue(out var value) ? $" - {value}" : " - (none)");
        }
        
        Library.DumpTables();
        Library.Shutdown();
    }
}
