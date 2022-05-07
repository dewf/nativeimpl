namespace TestLibraryClient.Support
{
    public abstract class ClientObject : ClientResource
    {
        protected override void NativePush()
        {
            NativeMethods.pushInstance(Id, true, false);
        }
        public static ClientObject GetById(int id)
        {
            return (ClientObject)GetResourceById(id);
        }
    }
}
