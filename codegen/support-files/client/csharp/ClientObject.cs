namespace %%TARGET_NAMESPACE%%.Support;

internal abstract class ClientObject : ClientResource
{
    protected override void NativePush()
    {
        NativeMethods.pushClientInterface(Id);
    }
    public static ClientObject GetById(int id)
    {
        return (ClientObject)GetResourceById(id);
    }
}

// marker interface, all interfaces must inherit from it
public interface IClientInterface;

internal static class ClientObjectHelpers
{
    private static readonly Dictionary<IClientInterface, ClientInterfaceWrapper> ClientInterfaceWrapperMap = new();
    internal class ClientInterfaceWrapper(IClientInterface thing) : ClientObject
    {
        public IClientInterface RawInterface { get; } = thing;
        protected override void ReleaseExtra()
        {
            ClientInterfaceWrapperMap.Remove(RawInterface);
        }
    }
    internal static void PushInterface(IClientInterface thing, bool isReturn)
    {
        switch (thing)
        {
            case null:
                NativeMethods.pushNull();
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
    internal static T PopInterface<T>() where T : IClientInterface
    {
        var id = NativeMethods.popClientInterface();
        if (id != 0)
        {
            // if we're popping it, we must have pushed it in the past sometime
            var wrapper = (ClientInterfaceWrapper)ClientObject.GetById(id);
            // we don't know exactly what it is, only that it implements T
            return (T)wrapper.RawInterface;
        }
        return default;
    }
}
