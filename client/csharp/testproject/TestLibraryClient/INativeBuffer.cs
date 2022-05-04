using System;

namespace TestLibraryClient
{
    public interface INativeBuffer<T> : IDisposable
    {
        public Span<T> GetSpan(out int length);
    }
}
