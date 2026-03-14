namespace %%TARGET_NAMESPACE%%.Support
{
    internal abstract class ServerObject(int id) : ServerResource(id)
    {
        protected override void NativePush()
        {
            NativeMethods.pushInstance(Id, false);
        }
    }
}
