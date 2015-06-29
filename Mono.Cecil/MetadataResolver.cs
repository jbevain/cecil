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

namespace Mono.Cecil {

	public interface IAssemblyResolver {
		AssemblyDefinition Resolve (AssemblyNameReference name);
		AssemblyDefinition Resolve (AssemblyNameReference name, ReaderParameters parameters);

		AssemblyDefinition Resolve (string fullName);
		AssemblyDefinition Resolve (string fullName, ReaderParameters parameters);
	}

	public interface IMetadataResolver {
		TypeDefinition Resolve (TypeReference type);
		FieldDefinition Resolve (FieldReference field);
		MethodDefinition Resolve (MethodReference method);
	}

#if !SILVERLIGHT && !CF
	[Serializable]
#endif
	public class ResolutionException : Exception {

		readonly MemberReference member;

		public MemberReference Member {
			get { return member; }
		}

		public IMetadataScope Scope {
			get {
				var type = member as TypeReference;
				if (type != null)
					return type.Scope;

				var declaring_type = member.DeclaringType;
				if (declaring_type != null)
					return declaring_type.Scope;

				throw new NotSupportedException ();
			}
		}

		public ResolutionException (MemberReference member)
			: base ("Failed to resolve " + member.FullName)
		{
			if (member == null)
				throw new ArgumentNullException ("member");

			this.member = member;
		}

#if !SILVERLIGHT && !CF
		protected ResolutionException (
			System.Runtime.Serialization.SerializationInfo info,
			System.Runtime.Serialization.StreamingContext context)
			: base (info, context)
		{
		}
#endif
	}

	public class MetadataResolver : IMetadataResolver {

		readonly IAssemblyResolver assembly_resolver;

		public IAssemblyResolver AssemblyResolver {
			get { return assembly_resolver; }
		}

		public MetadataResolver (IAssemblyResolver assemblyResolver)
		{
			if (assemblyResolver == null)
				throw new ArgumentNullException ("assemblyResolver");

			assembly_resolver = assemblyResolver;
		}

		public virtual TypeDefinition Resolve (TypeReference type)
		{
			if (type == null)
				throw new ArgumentNullException ("type");

			type = type.GetElementType ();

			var scope = type.Scope;

			if (scope == null)
				return null;

			switch (scope.MetadataScopeType) {
			case MetadataScopeType.AssemblyNameReference:
				var assembly = assembly_resolver.Resolve ((AssemblyNameReference) scope);
				if (assembly == null)
					return null;

				return GetType (assembly.MainModule, type);
			case MetadataScopeType.ModuleDefinition:
				return GetType ((ModuleDefinition) scope, type);
			case MetadataScopeType.ModuleReference:
				var modules = type.Module.Assembly.Modules;
				var module_ref = (ModuleReference) scope;
				for (int i = 0; i < modules.Count; i++) {
					var netmodule = modules [i];
					if (netmodule.Name == module_ref.Name)
						return GetType (netmodule, type);
				}
				break;
			}

			throw new NotSupportedException ();
		}

		static TypeDefinition GetType (ModuleDefinition module, TypeReference reference)
		{
			var type = GetTypeDefinition (module, reference);
			if (type != null)
				return type;

			if (!module.HasExportedTypes)
				return null;

			var exported_types = module.ExportedTypes;

			for (int i = 0; i < exported_types.Count; i++) {
				var exported_type = exported_types [i];
				if (exported_type.Name != reference.Name)
					continue;

				if (exported_type.Namespace != reference.Namespace)
					continue;

				return exported_type.Resolve ();
			}

			return null;
		}

		static TypeDefinition GetTypeDefinition (ModuleDefinition module, TypeReference type)
		{
			if (!type.IsNested)
				return module.GetType (type.Namespace, type.Name);

			var declaring_type = type.DeclaringType.Resolve ();
			if (declaring_type == null)
				return null;

			return declaring_type.GetNestedType (type.TypeFullName ());
		}

		public virtual FieldDefinition Resolve (FieldReference field)
		{
			if (field == null)
				throw new ArgumentNullException ("field");

			var type = Resolve (field.DeclaringType);
			if (type == null)
				return null;

			if (!type.HasFields)
				return null;

			return GetField (type, field);
		}

		FieldDefinition GetField (TypeDefinition type, FieldReference reference)
		{
			while (type != null) {
				var field = GetField (type.Fields, reference);
				if (field != null)
					return field;

				if (type.BaseType == null)
					return null;

				type = Resolve (type.BaseType);
			}

			return null;
		}

		static FieldDefinition GetField (Collection<FieldDefinition> fields, FieldReference reference)
		{
			for (int i = 0; i < fields.Count; i++) {
				var field = fields [i];

				if (field.Name != reference.Name)
					continue;

				if (!AreSame (field.FieldType, reference.FieldType, null, null))
					continue;

				return field;
			}

			return null;
		}

		public virtual MethodDefinition Resolve (MethodReference method)
		{
			if (method == null)
				throw new ArgumentNullException ("method");

			var type = Resolve (method.DeclaringType);
			if (type == null)
				return null;

			method = method.GetElementMethod ();

			if (!type.HasMethods)
				return null;

			var genInst = method.DeclaringType as GenericInstanceType;
			return GetMethod (type, genInst != null ? genInst.GenericArguments : null, method);
		}

		MethodDefinition GetMethod (TypeDefinition type, Collection<TypeReference> genericArguments, MethodReference reference)
		{
			while (type != null) {
				var method = GetMethod (type.Methods, genericArguments, reference);
				if (method != null)
					return method;

				if (type.BaseType == null)
					return null;

				type = Resolve (type.BaseType);
			}

			return null;
		}

		public static MethodDefinition GetMethod (Collection<MethodDefinition> methods, Collection<TypeReference> genericArguments, MethodReference reference)
		{
			for (int i = 0; i < methods.Count; i++) {
				var method = methods [i];

				if (method.Name != reference.Name)
					continue;

				if (method.HasGenericParameters != reference.HasGenericParameters)
					continue;

				if (method.HasGenericParameters && method.GenericParameters.Count != reference.GenericParameters.Count)
					continue;

				if (!AreSame (method.ReturnType, reference.ReturnType, method.DeclaringType, genericArguments))
					continue;

				if (method.HasParameters != reference.HasParameters)
					continue;

				if (!method.HasParameters && !reference.HasParameters)
					return method;

				if (!AreSame (method.Parameters, reference.Parameters, method.DeclaringType, genericArguments))
					continue;

				return method;
			}

			return null;
		}

		static bool AreSame (Collection<ParameterDefinition> a, Collection<ParameterDefinition> b, TypeDefinition sourceType, Collection<TypeReference> genericArguments)
		{
			var count = a.Count;

			if (count != b.Count)
				return false;

			if (count == 0)
				return true;

			for (int i = 0; i < count; i++)
				if (!AreSame (a [i].ParameterType, b [i].ParameterType, sourceType, genericArguments))
					return false;

			return true;
		}

		static bool AreSame (TypeSpecification a, TypeSpecification b, TypeDefinition sourceType, Collection<TypeReference> genericArguments)
		{
			if (!AreSame (a.ElementType, b.ElementType, sourceType, genericArguments))
				return false;

			if (a.IsGenericInstance)
				return AreSame ((GenericInstanceType) a, (GenericInstanceType) b, sourceType, genericArguments);

			if (a.IsRequiredModifier || a.IsOptionalModifier)
				return AreSame ((IModifierType) a, (IModifierType) b, sourceType, genericArguments);

			if (a.IsArray)
				return AreSame ((ArrayType) a, (ArrayType) b, sourceType, genericArguments);

			return true;
		}

		static bool AreSame (ArrayType a, ArrayType b, TypeDefinition sourceType, Collection<TypeReference> genericArguments)
		{
			if (a.Rank != b.Rank)
				return false;

			// TODO: dimensions

			return true;
		}

		static bool AreSame (IModifierType a, IModifierType b, TypeDefinition sourceType, Collection<TypeReference> genericArguments)
		{
			return AreSame (a.ModifierType, b.ModifierType, sourceType, genericArguments);
		}

		static bool AreSame (GenericInstanceType a, GenericInstanceType b, TypeDefinition sourceType, Collection<TypeReference> genericArguments)
		{
			if (a.GenericArguments.Count != b.GenericArguments.Count)
				return false;

			for (int i = 0; i < a.GenericArguments.Count; i++)
				if (!AreSame (a.GenericArguments [i], b.GenericArguments [i], sourceType, genericArguments))
					return false;

			return true;
		}

		static bool AreSame (GenericParameter a, GenericParameter b, TypeDefinition sourceType, Collection<TypeReference> genericArguments)
		{
			return a.Position == b.Position;
		}

		static bool AreSame (TypeReference a, TypeReference b, TypeDefinition sourceType, Collection<TypeReference> genericArguments)
		{
			if (ReferenceEquals (a, b))
				return true;

			if (a == null || b == null)
				return false;

			if (a.etype == Metadata.ElementType.Var && b.etype != a.etype &&   // Resolve if one is a parameter and the other not (TODO: Reverse order required?)
				sourceType != null && genericArguments != null &&   // Only resolve if data was provided
				sourceType.GenericParameters.Count == genericArguments.Count)   // TODO: Is the count validation correct?
			{
				// Resolve GenericParameter
				int genIndex = sourceType.GenericParameters.IndexOf((GenericParameter) a);
				if (genIndex != -1)
					a = genericArguments[genIndex];
			}

			if (a.etype != b.etype)
				return false;

			if (a.IsGenericParameter)
				return AreSame ((GenericParameter) a, (GenericParameter) b, sourceType, genericArguments);

			if (a.IsTypeSpecification ())
				return AreSame ((TypeSpecification) a, (TypeSpecification) b, sourceType, genericArguments);

			if (a.Name != b.Name || a.Namespace != b.Namespace)
				return false;

			//TODO: check scope

			return AreSame (a.DeclaringType, b.DeclaringType, sourceType, genericArguments);
		}
	}
}
