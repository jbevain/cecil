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
using System.Linq;

namespace Mono.Cecil.Rocks {

#if INSIDE_ROCKS
	public
#endif
	static class TypeDefinitionRocks {

		public static IEnumerable<MethodDefinition> GetConstructors (this TypeDefinition self)
		{
			if (self == null)
				throw new ArgumentNullException ("self");

			if (!self.HasMethods)
				return Empty<MethodDefinition>.Array;

			return self.Methods.Where (method => method.IsConstructor);
		}

		public static MethodDefinition GetStaticConstructor (this TypeDefinition self)
		{
			if (self == null)
				throw new ArgumentNullException ("self");

			if (!self.HasMethods)
				return null;

			return self.GetConstructors ().FirstOrDefault (ctor => ctor.IsStatic);
		}

		public static IEnumerable<MethodDefinition> GetMethods (this TypeDefinition self)
		{
			if (self == null)
				throw new ArgumentNullException ("self");

			if (!self.HasMethods)
				return Empty<MethodDefinition>.Array;

			return self.Methods.Where (method => !method.IsConstructor);
		}

		public static TypeReference GetEnumUnderlyingType (this TypeDefinition self)
		{
			if (self == null)
				throw new ArgumentNullException ("self");
			if (!self.IsEnum)
				throw new ArgumentException ();

			return Mixin.GetEnumUnderlyingType (self);
		}

        public static bool IsAssignableFrom(this TypeDefinition type, TypeDefinition test)
        {
            if (type.IsSameAs(test))
                return true;

            if (type.IsInterface)
                return test.Interfaces.Any(i => i.Resolve().IsSameAs(type));

            if (type.IsValueType)
                return false;

            return test.IsSubclassOf(type);
        }

        public static bool IsEventuallyAccessible(this TypeDefinition type)
        {
            if (type.IsPublic)
                return true;

            if (type.IsNested)
            {
                if (type.IsNestedPublic)
                    return true;
                if (type.IsNestedFamily)
                    return true;
                if (type.IsNestedFamilyOrAssembly)
                    return true;
            }

            return false;
        }
        
        public static bool IsSubclassOf(this TypeDefinition type, TypeDefinition test)
        {
            if (type == null)
                throw new NullReferenceException();
            if (test == null)
                throw new ArgumentNullException();

            if (test.IsInterface)
                return test.IsAssignableFrom(type);

            var baseType = type.BaseType;
            if (baseType == null)
                return false;
            type = baseType.Resolve();

            if (type.IsSameAs(test))
                    return true;

            return type.IsSubclassOf(test);
        }
    }
}
