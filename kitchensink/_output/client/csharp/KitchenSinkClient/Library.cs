using Org.Prefixed.KitchenSink.Support;

namespace Org.Prefixed.KitchenSink;

public static class Library
{
    public static void Init()
    {
        if (NativeImplClient.Init() != 0)
        {
            Console.WriteLine("NativeImplClient.Init failed");
            return;
        }
        // registrations, static module inits
        First.ModuleStatic.__Init();
        Second.ModuleStatic.__Init();
    }

    public static void Shutdown()
    {
        // module static shutdowns (if any, might be empty)
        Second.ModuleStatic.__Shutdown();
        First.ModuleStatic.__Shutdown();
        // bye
        NativeImplClient.Shutdown();
    }

    public static void DumpTables()
    {
        NativeImplClient.DumpTables();
    }
}
