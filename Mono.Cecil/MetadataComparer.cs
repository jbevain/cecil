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

using Mono.Collections.Generic;

namespace Mono.Cecil {

	public sealed class MetadataComparer :
		IEqualityComparer<TypeReference>,
		IEqualityComparer<FieldReference>,
		IEqualityComparer<MethodReference>,
		IEqualityComparer<AssemblyNameReference> {

		private readonly bool compare_declaring_type;

		private MetadataComparer (bool compare_declaring_type)
		{
			this.compare_declaring_type = compare_declaring_type;
		}

		public static readonly MetadataComparer Default = new MetadataComparer (true);
		public static readonly MetadataComparer OnlyDeclaration = new MetadataComparer (false);

		public static bool AreSame (FieldReference a, FieldReference b, bool compare_declaring_type)
		{
			if (ReferenceEquals (a, b))
				return true;

			if (a == null || b == null)
				return false;

			if (a.Name != b.Name)
				return false;

			if (compare_declaring_type && !AreSame (a.DeclaringType, b.DeclaringType))
				return false;

			if (!AreSame (a.FieldType, b.FieldType))
				return false;

			return true;
		}

		public static bool AreSame (MethodReference a, MethodReference b, bool compareDeclaringType)
		{
			if (ReferenceEquals (a, b))
				return true;

			if (a == null || b == null)
				return false;

			if (a.Name != b.Name)
				return false;

			if (a.HasGenericParameters != b.HasGenericParameters)
				return false;

			if (a.HasGenericParameters && a.GenericParameters.Count != b.GenericParameters.Count)
				return false;

			if (compareDeclaringType && !AreSame (a.DeclaringType, b.DeclaringType))
				return false;

			if (!AreSame (a.ReturnType, b.ReturnType))
				return false;

			if (a.IsVarArg () != b.IsVarArg ())
				return false;

			if (a.IsVarArg () && IsVarArgCallTo (a, b))
				return true;

			if (a.HasParameters != b.HasParameters)
				return false;

			if (!a.HasParameters)
				return true;

			if (!AreSame (a.Parameters, b.Parameters))
				return false;

			return true;
		}

		static bool AreSame (Collection<ParameterDefinition> a, Collection<ParameterDefinition> b)
		{
			var count = a.Count;

			if (count != b.Count)
				return false;

			if (count == 0)
				return true;

			for (int i = 0; i < count; i++)
				if (!AreSame (a [i].ParameterType, b [i].ParameterType))
					return false;

			return true;
		}

		static bool IsVarArgCallTo (MethodReference method, MethodReference reference)
		{
			if (method.Parameters.Count >= reference.Parameters.Count)
				return false;

			if (reference.GetSentinelPosition () != method.Parameters.Count)
				return false;

			for (int i = 0; i < method.Parameters.Count; i++)
				if (!AreSame (method.Parameters [i].ParameterType, reference.Parameters [i].ParameterType))
					return false;

			return true;
		}

		static bool AreSame (TypeSpecification a, TypeSpecification b)
		{
			if (!AreSame (a.ElementType, b.ElementType))
				return false;

			if (a.IsGenericInstance)
				return AreSame ((GenericInstanceType) a, (GenericInstanceType) b);

			if (a.IsRequiredModifier || a.IsOptionalModifier)
				return AreSame ((IModifierType) a, (IModifierType) b);

			if (a.IsArray)
				return AreSame ((ArrayType) a, (ArrayType) b);

			return true;
		}

		static bool AreSame (ArrayType a, ArrayType b)
		{
			if (a.Rank != b.Rank)
				return false;

			// TODO: dimensions

			return true;
		}

		static bool AreSame (IModifierType a, IModifierType b)
		{
			return AreSame (a.ModifierType, b.ModifierType);
		}

		static bool AreSame (GenericInstanceType a, GenericInstanceType b)
		{
			if (a.GenericArguments.Count != b.GenericArguments.Count)
				return false;

			for (int i = 0; i < a.GenericArguments.Count; i++)
				if (!AreSame (a.GenericArguments [i], b.GenericArguments [i]))
					return false;

			return true;
		}

		static bool AreSame (GenericParameter a, GenericParameter b)
		{
			return a.Position == b.Position;
		}

		public static bool AreSame (TypeReference a, TypeReference b)
		{
			if (ReferenceEquals (a, b))
				return true;

			if (a == null || b == null)
				return false;

			if (a.etype != b.etype)
				return false;

			if (a.IsGenericParameter)
				return AreSame ((GenericParameter) a, (GenericParameter) b);

			if (a.IsTypeSpecification ())
				return AreSame ((TypeSpecification) a, (TypeSpecification) b);

			if (a.Name != b.Name || a.Namespace != b.Namespace)
				return false;

			// ECMA-335 standard, 8.5.2: Assemblies and scoping
			// nested types are scoped by their declaring type
			if (!AreSame (a.DeclaringType, b.DeclaringType))
				return false;

			if (a.DeclaringType != null)
				return true;

			// non-nested types are scoped by their assembly
			return AreSame (GetScopeAssemblyName (a), GetScopeAssemblyName (b));
		}

		static AssemblyNameReference GetScopeAssemblyName (TypeReference type)
		{
			var scope = type.Scope;

			if (scope == null)
				return null;

			switch (scope.MetadataScopeType) {
			case MetadataScopeType.AssemblyNameReference:
				return (AssemblyNameReference) scope;

			case MetadataScopeType.ModuleDefinition:
				return ((ModuleDefinition) scope).Assembly.Name;

			case MetadataScopeType.ModuleReference:
				return type.Module.Assembly.Name;
			}

			throw new NotSupportedException ();
		}

		static bool Equals (byte [] a, byte [] b)
		{
			if (ReferenceEquals (a, b))
				return true;
			if (a == null)
				return false;
			if (a.Length != b.Length)
				return false;
			for (int i = 0; i < a.Length; i++)
				if (a [i] != b [i])
					return false;
			return true;
		}

		public static bool AreSame (AssemblyNameReference a, AssemblyNameReference b)
		{
			if (ReferenceEquals (a, b))
				return true;

			if (a == null || b == null)
				return false;

			if (a.Name != b.Name)
				return false;

			if (!Equals (a.Version, b.Version))
				return false;

			if (a.Culture != b.Culture)
				return false;

			if (!Equals (a.PublicKeyToken, b.PublicKeyToken))
				return false;

			return true;
		}

		bool IEqualityComparer<TypeReference>.Equals (TypeReference x, TypeReference y)
		{
			return AreSame (x, y);
		}

		int IEqualityComparer<TypeReference>.GetHashCode (TypeReference obj)
		{
			return obj != null ? obj.Name.GetHashCode () : 0;
		}

		bool IEqualityComparer<FieldReference>.Equals (FieldReference x, FieldReference y)
		{
			return AreSame (x, y, compare_declaring_type);
		}

		int IEqualityComparer<FieldReference>.GetHashCode (FieldReference obj)
		{
			return obj != null ? obj.Name.GetHashCode () : 0;
		}

		bool IEqualityComparer<MethodReference>.Equals (MethodReference x, MethodReference y)
		{
			return AreSame (x, y, compare_declaring_type);
		}

		int IEqualityComparer<MethodReference>.GetHashCode (MethodReference obj)
		{
			return obj != null ? obj.Name.GetHashCode () : 0;
		}

		bool IEqualityComparer<AssemblyNameReference>.Equals (AssemblyNameReference x, AssemblyNameReference y)
		{
			return AreSame (x, y);
		}

		int IEqualityComparer<AssemblyNameReference>.GetHashCode (AssemblyNameReference obj)
		{
			return obj != null ? obj.Name.GetHashCode () : 0;
		}
	}
}
