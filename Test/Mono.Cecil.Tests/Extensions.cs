using System;
using System.Linq;
using SR = System.Reflection;

using Mono.Cecil;

namespace Mono.Cecil.Tests {

	public static class Extensions {

		public static IMethodDefinition GetMethod (this ITypeDefinition self, string name)
		{
			return self.Methods.Where (m => m.Name == name).First ();
		}

		public static FieldDefinition GetField (this ITypeDefinition self, string name)
		{
			return self.Fields.Where (f => f.Name == name).First ();
		}

		public static TypeDefinition ToDefinition (this Type self)
		{
			var module = ModuleDefinition.ReadModule (self.Module.FullyQualifiedName);
			return (TypeDefinition) module.LookupToken (self.MetadataToken);
		}

		public static IMethodDefinition ToDefinition (this SR.MethodBase method)
		{
			var declaring_type = method.DeclaringType.ToDefinition ();
			return (MethodDefinition) declaring_type.Module.LookupToken (method.MetadataToken);
		}

		public static FieldDefinition ToDefinition (this SR.FieldInfo field)
		{
			var declaring_type = field.DeclaringType.ToDefinition ();
			return (FieldDefinition) declaring_type.Module.LookupToken (field.MetadataToken);
		}

		public static ITypeReference MakeGenericType (this ITypeReference self, params ITypeReference [] arguments)
		{
			if (self.GenericParameters.Count != arguments.Length)
				throw new ArgumentException ();

			var instance = new GenericInstanceType (self);
			foreach (var argument in arguments)
				instance.GenericArguments.Add (argument);

			return instance;
		}

		public static MethodReference MakeGenericMethod (this IMethodReference self, params ITypeReference [] arguments)
		{
			if (self.GenericParameters.Count != arguments.Length)
				throw new ArgumentException ();

			var instance = new GenericInstanceMethod (self);
			foreach (var argument in arguments)
				instance.GenericArguments.Add (argument);

			return instance;
		}

		public static MethodReference MakeGeneric (this IMethodReference self, params ITypeReference [] arguments)
		{
			var reference = new MethodReference {
				Name = self.Name,
				DeclaringType = self.DeclaringType.MakeGenericType (arguments),
				HasThis = self.HasThis,
				ExplicitThis = self.ExplicitThis,
				ReturnType = self.ReturnType,
				CallingConvention = self.CallingConvention,
			};

			foreach (var parameter in self.Parameters)
				reference.Parameters.Add (new ParameterDefinition (parameter.ParameterType));

			foreach (var generic_parameter in self.GenericParameters)
				reference.GenericParameters.Add (new GenericParameter (generic_parameter.Name, reference));

			return reference;
		}

		public static FieldReference MakeGeneric (this FieldReference self, params ITypeReference [] arguments)
		{
			return new FieldReference {
				Name = self.Name,
				DeclaringType = self.DeclaringType.MakeGenericType (arguments),
				FieldType = self.FieldType,
			};
		}
	}
}
