using Org.Prefixed.KitchenSink;
namespace AppRunner;

internal static class Program
{
    private static void Main(string[] args)
    {
        Library.Init();

        using (var wut = First.CreateThing())
        {
            wut.SomeMethod(101);
        }
        
        Library.Shutdown();
    }
}
