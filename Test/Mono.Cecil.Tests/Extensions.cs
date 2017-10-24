using System;
using System.IO;
using System.Linq;
using SR = System.Reflection;

namespace Mono.Cecil.Tests {

	public static class Extensions {

#if NET_CORE
		public static SR.Assembly LoadAssembly (MemoryStream stream)
		{
			return System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromStream (stream);
		}

		public static SR.TypeInfo GetTypeInfo (this Type self)
		{
			return SR.IntrospectionExtensions.GetTypeInfo(self);
		}

		public static SR.MethodInfo GetMethodInfo (this Delegate self)
		{
			return SR.RuntimeReflectionExtensions.GetMethodInfo (self);
		}

		public static string GetCultureName(this SR.AssemblyName self)
		{
			return self.CultureName;
		}
#else
		public static SR.Assembly LoadAssembly (MemoryStream stream)
		{
			return SR.Assembly.Load(stream.ToArray ());
		}

		public static Type GetTypeInfo (this Type self)
		{
			return self;
		}

		public static SR.MethodInfo GetMethodInfo (this Delegate self)
		{
			return self.Method;
		}

		public static Delegate CreateDelegate (this SR.MethodInfo self, Type type)
		{
			return Delegate.CreateDelegate (type, self);
		}

		public static string GetCultureName(this SR.AssemblyName self)
		{
			return self.CultureInfo.Name;
		}
#endif

		public static MethodDefinition GetMethod (this TypeDefinition self, string name)
		{
			return self.Methods.Where (m => m.Name == name).First ();
		}

		public static FieldDefinition GetField (this TypeDefinition self, string name)
		{
			return self.Fields.Where (f => f.Name == name).First ();
		}

		public static TypeDefinition ToDefinition (this Type self)
		{
			var module = ModuleDefinition.ReadModule (new MemoryStream (File.ReadAllBytes (self.GetTypeInfo().Module.FullyQualifiedName)));
			return (TypeDefinition) module.LookupToken (self.GetTypeInfo().MetadataToken);
		}

		public static MethodDefinition ToDefinition (this SR.MethodBase method)
		{
			var declaring_type = method.DeclaringType.ToDefinition ();
			return (MethodDefinition) declaring_type.Module.LookupToken (method.MetadataToken);
		}

		public static FieldDefinition ToDefinition (this SR.FieldInfo field)
		{
			var declaring_type = field.DeclaringType.ToDefinition ();
			return (FieldDefinition) declaring_type.Module.LookupToken (field.MetadataToken);
		}

		public static TypeReference MakeGenericType (this TypeReference self, params TypeReference [] arguments)
		{
			if (self.GenericParameters.Count != arguments.Length)
				throw new ArgumentException ();

			var instance = new GenericInstanceType (self);
			foreach (var argument in arguments)
				instance.GenericArguments.Add (argument);

			return instance;
		}

		public static MethodReference MakeGenericMethod (this MethodReference self, params TypeReference [] arguments)
		{
			if (self.GenericParameters.Count != arguments.Length)
				throw new ArgumentException ();

			var instance = new GenericInstanceMethod (self);
			foreach (var argument in arguments)
				instance.GenericArguments.Add (argument);

			return instance;
		}

		public static MethodReference MakeGeneric (this MethodReference self, params TypeReference [] arguments)
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

		public static FieldReference MakeGeneric (this FieldReference self, params TypeReference [] arguments)
		{
			return new FieldReference {
				Name = self.Name,
				DeclaringType = self.DeclaringType.MakeGenericType (arguments),
				FieldType = self.FieldType,
			};
		}
	}
}
