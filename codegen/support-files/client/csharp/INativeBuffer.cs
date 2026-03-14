namespace %%TARGET_NAMESPACE%%.Support
{
    public interface INativeBuffer<T> : IDisposable
    {
        public Span<T> GetSpan(out int length);
    }
}
