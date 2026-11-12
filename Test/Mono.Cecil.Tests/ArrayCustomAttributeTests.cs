using System;
using System.Linq;
using Mono.Cecil.Metadata;
using Mono.Cecil.PE;
using NUnit.Framework;

namespace Mono.Cecil.Tests {

	[TestFixture]
	public class ArrayCustomAttributeTests {

		static MethodReference Constructor (ModuleDefinition module, TypeReference parameter)
		{
			var ctor = new MethodReference (".ctor", module.TypeSystem.Void, module.TypeSystem.Object) { HasThis = true };
			ctor.Parameters.Add (new ParameterDefinition (parameter));
			return ctor;
		}

		static SignatureWriter Writer (ModuleDefinition module)
		{
			return new SignatureWriter (new MetadataBuilder (module, string.Empty, 0, null));
		}

		static SignatureReader Reader (ModuleDefinition module, byte [] signature)
		{
			var blob = new byte [signature.Length + 1];
			blob [0] = (byte) signature.Length;
			Array.Copy (signature, 0, blob, 1, signature.Length);
			var image = new Image { BlobHeap = new BlobHeap (blob), TableHeap = new TableHeap (new byte [0]) };
			return new SignatureReader (0, new MetadataReader (image, module, null));
		}

		[TestCase (1, false)]
		[TestCase (2, false)]
		[TestCase (1, true)]
		[TestCase (2, true)]
		public void RejectNonVectorValues (int rank, bool nested)
		{
			using (var module = ModuleDefinition.CreateModule ("Arrays", ModuleKind.Dll)) {
				var array = new ArrayType (module.TypeSystem.Int32, rank, false);
				if (nested)
					array = new ArrayType (array);
				var ctor = Constructor (module, array);
				foreach (var values in new [] { null, new CustomAttributeArgument [0], new [] { new CustomAttributeArgument (module.TypeSystem.Int32, 1) } }) {
					var argument = new CustomAttributeArgument (array, values);
					var attribute = new CustomAttribute (ctor);
					attribute.ConstructorArguments.Add (argument);
					ctor.Parameters [0].ParameterType = array;
					Assert.Throws<ArgumentException> (() => Writer (module).WriteCustomAttributeConstructorArguments (attribute));
					ctor.Parameters [0].ParameterType = new ArrayType (module.TypeSystem.Int32);
					Assert.Throws<ArgumentException> (() => Writer (module).WriteCustomAttributeConstructorArguments (attribute));
					ctor.Parameters [0].ParameterType = module.TypeSystem.Object;
					attribute.ConstructorArguments [0] = new CustomAttributeArgument (module.TypeSystem.Object, argument);
					Assert.Throws<ArgumentException> (() => Writer (module).WriteCustomAttributeConstructorArguments (attribute));
					attribute.Properties.Add (new CustomAttributeNamedArgument ("Items", argument));
					Assert.Throws<ArgumentException> (() => Writer (module).WriteCustomAttributeNamedArguments (attribute));
					attribute.Properties.Clear ();
					attribute.Fields.Add (new CustomAttributeNamedArgument ("Items", argument));
					Assert.Throws<ArgumentException> (() => Writer (module).WriteCustomAttributeNamedArguments (attribute));
				}
				ctor.Parameters [0].ParameterType = array;
				foreach (var bytes in new [] { new byte [4], new byte [] { 0xff, 0xff, 0xff, 0xff } })
					Assert.Throws<BadImageFormatException> (() => Reader (module, bytes).ReadCustomAttributeConstructorArguments (new CustomAttribute (ctor), ctor.Parameters));
			}
		}

		[Test]
		public void RejectArrayTagInNamedArgument ()
		{
			using (var module = ModuleDefinition.CreateModule ("Arrays", ModuleKind.Dll)) {
				var attribute = new CustomAttribute (Constructor (module, module.TypeSystem.Object));
				var reader = Reader (module, new byte [] { 0x54, 0x14 });
				Assert.Throws<BadImageFormatException> (() => reader.ReadCustomAttributeNamedArguments (1, ref attribute.fields, ref attribute.properties));
			}
		}

		[TestCase (-1)]
		[TestCase (0)]
		[TestCase (2)]
		public void VectorValuesRoundTrip (int count)
		{
			using (var module = ModuleDefinition.CreateModule ("Arrays", ModuleKind.Dll)) {
				var vector = new ArrayType (module.TypeSystem.Int32);
				var values = count < 0 ? null : Enumerable.Repeat (new CustomAttributeArgument (module.TypeSystem.Int32, 42), count).ToArray ();
				var ctor = Constructor (module, vector);
				var attribute = new CustomAttribute (ctor);
				attribute.ConstructorArguments.Add (new CustomAttributeArgument (vector, values));
				attribute.Properties.Add (new CustomAttributeNamedArgument ("Items", new CustomAttributeArgument (vector, values)));
				var writer = Writer (module);
				writer.WriteCustomAttributeConstructorArguments (attribute);
				writer.WriteCustomAttributeNamedArguments (attribute);
				var reader = Reader (module, writer.buffer.Take (writer.length).ToArray ());
				var decoded = new CustomAttribute (ctor);
				reader.ReadCustomAttributeConstructorArguments (decoded, ctor.Parameters);
				reader.ReadCustomAttributeNamedArguments (reader.ReadUInt16 (), ref decoded.fields, ref decoded.properties);
				foreach (var argument in new [] { decoded.ConstructorArguments [0], decoded.Properties [0].Argument }) {
					Assert.IsTrue (((ArrayType) argument.Type).IsVector);
					if (values == null)
						Assert.IsNull (argument.Value);
					else
						CollectionAssert.AreEqual (values, (CustomAttributeArgument []) argument.Value);
				}
			}
		}

		[TestCase ("[]")]
		[TestCase ("[*]")]
		[TestCase ("[,]")]
		public void TypeValuedArgumentsPreserveArrayKind (string suffix)
		{
			using (var module = ModuleDefinition.CreateModule ("Arrays", ModuleKind.Dll)) {
				var system_type = module.ImportReference (typeof (Type));
				var ctor = Constructor (module, system_type);
				var attribute = new CustomAttribute (ctor);
				var name = "System.Int32" + suffix;
				attribute.ConstructorArguments.Add (new CustomAttributeArgument (system_type, TypeParser.ParseType (module, name)));
				var writer = Writer (module);
				writer.WriteCustomAttributeConstructorArguments (attribute);
				var reader = Reader (module, writer.buffer.Take (writer.length).ToArray ());
				var decoded = new CustomAttribute (ctor);
				reader.ReadCustomAttributeConstructorArguments (decoded, ctor.Parameters);
				Assert.AreEqual (name, ((TypeReference) decoded.ConstructorArguments [0].Value).FullName);
			}
		}
	}
}
