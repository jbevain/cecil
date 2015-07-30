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

	sealed class GuidHeap : Heap {

		public GuidHeap (Section section, uint start, uint size)
			: base (section, start, size)
		{
		}

		public Guid Read (uint index)
		{
			const int guid_size = 16;

			if (index == 0 || ((index - 1) + guid_size) > Size)
				return new Guid ();

			var buffer = new byte [guid_size];

			Buffer.BlockCopy (Section.Data, (int) (Offset + ((index - 1) * guid_size)), buffer, 0, guid_size);

			return new Guid (buffer);
		}
	}
}
