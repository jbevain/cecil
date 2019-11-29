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
using System.Text;
using Mono.Cecil.PE;

namespace Mono.Cecil.Metadata {

	unsafe class StringHeap : Heap {

		readonly Dictionary<uint, string> strings = new Dictionary<uint, string> ();

		public StringHeap (byte* data, uint size)
			: base (data, size)
		{
		}

		public string Read (uint index)
		{
			if (index == 0)
				return string.Empty;

			string @string;
			if (strings.TryGetValue (index, out @string))
				return @string;

			if (index > size - 1)
				return string.Empty;

			@string = ReadStringAt (index);
			if (@string.Length != 0)
				strings.Add (index, @string);

			return @string;
		}

		protected virtual string ReadStringAt (uint index)
		{
			int length = 0;
			int start = (int) index;

			for (int i = start; ; i++) {
				if (data [i] == 0)
					break;

				length++;
			}

#if NET_CORE
			return Encoding.UTF8.GetString (this.data + start, length);
#else
			var buffer = new PByteBuffer (this.data + start, (uint) length);
			return Encoding.UTF8.GetString (buffer.ReadBytes (length));
#endif
		}
	}
}
