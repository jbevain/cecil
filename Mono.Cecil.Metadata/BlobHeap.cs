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

using Mono.Cecil.PE;

namespace Mono.Cecil.Metadata {

	sealed class BlobHeap : Heap {

		public BlobHeap (Section section, uint start, uint size)
			: base (section, start, size)
		{
		}

		public byte [] Read (uint index)
		{
			if (index == 0 || index > Size - 1)
				return Empty<byte>.Array;

			var data = Section.Data;

			int position = (int) (index + Offset);
			int length = (int) data.ReadCompressedUInt32 (ref position);

			var buffer = new byte [length];

			Buffer.BlockCopy (data, position, buffer, 0, length);

			return buffer;
		}
	}
}
