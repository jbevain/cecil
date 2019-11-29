//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

using Mono.Cecil.PE;

namespace Mono.Cecil.Metadata {

	abstract unsafe class Heap {

		public int IndexSize;

		readonly internal byte* data;
		readonly internal uint size;

		public ByteSpan Span => new ByteSpan (data, size);

		protected Heap (byte* data, uint size)
		{
			this.data = data;
			this.size = size;
		}
	}
}
