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
using System.Text;

namespace Mono.Cecil.Rocks {

	public class DocCommentId
	{
		IMemberDefinition commentMember;
		StringBuilder id;

		DocCommentId (IMemberDefinition member)
		{
			commentMember = member;
			id = new StringBuilder ();
		}

		void WriteField (FieldDefinition field)
		{
			WriteDefinition ('F', field);
		}

		void WriteEvent (EventDefinition @event)
		{
			WriteDefinition ('E', @event);
		}

		void WriteType (TypeDefinition type)
		{
			id.Append ('T').Append (':');
			WriteTypeFullName (type);
		}

		void WriteMethod (MethodDefinition method)
		{
			WriteDefinition ('M', method);

			if (method.HasGenericParameters) {
				id.Append ('`').Append ('`');
				id.Append (method.GenericParameters.Count);
			}

			if (method.HasParameters)
				WriteParameters (method.Parameters);

			if (IsConversionOperator (method))
				WriteReturnType (method);
		}

		static bool IsConversionOperator (MethodDefinition self)
		{
			if (self == null)
				throw new ArgumentNullException ("self");

			return self.IsSpecialName
				&& (self.Name == "op_Explicit" || self.Name == "op_Implicit");
		}

		void WriteReturnType (MethodDefinition method)
		{
			id.Append ('~');
			WriteTypeSignature (method.ReturnType);
		}

		void WriteProperty (PropertyDefinition property)
		{
			WriteDefinition ('P', property);

			if (property.HasParameters)
				WriteParameters (property.Parameters);
		}

		void WriteParameters (IList<ParameterDefinition> parameters)
		{
			id.Append ('(');
			WriteList (parameters, p => WriteTypeSignature (p.ParameterType));
			id.Append (')');
		}

		void WriteTypeSignature (TypeReference type)
		{
			switch (type.MetadataType)
			{
				case MetadataType.Array:
					WriteArrayTypeSignature ((ArrayType) type);
					break;
				case MetadataType.ByReference:
					WriteTypeSignature (((ByReferenceType) type).ElementType);
					id.Append ('@');
					break;
				case MetadataType.FunctionPointer:
					WriteFunctionPointerTypeSignature ((FunctionPointerType) type);
					break;
				case MetadataType.GenericInstance:
					WriteGenericInstanceTypeSignature ((GenericInstanceType) type);
					break;
				case MetadataType.Var:
					if (IsGenericMethodTypeParameter (type))
						id.Append ('`');
					id.Append ('`');
					id.Append (((GenericParameter) type).Position);
					break;
				case MetadataType.MVar:
					id.Append ('`').Append ('`');
					id.Append (((GenericParameter) type).Position);
					break;
				case MetadataType.OptionalModifier:
					WriteModiferTypeSignature ((OptionalModifierType) type, '!');
					break;
				case MetadataType.RequiredModifier:
					WriteModiferTypeSignature ((RequiredModifierType) type, '|');
					break;
				case MetadataType.Pointer:
					WriteTypeSignature (((PointerType) type).ElementType);
					id.Append ('*');
					break;
				default:
					WriteTypeFullName (type);
					break;
			}
		}

		bool IsGenericMethodTypeParameter (TypeReference type)
		{
			if (commentMember is MethodDefinition methodDefinition && type is GenericParameter genericParameter)
				return methodDefinition.GenericParameters.Any (i => i.Name == genericParameter.Name);

			return false;
		}

		void WriteGenericInstanceTypeSignature (GenericInstanceType type)
		{
			if (type.ElementType.IsTypeSpecification ())
				throw new NotSupportedException ();

			GenericTypeOptions options = new GenericTypeOptions {
				StripGenericArity = true,
				IsNestedType = type.IsNested,
				Arguments = type.GenericArguments
			};

			WriteTypeFullName (type.ElementType, options);
			WriteGenericInstanceTypeArguments (type);
		}

		void WriteGenericInstanceTypeArguments (GenericInstanceType type)
		{
			if (!type.IsNested) {
				id.Append ('{');
				WriteList (type.GenericArguments, WriteTypeSignature);
				id.Append ('}');
			}
		}

		void WriteList<T> (IList<T> list, Action<T> action)
		{
			for (int i = 0; i < list.Count; i++) {
				if (i > 0)
					id.Append (',');

				action (list [i]);
			}
		}

		void WriteModiferTypeSignature (IModifierType type, char id)
		{
			WriteTypeSignature (type.ElementType);
			this.id.Append (id);
			WriteTypeSignature (type.ModifierType);
		}

		void WriteFunctionPointerTypeSignature (FunctionPointerType type)
		{
			id.Append ("=FUNC:");
			WriteTypeSignature (type.ReturnType);

			if (type.HasParameters)
				WriteParameters (type.Parameters);
		}

		void WriteArrayTypeSignature (ArrayType type)
		{
			WriteTypeSignature (type.ElementType);

			if (type.IsVector) {
				id.Append ("[]");
				return;
			}

			id.Append ("[");

			WriteList (type.Dimensions, dimension => {
				if (dimension.LowerBound.HasValue)
					id.Append (dimension.LowerBound.Value);

				id.Append (':');

				if (dimension.UpperBound.HasValue)
					id.Append (dimension.UpperBound.Value - (dimension.LowerBound.GetValueOrDefault () + 1));
			});

			id.Append ("]");
		}

		void WriteDefinition (char id, IMemberDefinition member)
		{
			this.id.Append (id)
				.Append (':');

			WriteTypeFullName (member.DeclaringType);
			this.id.Append ('.');
			WriteItemName (member.Name);
		}

		void WriteTypeFullName (TypeReference type)
		{
			WriteTypeFullName (type, GenericTypeOptions.Empty ());
		}

		void WriteTypeFullName (TypeReference type, GenericTypeOptions options)
		{
			if (type.DeclaringType != null) {
				WriteTypeFullName (type.DeclaringType, options);
				id.Append ('.');
			}

			if (!string.IsNullOrEmpty (type.Namespace)) {
				id.Append (type.Namespace);
				id.Append ('.');
			}

			var name = type.Name;

			if (options.StripGenericArity) {
				var index = name.LastIndexOf ('`');
				if (index > 0)
					name = name.Substring (0, index);
			}

			id.Append (name);

			WriteNestedGenericTypeParameters (type, options);
		}

		void WriteNestedGenericTypeParameters (TypeReference type, GenericTypeOptions options)
		{
			var isNestedGenericType = options.IsNestedType && IsGenericType (type);
			if (isNestedGenericType) {
				id.Append ('{');
				WriteList (GetNestedGenericTypeArguments (type, options), WriteTypeSignature);
				id.Append ('}');
			}
		}

		static bool IsGenericType (TypeReference type)
		{
			// When the nested type is defined in a generic class,
			// the nested type will have generic parameters but maybe it is not a generic type.
			if (type.HasGenericParameters) {
				var name = string.Empty;
				var index = type.Name.LastIndexOf ('`');
				if (index >= 0)
					name = type.Name.Substring (0, index);

				return type.Name.LastIndexOf ('`') == name.Length;
			}

			return false;
		}

		IList<TypeReference> GetNestedGenericTypeArguments (TypeReference type, GenericTypeOptions options)
		{
			var typeParameterCount = GetNestedGenericTypeParameterCount (type);
			var typeGenericArguments = options.Arguments.Skip (options.ArgumentIndex).Take (typeParameterCount).ToList ();

			options.ArgumentIndex += typeParameterCount;

			return typeGenericArguments;
		}

		int GetNestedGenericTypeParameterCount (TypeReference type)
		{
			var returnValue = 0;
			var index = type.Name.LastIndexOf ('`');
			if (index >= 0)
				returnValue = int.Parse (type.Name.Substring (index + 1));

			return returnValue;
		}

		void WriteItemName (string name)
		{
			id.Append (name.Replace ('.', '#').Replace('<', '{').Replace('>', '}'));
		}

		public override string ToString ()
		{
			return id.ToString ();
		}

		public static string GetDocCommentId (IMemberDefinition member)
		{
			if (member == null)
				throw new ArgumentNullException ("member");

			var documentId = new DocCommentId (member);

			switch (member.MetadataToken.TokenType)
			{
				case TokenType.Field:
					documentId.WriteField ((FieldDefinition) member);
					break;
				case TokenType.Method:
					documentId.WriteMethod ((MethodDefinition) member);
					break;
				case TokenType.TypeDef:
					documentId.WriteType ((TypeDefinition) member);
					break;
				case TokenType.Event:
					documentId.WriteEvent ((EventDefinition) member);
					break;
				case TokenType.Property:
					documentId.WriteProperty ((PropertyDefinition) member);
					break;
				default:
					throw new NotSupportedException (member.FullName);
			}

			return documentId.ToString ();
		}

		class GenericTypeOptions {
			public bool StripGenericArity { get; set; }

			public bool IsNestedType { get; set; }

			public IList<TypeReference> Arguments { get; set; }

			public int ArgumentIndex { get; set; }

			public static GenericTypeOptions Empty ()
			{
				return new GenericTypeOptions ();
			}
		}
	}
}
