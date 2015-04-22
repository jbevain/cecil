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

	sealed class UserStringHeap : StringHeap {

		public UserStringHeap (Section section, uint start, uint size)
			: base (section, start, size)
		{
		}

		protected override string ReadStringAt (uint index)
		{
			byte [] data = Section.Data;
			int start = (int) (index + Offset);

			uint length = (uint) (data.ReadCompressedUInt32 (ref start) & ~1);
			if (length < 1)
				return string.Empty;

			var chars = new char [length / 2];

			for (int i = start, j = 0; i < start + length; i += 2)
				chars [j++] = (char) (data [i] | (data [i + 1] << 8));

			return new string (chars);
		}
	}
}
