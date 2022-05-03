namespace TestLibraryClient
{
    internal abstract class ServerObject : ServerResource
    {
        protected ServerObject(int id) : base(id)
        {
        }
        protected override void NativePush(bool inSafeArea)
        {
            NativeMethods.pushInstance(Id, false, inSafeArea);
        }
    }
}
