//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

using System;
using System.Runtime.InteropServices;

namespace Mono.Cecil.Metadata {

	sealed unsafe class BlobHeap : Heap {

		public BlobHeap (byte* data, uint size)
			: base (data, size)
		{
		}

		public byte [] Read (uint index)
		{
			if (index == 0 || index > this.size - 1)
				return Empty<byte>.Array;

			int position = (int) index;
			int length = (int) Mixin.ReadCompressedUInt32 (data, ref position);

			if (length > size - position)
				return Empty<byte>.Array;

			var buffer = new byte [length];

			Marshal.Copy ((IntPtr)(data + position), buffer, 0, length);

			return buffer;
		}

		public void GetView (uint signature, out byte [] buffer, out int index, out int length)
		{
			buffer = Read (signature);
			index = 0;
			length = buffer.Length;


			/*
			if (signature == 0 || signature > data.Length - 1) {
				buffer = null;
				index = length = 0;
				return;
			}

			buffer = data;

			index = (int) signature;
			length = (int) buffer.ReadCompressedUInt32 (ref index);
			*/
		}
	}
}
