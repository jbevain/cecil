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

namespace Mono.Cecil.Rocks {

#if INSIDE_ROCKS
	public
#endif
	static class MethodDefinitionRocks {

		public static MethodDefinition GetBaseMethod (this MethodDefinition self)
		{
			if (self == null)
				throw new ArgumentNullException ("self");
			if (!self.IsVirtual)
				return self;
			if (self.IsNewSlot)
				return self;

			var base_type = self.DeclaringType.BaseType;
			while (base_type != null) {
				var @base = GetMatchingMethod (base_type, self);
				if (@base != null)
					return @base;

				var resolved = base_type.Resolve();
				base_type = resolved != null ? resolved.BaseType : null;
			}

			return self;
		}

		public static MethodDefinition GetOriginalBaseMethod (this MethodDefinition self)
		{
			if (self == null)
				throw new ArgumentNullException ("self");

			while (true) {
				var @base = self.GetBaseMethod ();
				if (@base == self)
					return self;

				self = @base;
			}
		}

		static TypeDefinition ResolveBaseType (TypeDefinition type)
		{
			if (type == null)
				return null;

			var base_type = type.BaseType;
			if (base_type == null)
				return null;

			return base_type.Resolve ();
		}

		static MethodDefinition GetMatchingMethod (TypeReference type, MethodDefinition method)
		{
			var resolved = type.Resolve();
			if (resolved == null) return null;

			var genInst = type as GenericInstanceType;
			return MetadataResolver.GetMethod (resolved.Methods, genInst != null ? genInst.GenericArguments : null, method);
		}
	}
}
