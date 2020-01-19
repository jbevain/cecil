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

	sealed unsafe class GuidHeap : Heap {

		public GuidHeap (byte* data, uint size)
			: base (data, size)
		{
		}

		public Guid Read (uint index)
		{
			const int guid_size = 16;

			if (index == 0 || ((index - 1) + guid_size) > size)
				return new Guid ();

			if (BitConverter.IsLittleEndian) {
				return *(Guid*) (data + ((index - 1) * guid_size));
			} else {
				var buffer = new byte [guid_size];
				Marshal.Copy ((IntPtr)(data + ((index - 1) * guid_size)), buffer, 0, guid_size);
				return new Guid (buffer);
			}
		}
	}
}
