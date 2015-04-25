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

namespace Mono.Cecil.Metadata {

	sealed class GuidHeap : Heap {

		public GuidHeap (byte [] data)
			: base (data)
		{
		}

		public Guid Read (uint index)
		{
			if (index == 0)
				return new Guid ();

			const int guid_size = 16;

			var buffer = new byte [guid_size];

			index--;

			Buffer.BlockCopy (this.data, (int) index, buffer, 0, guid_size);

			return new Guid (buffer);
		}
	}
}
