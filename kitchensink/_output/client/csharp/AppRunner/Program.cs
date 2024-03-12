using Org.Prefixed.KitchenSink;
namespace AppRunner;

internal static class Program
{
    private static void Main(string[] args)
    {
        Library.Init();

        using (var x = First.CreateThing())
        {
            try
            {
                x.DoSomething(1234);
            }
            catch (First.WootProblem p)
            {
                Console.WriteLine($"*** caught problem {p.S}");
            }
        }
        Console.WriteLine("after block");
        
        Library.Shutdown();
    }
}
