using System;

namespace TestLibraryClient
{
    public interface INativeBuffer<T>
    {
        public Span<T> GetSpan(out int length);
    }
}
