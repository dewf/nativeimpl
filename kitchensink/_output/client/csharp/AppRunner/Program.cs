using Org.Prefixed.KitchenSink;

using Org.Prefixed.KitchenSink.First;
using static Org.Prefixed.KitchenSink.First.ModuleStatic;

namespace AppRunner;

internal static class Program
{
    private static void Main(string[] args)
    {
        Library.Init();

        var x = RootOpaque.MakeOne();

        Library.DumpTables();
        Library.Shutdown();
    }
}

