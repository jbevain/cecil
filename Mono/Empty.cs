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
using Mono.Collections.Generic;

namespace Mono {

	static class Empty<T> {

		public static readonly T [] Array = new T [0];
	}
}

namespace Mono.Cecil {

	static partial class Mixin {

		public static bool IsNullOrEmpty<T> (this T [] self)
		{
			return self == null || self.Length == 0;
		}

		public static bool IsNullOrEmpty<T> (this Collection<T> self)
		{
			return self == null || self.size == 0;
		}

		public static T [] Resize<T> (this T [] self, int length)
		{
			Array.Resize (ref self, length);
			return self;
		}
	}
}
