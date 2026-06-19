using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace %%TARGET_NAMESPACE%%.Support
{
    public class ClientBuffer<T> : ClientResource, INativeBuffer<T> where T : unmanaged
    {
        private IntPtr _block;
        private bool _disposed;
        private readonly int _elementSize;
        private readonly int[] _dims;
        private readonly long _totalCount;
        private readonly long _totalSize;
        public ClientBuffer(params int[] dims)
        {
            _dims = dims;
            _elementSize = Marshal.SizeOf<T>();
            _totalCount = dims.Aggregate(1L, (product, i) => product * i);
            _totalSize = _totalCount * _elementSize;
            _block = Marshal.AllocHGlobal((IntPtr)_totalSize);
            GC.AddMemoryPressure(_totalSize);
            SupportLogger.Log(LogLevel.Trace, "ClientBuffer {block} allocated (from dimensions)", _block);
        }
        private static unsafe void GenericArrayCopy(T[] sourceArray, IntPtr destination) 
        {
            var byteCount = sourceArray.Length * sizeof(T);
            fixed (T* srcPtr = sourceArray)
            {
                Buffer.MemoryCopy(srcPtr, (void*)destination, byteCount, byteCount);
            }
        }
        private static unsafe void GenericSpanCopy(Span<T> sourceSpan, IntPtr destination)
        {
            var byteCount = sourceSpan.Length * sizeof(T);
            fixed (T* srcPtr = sourceSpan)
            {
                Buffer.MemoryCopy(srcPtr, (void*)destination, byteCount, byteCount);
            }
        }
        public ClientBuffer(T[] data)
        {
            _dims = [data.Length];
            _elementSize = Marshal.SizeOf<T>();
            _totalCount = data.Length;
            _totalSize = _totalCount * _elementSize;
            _block = Marshal.AllocHGlobal((IntPtr)_totalSize);
            GC.AddMemoryPressure(_totalSize);
            SupportLogger.Log(LogLevel.Trace, "ClientBuffer {block} allocated (from array)", _block);
            GenericArrayCopy(data, _block);
        }
        public ClientBuffer(Span<T> data)
        {
            _dims = [data.Length];
            _elementSize = Marshal.SizeOf<T>();
            _totalCount = data.Length;
            _totalSize = _totalCount * _elementSize;
            _block = Marshal.AllocHGlobal((IntPtr)_totalSize);
            GC.AddMemoryPressure(_totalSize);
            SupportLogger.Log(LogLevel.Trace, "ClientBuffer {block} allocated (from span)", _block);
            GenericSpanCopy(data, _block);
        }
        
        // // currently this just makes a copy
        // // but would we ever want to wrap+pin an existing memory array? so that it's always current on the native side?
        // public unsafe ClientBuffer(T[] sourceArray)
        // {
        //     _dims = [sourceArray.Length];
        //     _elementSize = Marshal.SizeOf<T>();
        //     _totalCount = sourceArray.Length;
        //     _totalSize = _totalCount * _elementSize;
        //     _block = Marshal.AllocHGlobal((IntPtr)_totalSize);
        //     GC.AddMemoryPressure(_totalSize);
        //     // copy from sourceArray to block
        //     fixed (T* p = sourceArray)
        //     {
        //         IntPtr sourcePtr = (IntPtr)p;
        //         Buffer.MemoryCopy(sourcePtr.ToPointer(), _block.ToPointer(), _totalSize, _totalSize);
        //     }
        //     // done
        //     InternalLogger.Log(LogLevel.Trace, "ClientBuffer {0} allocated", _block);
        // }
        
        public Span<T> GetSpan(out int length)
        {
            unsafe
            {
                var ptr = _block.ToPointer();
                length = (int)_totalCount;
                return new Span<T>(ptr, length);
            }
        }
        
        protected override void NativePush()
        {
            NativeMethods.BufferDescriptor descriptor;
            descriptor.Start = _block;
            descriptor.ElementSize = _elementSize;
            descriptor.TotalCount = (IntPtr)_totalCount;
            descriptor.TotalSize = (IntPtr)_totalSize;
            NativeMethods.pushBuffer(Id, true, ref descriptor);
        }

        ~ClientBuffer()
        {
            if (!_disposed) Dispose();
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            Marshal.FreeHGlobal(_block);
            GC.RemoveMemoryPressure(_totalSize);
            SupportLogger.Log(LogLevel.Trace, "ClientBuffer {block} disposed", _block);
            _block = IntPtr.Zero;
            _disposed = true;
        }

        public static ClientBuffer<T> GetById(int id)
        {
            return (ClientBuffer<T>)GetResourceById(id);
        }
    }
}
