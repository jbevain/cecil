//
// TypeDefinitionRocks.cs
//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2011 Jb Evain
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Linq;

namespace Mono.Cecil.Rocks {

#if INSIDE_ROCKS
	public
#endif
	static class TypeDefinitionRocks {

		public static IEnumerable<IMethodDefinition> GetConstructors (this ITypeDefinition self)
		{
			if (self == null)
				throw new ArgumentNullException ("self");

			if (!self.HasMethods)
				return Empty<IMethodDefinition>.Array;

			return self.Methods.Where (method => method.IsConstructor);
		}

		public static IMethodDefinition GetStaticConstructor (this ITypeDefinition self)
		{
			if (self == null)
				throw new ArgumentNullException ("self");

			if (!self.HasMethods)
				return null;

			return self.GetConstructors ().FirstOrDefault (ctor => ctor.IsStatic);
		}

		public static IEnumerable<IMethodDefinition> GetMethods (this ITypeDefinition self)
		{
			if (self == null)
				throw new ArgumentNullException ("self");

			if (!self.HasMethods)
				return Empty<IMethodDefinition>.Array;

			return self.Methods.Where (method => !method.IsConstructor);
		}

		public static ITypeReference GetEnumUnderlyingType (this ITypeDefinition self)
		{
			if (self == null)
				throw new ArgumentNullException ("self");
			if (!self.IsEnum)
				throw new ArgumentException ();

			return Mixin.GetEnumUnderlyingType (self);
		}
	}
}
