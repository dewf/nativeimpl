using CSharpFunctionalExtensions;
using Org.Prefixed.KitchenSink;
namespace AppRunner;

internal static class Program
{
    private static void Main(string[] args)
    {
        Library.Init();

        var thing1 = new First.Thing1.Value2(3003);
        var thing2 = new First.Thing2.Variant2(thing1);
        First.DoSomething(thing2);
        
        Library.DumpTables();
        Library.Shutdown();
    }
}
