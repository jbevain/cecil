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
using System.Collections.Generic;

namespace Mono.Cecil.PE {

	sealed class ByteBufferEqualityComparer : IEqualityComparer<ByteBuffer> {

		public bool Equals (ByteBuffer x, ByteBuffer y)
		{
			if (x.length != y.length)
				return false;

			var x_buffer = x.buffer;
			var y_buffer = y.buffer;

			for (int i = 0; i < x.length; i++)
				if (x_buffer [i] != y_buffer [i])
					return false;

			return true;
		}

		public int GetHashCode (ByteBuffer buffer)
		{
#if !BYTE_BUFFER_WELL_DISTRIBUTED_HASH
			var hash = 0;
			var bytes = buffer.buffer;
			for (int i = 0; i < buffer.length; i++)
				hash = (hash * 37) ^ bytes [i];

			return hash;
#else
			const uint p = 16777619;
			uint hash = 2166136261;

			var bytes = buffer.buffer;
			for (int i = 0; i < buffer.length; i++)
			    hash = (hash ^ bytes [i]) * p;

			hash += hash << 13;
			hash ^= hash >> 7;
			hash += hash << 3;
			hash ^= hash >> 17;
			hash += hash << 5;

			return (int) hash;
#endif
		}
	}
}
