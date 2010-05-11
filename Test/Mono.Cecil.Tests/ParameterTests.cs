using System;
using System.Linq;

using Mono.Cecil;
using Mono.Cecil.Metadata;

using NUnit.Framework;

namespace Mono.Cecil.Tests {

	[TestFixture]
	public class ParameterTests : BaseTestFixture {

		[TestCSharp ("Methods.cs")]
		public void MarshalAsI4 (ModuleDefinition module)
		{
			var bar = module.GetType ("Bar");
			var pan = bar.GetMethod ("Pan");

			Assert.AreEqual (1, pan.Parameters.Count);

			var parameter = pan.Parameters [0];

			Assert.IsTrue (parameter.HasMarshalInfo);
			var info = parameter.MarshalInfo;

			Assert.AreEqual (typeof (MarshalInfo), info.GetType ());
			Assert.AreEqual (NativeType.I4, info.NativeType);
		}

		[TestCSharp ("Methods.cs")]
		public void CustomMarshaler (ModuleDefinition module)
		{
			var bar = module.GetType ("Bar");
			var pan = bar.GetMethod ("PanPan");

			var parameter = pan.Parameters [0];

			Assert.IsTrue (parameter.HasMarshalInfo);

			var info = (CustomMarshalInfo) parameter.MarshalInfo;

			Assert.AreEqual (Guid.Empty, info.Guid);
			Assert.AreEqual (string.Empty, info.UnmanagedType);
			Assert.AreEqual (NativeType.CustomMarshaler, info.NativeType);
			Assert.AreEqual ("nomnom", info.Cookie);

			Assert.AreEqual ("Foo", info.ManagedType.FullName);
			Assert.AreEqual (module, info.ManagedType.Scope);
		}

		[TestCSharp ("Methods.cs")]
		public void SafeArrayMarshaler (ModuleDefinition module)
		{
			var bar = module.GetType ("Bar");
			var pan = bar.GetMethod ("PanPan");

			Assert.IsTrue (pan.MethodReturnType.HasMarshalInfo);

			var info = (SafeArrayMarshalInfo) pan.MethodReturnType.MarshalInfo;

			Assert.AreEqual (VariantType.Dispatch, info.ElementType);
		}

		[TestCSharp ("Methods.cs")]
		public void ArrayMarshaler (ModuleDefinition module)
		{
			var bar = module.GetType ("Bar");
			var pan = bar.GetMethod ("PanPan");

			var parameter = pan.Parameters [1];

			Assert.IsTrue (parameter.HasMarshalInfo);

			var info = (ArrayMarshalInfo) parameter.MarshalInfo;

			Assert.AreEqual (NativeType.I8, info.ElementType);
			Assert.AreEqual (66, info.Size);
			Assert.AreEqual (2, info.SizeParameterIndex);

			parameter = pan.Parameters [3];

			Assert.IsTrue (parameter.HasMarshalInfo);

			info = (ArrayMarshalInfo) parameter.MarshalInfo;

			Assert.AreEqual (NativeType.I2, info.ElementType);
			Assert.AreEqual (-1, info.Size);
			Assert.AreEqual (-1, info.SizeParameterIndex);
		}

		[TestModule ("boxedoptarg.dll")]
		public void BoxedDefaultArgumentValue (ModuleDefinition module)
		{
			var foo = module.GetType ("Foo");
			var bar = foo.GetMethod ("Bar");
			var baz = bar.Parameters [0];

			Assert.IsTrue (baz.HasConstant);
			Assert.AreEqual (-1, baz.Constant);
		}

		[Test]
		public void AddParameterIndex ()
		{
			var object_ref = new TypeReference ("System", "Object", null, false);
			var method = new MethodDefinition ("foo", MethodAttributes.Static, object_ref);

			var x = new ParameterDefinition ("x", ParameterAttributes.None, object_ref);
			var y = new ParameterDefinition ("y", ParameterAttributes.None, object_ref);

			method.Parameters.Add (x);
			method.Parameters.Add (y);

			Assert.AreEqual (0, x.Index);
			Assert.AreEqual (1, y.Index);

			Assert.AreEqual (method, x.Method);
			Assert.AreEqual (method, y.Method);
		}

		[Test]
		public void RemoveAtParameterIndex ()
		{
			var object_ref = new TypeReference ("System", "Object", null, false);
			var method = new MethodDefinition ("foo", MethodAttributes.Static, object_ref);

			var x = new ParameterDefinition ("x", ParameterAttributes.None, object_ref);
			var y = new ParameterDefinition ("y", ParameterAttributes.None, object_ref);
			var z = new ParameterDefinition ("y", ParameterAttributes.None, object_ref);

			method.Parameters.Add (x);
			method.Parameters.Add (y);
			method.Parameters.Add (z);

			Assert.AreEqual (0, x.Index);
			Assert.AreEqual (1, y.Index);
			Assert.AreEqual (2, z.Index);

			method.Parameters.RemoveAt (1);

			Assert.AreEqual (0, x.Index);
			Assert.AreEqual (-1, y.Index);
			Assert.AreEqual (1, z.Index);
		}

		[Test]
		public void RemoveParameterIndex ()
		{
			var object_ref = new TypeReference ("System", "Object", null, false);
			var method = new MethodDefinition ("foo", MethodAttributes.Static, object_ref);

			var x = new ParameterDefinition ("x", ParameterAttributes.None, object_ref);
			var y = new ParameterDefinition ("y", ParameterAttributes.None, object_ref);
			var z = new ParameterDefinition ("y", ParameterAttributes.None, object_ref);

			method.Parameters.Add (x);
			method.Parameters.Add (y);
			method.Parameters.Add (z);

			Assert.AreEqual (0, x.Index);
			Assert.AreEqual (1, y.Index);
			Assert.AreEqual (2, z.Index);

			method.Parameters.Remove (y);

			Assert.AreEqual (0, x.Index);
			Assert.AreEqual (-1, y.Index);
			Assert.AreEqual (1, z.Index);
		}

		[Test]
		public void InsertParameterIndex ()
		{
			var object_ref = new TypeReference ("System", "Object", null, false);
			var method = new MethodDefinition ("foo", MethodAttributes.Static, object_ref);

			var x = new ParameterDefinition ("x", ParameterAttributes.None, object_ref);
			var y = new ParameterDefinition ("y", ParameterAttributes.None, object_ref);
			var z = new ParameterDefinition ("y", ParameterAttributes.None, object_ref);

			method.Parameters.Add (x);
			method.Parameters.Add (z);

			Assert.AreEqual (0, x.Index);
			Assert.AreEqual (-1, y.Index);
			Assert.AreEqual (1, z.Index);

			method.Parameters.Insert (1, y);

			Assert.AreEqual (0, x.Index);
			Assert.AreEqual (1, y.Index);
			Assert.AreEqual (2, z.Index);
		}
	}
}
