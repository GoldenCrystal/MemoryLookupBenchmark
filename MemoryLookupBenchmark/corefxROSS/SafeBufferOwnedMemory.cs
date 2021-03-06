﻿using System;
using System.Buffers;
using System.Buffers.ReadOnlySequenceSegment;
using System.Runtime.InteropServices;

namespace MemoryLookupBenchmark.ReadOnlySequenceSegment
{
    /// <summary>Wrapper around <see cref="SafeBuffer"/> usable together with <see cref="ReadOnlySequenceSegment{T}"/>.</summary>
    /// <typeparam name="T"></typeparam>
    public class OwnedMemorySegment<T> : ReadOnlySequenceSegment<T>
    {
        public static ReadOnlySequence<T> CreateSingleSequence(OwnedMemory<T> buffer)
        {
            OwnedMemorySegment<T> first = new OwnedMemorySegment<T>(buffer);

            return new ReadOnlySequence<T>(first, 0, first, first.Memory.Length);
        }

        public static ReadOnlySequence<T> CreateMultiSequence(OwnedMemory<T> buffer, int count)
        {
            OwnedMemorySegment<T> first = new OwnedMemorySegment<T>(buffer);
            OwnedMemorySegment<T> last = first;
            for (var i = 1; i < count; i++)
            {
                last = last.Append(buffer);
            }

            return new ReadOnlySequence<T>(first, 0, last, last.Memory.Length);
        }

        private OwnedMemory<T> _ownedMemory;

        public OwnedMemorySegment(OwnedMemory<T> memory)
        {
            _ownedMemory = memory;
            Memory = memory.Memory;
        }

        public OwnedMemorySegment<T> Append(OwnedMemory<T> memory)
        {
            var segment = new OwnedMemorySegment<T>(memory)
            {
                RunningIndex = RunningIndex + Memory.Length
            };
            Next = segment;
            return segment;
        }
    }

    public sealed class SafeBufferOwnedMemory<T> : OwnedMemory<T>
    {
        public static ReadOnlySequence<T> CreateReadOnlyBuffer(SafeBuffer buffer, long offset, long length)
        {
            int count = (int)Math.Min(length, int.MaxValue);
            OwnedMemorySegment<T> first = new OwnedMemorySegment<T>(new SafeBufferOwnedMemory<T>(buffer, offset, count));
            OwnedMemorySegment<T> last = first;
            length -= count;
            offset += count;

            while (length > 0)
            {
                count = (int)Math.Min(length, int.MaxValue);
                last = last.Append(new SafeBufferOwnedMemory<T>(buffer, offset, count));
                length -= count;
                offset += count;
            }

            return new ReadOnlySequence<T>(first, 0, last, last.Memory.Length);
        }

        private readonly SafeBuffer _buffer;
        private readonly long _offset;
        private readonly int _length;

        private SafeBufferOwnedMemory(SafeBuffer buffer, long offset, int length)
        {
            _buffer = buffer;
            _offset = offset;
            _length = length;
        }

        public override int Length => _length;

        protected override void Dispose(bool disposing) { }

        public override bool IsDisposed => _buffer.IsClosed;
        protected override bool IsRetained => !_buffer.IsClosed;

        public override unsafe Span<T> Span => new Span<T>((byte*)_buffer.DangerousGetHandle() + _offset, _length);

        public override void Retain()
        {
            bool success = false;
            _buffer.DangerousAddRef(ref success);
        }

        public override bool Release()
        {
            _buffer.DangerousRelease();
            return _buffer.IsClosed;
        }

        public override unsafe MemoryHandle Pin(int byteOffset = 0) => new MemoryHandle(this, (byte*)_buffer.DangerousGetHandle() + _offset);

        protected override bool TryGetArray(out ArraySegment<T> arraySegment)
        {
            arraySegment = null;
            return false;
        }
    }
}