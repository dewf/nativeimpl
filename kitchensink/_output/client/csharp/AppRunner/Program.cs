using CSharpFunctionalExtensions;
using Org.Prefixed.KitchenSink;
namespace AppRunner;

internal static class Program
{
    private static void Main(string[] args)
    {
        Library.Init();
        
        First.DoSomething(Maybe.None, 1234);
        
        Library.DumpTables();
        Library.Shutdown();
    }
}
