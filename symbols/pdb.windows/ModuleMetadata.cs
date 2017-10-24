// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

#if !READ_ONLY

using System.Collections.Generic;
using Microsoft.DiaSymReader;

namespace Mono.Cecil.WindowsPdb {

	class ModuleMetadata : ISymWriterMetadataProvider {

		readonly ModuleDefinition module;

		Dictionary<uint, TypeDefinition> types;
		Dictionary<uint, MethodDefinition> methods;

		public ModuleMetadata (ModuleDefinition module)
		{
			this.module = module;
		}

		bool TryGetType (uint token, out TypeDefinition type)
		{
			if (types == null)
				InitializeMetadata (module);

			return types.TryGetValue (token, out type);
		}

		bool TryGetMethod (uint token, out MethodDefinition method)
		{
			if (methods == null)
				InitializeMetadata (module);

			return methods.TryGetValue (token, out method);
		}

		void InitializeMetadata (ModuleDefinition module)
		{
			types = new Dictionary<uint, TypeDefinition> ();
			methods = new Dictionary<uint, MethodDefinition> ();

			foreach (var type in module.GetTypes ()) {
				types.Add (type.MetadataToken.ToUInt32 (), type);
				InitializeMethods (type);
			}
		}

		void InitializeMethods (TypeDefinition type)
		{
			foreach (var method in type.Methods)
				methods.Add (method.MetadataToken.ToUInt32 (), method);
		}

		bool ISymWriterMetadataProvider.TryGetTypeDefinitionInfo (int typeDefinitionToken, out string namespaceName, out string typeName, out System.Reflection.TypeAttributes attributes, out int baseTypeToken)
		{
			TypeDefinition type;
			if (!TryGetType ((uint)typeDefinitionToken, out type)) {
				namespaceName = null;
				typeName = null;
				attributes = 0;
				baseTypeToken = 0;
				return false;
			}

			typeName = type.IsNested ? type.Name : type.FullName;
			namespaceName = type.Namespace;
			attributes = (System.Reflection.TypeAttributes)type.Attributes;
			baseTypeToken = type.BaseType.MetadataToken.ToInt32 ();
			return true;
		}

		bool ISymWriterMetadataProvider.TryGetEnclosingType (int nestedTypeToken, out int enclosingTypeToken)
		{
			TypeDefinition type;
			if (!TryGetType ((uint)nestedTypeToken, out type) || !type.IsNested) {
				enclosingTypeToken = 0;
				return false;
			}

			enclosingTypeToken = type.DeclaringType.MetadataToken.ToInt32 ();
			return true;
		}

		bool ISymWriterMetadataProvider.TryGetMethodInfo (int methodDefinitionToken, out string methodName, out int declaringTypeToken)
		{
			MethodDefinition method;
			if (!TryGetMethod ((uint)methodDefinitionToken, out method)) {
				methodName = null;
				declaringTypeToken = 0;
				return false;
			}

			declaringTypeToken = method.DeclaringType.MetadataToken.ToInt32 ();
			methodName = method.Name;
			return true;
		}
	}
}

#endif
