using CSharpFunctionalExtensions;
using Org.Prefixed.KitchenSink;
namespace AppRunner;

internal static class Program
{
    private static void Main(string[] args)
    {
        Library.Init();
        
        First.DoSomething("excellent string!", 1001);
        
        Library.DumpTables();
        Library.Shutdown();
    }
}
