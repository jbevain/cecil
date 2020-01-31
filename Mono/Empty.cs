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

namespace Mono {

	static class Empty<T> {

		#if NET40
		public static readonly T [] Array = new T[0];
		#else
		public static readonly T [] Array = System.Array.Empty<T> ();
		#endif
	}

	class ArgumentNullOrEmptyException : ArgumentException {

		public ArgumentNullOrEmptyException (string paramName)
			: base ("Argument null or empty", paramName)
		{
		}
	}
}

namespace Mono.Cecil {

	static partial class Mixin {

		public static bool IsNullOrEmpty<T> (this ICollection<T> self) => self == null || self.Count == 0;

		public static T [] Add<T> (this T [] self, T item)
		{
			if (self == null) {
				self = new [] { item };
				return self;
			}

			Array.Resize (ref self, self.Length + 1);
			self [self.Length - 1] = item;
			return self;
		}
	}
}
