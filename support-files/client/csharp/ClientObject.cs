namespace %%TARGET_NAMESPACE%%.Support;

internal abstract class ClientObject : ClientResource
{
    protected override void NativePush()
    {
        NativeMethods.pushInstance(Id, true);
    }
    public static ClientObject GetById(int id)
    {
        return (ClientObject)GetResourceById(id);
    }
}

internal static class ClientObjectHelpers
{
    private static readonly Dictionary<IDisposable, ClientInterfaceWrapper> ClientInterfaceWrapperMap = new();
    internal class ClientInterfaceWrapper(IDisposable thing) : ClientObject
    {
        public IDisposable RawInterface { get; } = thing;
        protected override void ReleaseExtra()
        {
            ClientInterfaceWrapperMap.Remove(RawInterface);
        }
    }
    internal static void PushInterface(IDisposable thing, bool isReturn)
    {
        switch (thing)
        {
            case null:
                NativeImplClient.PushNull();
                break;
            case ServerObject:
                // server stuff can go right over
                ((IPushable)thing).Push(isReturn);
                break;
            default:
            {
                // if it's ours (client side), have we seen it before? has it already been pushed to the other side?
                if (ClientInterfaceWrapperMap.TryGetValue(thing, out var existing))
                {
                    ((IPushable)existing).Push(isReturn);
                }
                else
                {
                    // it's fresh, need to create a wrapper to push it (so we don't have to force developers to inherit from ClientObject themselves)
                    var wrapper = new ClientInterfaceWrapper(thing);
                    ClientInterfaceWrapperMap[thing] = wrapper;
                    ((IPushable)wrapper).Push(isReturn);
                }
                break;
            }
        }
    }
    // Pop is unfortunately more complex, and C# generic stuff makes it difficult, so we rely on the codegen for it
}