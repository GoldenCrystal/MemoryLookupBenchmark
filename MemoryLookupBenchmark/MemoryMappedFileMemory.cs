﻿using Microsoft.Win32.SafeHandles;
using System;
using System.Buffers;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;

namespace MemoryLookupBenchmark
{
	[StructLayout(LayoutKind.Sequential)]
	internal unsafe sealed class MemoryMappedFileMemory : IDisposable
	{
		private readonly SafeMemoryMappedViewHandle _buffer;
		private readonly long _dataOffset;
		private readonly long _dataLength;
		private MemoryMappedFile _memoryMappedFile;
		private MemoryMappedViewAccessor _memoryMappedViewAccessor;

		public MemoryMappedFileMemory(long size)
		{
			_memoryMappedFile = MemoryMappedFile.CreateNew(null, size, MemoryMappedFileAccess.ReadWrite, MemoryMappedFileOptions.None, HandleInheritability.None);
			_memoryMappedViewAccessor = _memoryMappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
			_dataOffset = _memoryMappedViewAccessor.PointerOffset;
			_dataLength = size;

			var handle = _memoryMappedViewAccessor.SafeMemoryMappedViewHandle;

			bool success = false;
			handle.DangerousAddRef(ref success);

			_buffer = handle;
		}

		public void Dispose()
		{
			if (Interlocked.Exchange(ref _memoryMappedViewAccessor, null) is var memoryMappedViewAccessor)
			{
				_buffer.DangerousRelease();
				memoryMappedViewAccessor.Dispose();
			}
			Interlocked.Exchange(ref _memoryMappedFile, null)?.Dispose();
		}

		public byte* DataPointer => (byte*)_buffer.DangerousGetHandle() + _dataOffset;

		public long Length => _dataLength;

		public BufferSlice<byte> Slice(long offset, int length)
		{
			if (unchecked((ulong)offset) > (ulong)_dataLength || (ulong)length > (ulong)(_dataLength - offset))
			{
				ThrowHelper.ThrowArgumentOutOfRangeException();
			}

			return new BufferSlice<byte>(_buffer, _dataOffset + offset, length);
		}

		public BufferSlice<byte> SliceWithoutBoundsChecking(long offset, int length) => new BufferSlice<byte>(_buffer, _dataOffset + offset, length);

		public System.Buffers.ReadOnlySequenceSegment.ReadOnlySequence<byte> CreateReadOnlyBufferROSS() => ReadOnlySequenceSegment.SafeBufferOwnedMemory<byte>.CreateReadOnlyBuffer(_buffer, _dataOffset, _dataLength);

        public System.Buffers.IMemoryList.ReadOnlySequence<byte> CreateReadOnlyBufferIMemoryList() => IMemoryList.SafeBufferOwnedMemory<byte>.CreateReadOnlyBuffer(_buffer, _dataOffset, _dataLength);

        public System.Buffers.Current.ReadOnlySequence<byte> CreateReadOnlyBufferCurrent() => Current.SafeBufferOwnedMemory<byte>.CreateReadOnlyBuffer(_buffer, _dataOffset, _dataLength);
    }
}
