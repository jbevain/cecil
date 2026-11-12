using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Mono.Cecil.Cil;
using Mono.Cecil.Metadata;
using Mono.Cecil.PE;
using NUnit.Framework;

namespace Mono.Cecil.Tests {

	[TestFixture]
	public class ArrayTypeTests {

		[Test]
		public void ExplicitArrayKindAndLegacyConstructors ()
		{
			Assert.IsNotNull (typeof (ArrayType).GetConstructor (new [] { typeof (TypeReference), typeof (int) }),
				"Keep the two-argument constructor for existing binaries");
			using (var module = ModuleDefinition.CreateModule ("Arrays", ModuleKind.Dll)) {
				Assert.IsTrue (new ArrayType (module.TypeSystem.Int32).IsVector);
				Assert.IsTrue (new ArrayType (module.TypeSystem.Int32, 1).IsVector);
				Assert.IsFalse (new ArrayType (module.TypeSystem.Int32, 2).IsVector);
				var array = new ArrayType (module.TypeSystem.Int32, isVector: false);
				Assert.AreEqual ("Int32[*]", array.Name);
				Assert.AreEqual ("System.Int32[*]", array.FullName);
				Assert.AreEqual (1, array.Rank);
				Assert.IsFalse (array.IsVector);
				Assert.AreEqual (MetadataType.Array, array.MetadataType);
				Assert.IsFalse (array.Dimensions [0].IsSized);
				array.Dimensions [0] = new ArrayDimension (0, null);
				Assert.IsFalse (array.IsVector);
				array.Dimensions [0] = new ArrayDimension ();
				Assert.IsFalse (array.IsVector, "Removing bounds must not turn ARRAY into SZARRAY");

				var vector = new ArrayType (module.TypeSystem.Int32);
				Assert.IsNull (vector.Dimensions);
				Assert.IsNull (vector.Dimensions, "Repeated access must remain safe");
				Assert.IsTrue (vector.IsVector);
				Assert.AreEqual (MetadataType.SzArray, vector.MetadataType);
				Assert.AreEqual (1, vector.Rank);

				var matrix = new ArrayType (module.TypeSystem.Int32, 2);
				Assert.AreEqual (2, matrix.Rank);
				Assert.AreEqual ("System.Int32[,]", matrix.FullName);
				Assert.AreEqual (MetadataType.Array, matrix.MetadataType);
				matrix.Dimensions.RemoveAt (1);
				Assert.IsFalse (matrix.IsVector, "Reducing ARRAY to rank one must preserve its kind");
				Assert.AreEqual ("System.Int32[*]", matrix.FullName);
			}
		}

		[TestCase (1, true)]
		[TestCase (1, false)]
		[TestCase (2, false)]
		public void ExplicitRankAndKind (int rank, bool vector)
		{
			using (var module = ModuleDefinition.CreateModule ("Arrays", ModuleKind.Dll)) {
				var array = new ArrayType (module.TypeSystem.Int32, rank, vector);
				Assert.AreEqual (rank, array.Rank);
				Assert.AreEqual (vector, array.IsVector);
				Assert.AreEqual (vector ? MetadataType.SzArray : MetadataType.Array, array.MetadataType);
				if (vector)
					Assert.IsNull (array.Dimensions);
				else
					Assert.AreEqual (rank, array.Dimensions.Count);
			}
		}

		[TestCase (2, true)]
		[TestCase (0, true)]
		[TestCase (0, false)]
		[TestCase (-1, false)]
		[TestCase (0x20000000, false)]
		public void RejectInvalidRankAndKind (int rank, bool vector)
		{
			using (var module = ModuleDefinition.CreateModule ("Arrays", ModuleKind.Dll))
				Assert.Throws<ArgumentOutOfRangeException> (() => new ArrayType (module.TypeSystem.Int32, rank, vector));
		}

		[TestCase ("[]")]
		[TestCase ("[*]")]
		[TestCase ("[,]")]
		[TestCase ("[*][]")]
		[TestCase ("[][*]")]
		[TestCase ("[*]*")]
		[TestCase ("[*]&")]
		public void ParseArrayTypeNames (string suffix)
		{
			using (var module = ModuleDefinition.CreateModule ("Arrays", ModuleKind.Dll)) {
				var name = "Example.Element" + suffix;
				var type = TypeParser.ParseType (module, name + ", External");
				Assert.AreEqual (name, type.FullName);
				Assert.AreEqual ("External", type.Scope.Name, "The closing bracket must be consumed before the assembly name");
				var text = TypeParser.ToParseable (type);
				Assert.IsTrue (text.StartsWith (name + ", External", StringComparison.Ordinal));
				Assert.AreEqual (name, TypeParser.ParseType (module, text).FullName);
			}
		}

		[TestCase ("[]", new byte [] { 0x1d, 0x08 })]
		[TestCase ("[*]", new byte [] { 0x14, 0x08, 1, 0, 0 })]
		[TestCase ("[,]", new byte [] { 0x14, 0x08, 2, 0, 0 })]
		[TestCase ("[,,]", new byte [] { 0x14, 0x08, 3, 0, 0 })]
		[TestCase ("[][]", new byte [] { 0x1d, 0x1d, 0x08 })]
		[TestCase ("[*][]", new byte [] { 0x1d, 0x14, 0x08, 1, 0, 0 })]
		[TestCase ("[][*]", new byte [] { 0x14, 0x1d, 0x08, 1, 0, 0 })]
		[TestCase ("[*][*]", new byte [] { 0x14, 0x14, 0x08, 1, 0, 0, 1, 0, 0 })]
		[TestCase ("[*]*", new byte [] { 0x0f, 0x14, 0x08, 1, 0, 0 })]
		[TestCase ("[*]&", new byte [] { 0x10, 0x14, 0x08, 1, 0, 0 })]
		public void ParseArrayKindAndSignature (string suffix, byte [] signature)
		{
			using (var module = ModuleDefinition.CreateModule ("Arrays", ModuleKind.Dll)) {
				var name = "System.Int32" + suffix;
				foreach (var input in new [] { name, name + ", " + module.TypeSystem.CoreLibrary }) {
					var type = TypeParser.ParseType (module, input);
					var reparsed = TypeParser.ParseType (module, TypeParser.ToParseable (type));
					foreach (var actual in new [] { type, reparsed }) {
						Assert.AreEqual (name, actual.FullName);
						CollectionAssert.AreEqual (signature, WriteSignature (module, actual));
						var expected = ReadSignature (module, signature).ReadTypeSignature ();
						var current = actual;
						while (expected is TypeSpecification) {
							Assert.AreEqual (expected.MetadataType, current.MetadataType);
							if (expected is ArrayType) {
								var expected_array = (ArrayType) expected;
								var array = (ArrayType) current;
								Assert.AreEqual (expected_array.IsVector, array.IsVector);
								Assert.AreEqual (expected_array.Rank, array.Rank);
								if (expected_array.IsVector)
									Assert.IsNull (array.Dimensions);
								else
									Assert.AreEqual (expected_array.Rank, array.Dimensions.Count);
							}
							expected = ((TypeSpecification) expected).ElementType;
							current = ((TypeSpecification) current).ElementType;
						}
					}
				}
			}
		}

		[TestCase ("[]", true)]
		[TestCase ("[*]", false)]
		public void ParseGenericArgumentWithRankOneArray (string suffix, bool vector)
		{
			using (var module = ModuleDefinition.CreateModule ("Arrays", ModuleKind.Dll)) {
				var type = (GenericInstanceType) TypeParser.ParseType (module,
					"Example.Generic`1[[Example.Element" + suffix + ", ElementAssembly]], GenericAssembly");
				Assert.AreEqual ("ElementAssembly", type.GenericArguments [0].Scope.Name);
				Assert.AreEqual ("GenericAssembly", type.Scope.Name);
				Assert.AreEqual (vector, ((ArrayType) type.GenericArguments [0]).IsVector);
				Assert.AreEqual (vector ? MetadataType.SzArray : MetadataType.Array, type.GenericArguments [0].MetadataType);
				var parsed = (GenericInstanceType) TypeParser.ParseType (module, TypeParser.ToParseable (type));
				Assert.AreEqual (type.FullName, parsed.FullName);
				Assert.AreEqual (vector, ((ArrayType) parsed.GenericArguments [0]).IsVector);
				Assert.AreEqual (type.GenericArguments [0].MetadataType, parsed.GenericArguments [0].MetadataType);
			}
		}

		static IEnumerable<TestCaseData> ArraySignatures ()
		{
			// ECMA-335: SZARRAY=0x1d, ARRAY=0x14, I4=0x08.
			yield return new TestCaseData (new byte [] { 0x1d, 0x08 }, "System.Int32[]", true);
			yield return new TestCaseData (new byte [] { 0x14, 0x08, 1, 0, 0 }, "System.Int32[*]", false);
			yield return new TestCaseData (new byte [] { 0x14, 0x08, 1, 0, 1, 0 }, "System.Int32[0...]", false);
			yield return new TestCaseData (new byte [] { 0x14, 0x08, 1, 1, 5, 1, 0x7d }, "System.Int32[-2...2]", false);
			yield return new TestCaseData (new byte [] { 0x14, 0x08, 1, 1, 5, 0 }, "System.Int32[...4]", false);
			yield return new TestCaseData (new byte [] { 0x14, 0x08, 1, 1, 0, 0 }, "System.Int32[...-1]", false);
			yield return new TestCaseData (new byte [] { 0x14, 0x08, 2, 2, 3, 4, 1, 0x7d }, "System.Int32[-2...0,...3]", false);
			yield return new TestCaseData (new byte [] { 0x14, 0x08, 2, 1, 5, 2, 0x7d, 2 }, "System.Int32[-2...2,1...]", false);
			yield return new TestCaseData (new byte [] { 0x14, 0x08, 1, 1, 0xdf, 0xff, 0xff, 0xff, 0 }, "System.Int32[...536870910]", false);
			yield return new TestCaseData (new byte [] { 0x14, 0x08, 1, 0, 1, 0xc0, 0, 0, 1 }, "System.Int32[-268435456...]", false);
			yield return new TestCaseData (new byte [] { 0x14, 0x08, 1, 0, 1, 0xdf, 0xff, 0xff, 0xfe }, "System.Int32[268435455...]", false);
			yield return new TestCaseData (new byte [] { 0x14, 0x08, 2, 0, 0 }, "System.Int32[,]", false);
			yield return new TestCaseData (new byte [] { 0x1d, 0x14, 0x08, 1, 0, 0 }, "System.Int32[*][]", true);
			yield return new TestCaseData (new byte [] { 0x14, 0x1d, 0x08, 1, 0, 0 }, "System.Int32[][*]", false);
		}

		[TestCaseSource (nameof (ArraySignatures))]
		public void ReadWriteAndImportArraySignatures (byte [] signature, string name, bool vector)
		{
			using (var module = ModuleDefinition.CreateModule ("Arrays", ModuleKind.Dll))
			using (var target = ModuleDefinition.CreateModule ("Target", ModuleKind.Dll)) {
				// Start from independently specified bytes so reader and writer cannot
				// hide the same mistake in each other.
				var array = ReadArraySignature (module, signature);
				Assert.AreEqual (name, array.FullName);
				Assert.AreEqual (vector, array.IsVector);
				Assert.AreEqual (vector ? MetadataType.SzArray : MetadataType.Array, array.MetadataType);
				CollectionAssert.AreEqual (signature, WriteSignature (module, array));

				var imported = (ArrayType) target.ImportReference (array);
				Assert.AreEqual (target, imported.Module);
				Assert.AreEqual (name, imported.FullName);
				Assert.AreEqual (vector, imported.IsVector);
				CollectionAssert.AreEqual (signature, WriteSignature (target, imported));

				var type = new TypeDefinition ("", "Holder", TypeAttributes.Public, target.TypeSystem.Object);
				target.Types.Add (type);
				type.Fields.Add (new FieldDefinition ("Value", FieldAttributes.Public, imported));
				using (var stream = new MemoryStream ()) {
					target.Write (stream);
					stream.Position = 0;
					using (var roundtrip = ModuleDefinition.ReadModule (stream)) {
						var field_type = (ArrayType) roundtrip.GetType ("Holder").Fields [0].FieldType;
						Assert.AreEqual (name, field_type.FullName);
						Assert.AreEqual (vector, field_type.IsVector);
						CollectionAssert.AreEqual (signature, WriteSignature (roundtrip, field_type));
					}
				}
			}
		}

		[TestCase (new byte [] { 0x14, 0x08, 0, 0, 0 })]
		[TestCase (new byte [] { 0x14, 0x08, 1, 2, 1, 1, 0 })]
		[TestCase (new byte [] { 0x14, 0x08, 1, 0, 2, 0, 0 })]
		[TestCase (new byte [] { 0x14, 0x08, 1, 1 })]
		[TestCase (new byte [] { 0x14, 0x08, 1, 1, 0x80 })]
		[TestCase (new byte [] { 0x14, 0x08, 1, 0, 1, 0xc0, 0 })]
		[TestCase (new byte [] { 0x14, 0x08, 0xe0, 0, 0, 1, 0, 0 })]
		public void RejectInvalidArrayShape (byte [] signature)
		{
			using (var module = ModuleDefinition.CreateModule ("Arrays", ModuleKind.Dll))
				Assert.Throws<BadImageFormatException> (() => ReadArraySignature (module, signature));
		}

		[Test]
		public void RejectUnencodableArrayDimensions ()
		{
			using (var module = ModuleDefinition.CreateModule ("Arrays", ModuleKind.Dll)) {
				var array = new ArrayType (module.TypeSystem.Int32, 2);
				array.Dimensions [1] = new ArrayDimension (null, 4);
				Assert.Throws<ArgumentException> (() => WriteSignature (module, array), "Sizes cannot skip a dimension");
				array.Dimensions [1] = new ArrayDimension (1, null);
				Assert.Throws<ArgumentException> (() => WriteSignature (module, array), "Lower bounds cannot skip a dimension");
				array.Dimensions.Clear ();
				Assert.Throws<ArgumentException> (() => WriteSignature (module, array), "Rank zero is invalid");
			}
		}

		[TestCase (0, -2)]
		[TestCase (0, 536870911)]
		[TestCase (-268435457, -268435457)]
		[TestCase (268435456, 268435456)]
		[TestCase (int.MinValue, int.MaxValue)]
		public void RejectArrayBoundsOutsideEncodingRange (int lower, int upper)
		{
			using (var module = ModuleDefinition.CreateModule ("Arrays", ModuleKind.Dll)) {
				var array = new ArrayType (module.TypeSystem.Int32, false);
				array.Dimensions [0] = new ArrayDimension (lower, upper);
				Assert.Throws<ArgumentException> (() => WriteSignature (module, array));
			}
		}

		static ArrayType ReadArraySignature (ModuleDefinition module, byte [] signature)
		{
			return (ArrayType) ReadSignature (module, signature).ReadTypeSignature ();
		}

		static SignatureReader ReadSignature (ModuleDefinition module, byte [] signature)
		{
			var blob = new byte [signature.Length + 1];
			blob [0] = (byte) signature.Length;
			Array.Copy (signature, 0, blob, 1, signature.Length);
			var image = new Image { BlobHeap = new BlobHeap (blob), TableHeap = new TableHeap (new byte [0]) };
			return new SignatureReader (0, new MetadataReader (image, module, null));
		}

		static byte [] WriteSignature (ModuleDefinition module, TypeReference type)
		{
			var writer = new SignatureWriter (new MetadataBuilder (module, string.Empty, 0, null));
			writer.WriteTypeSignature (type);
			return writer.buffer.Take (writer.length).ToArray ();
		}

		[Test]
		public void ReflectionImportPreservesArrayKind ()
		{
			using (var module = ModuleDefinition.CreateModule ("Arrays", ModuleKind.Dll)) {
				var element = typeof (int);
				var types = new [] { element.MakeArrayType (), element.MakeArrayType (1), element.MakeArrayType (2),
					element.MakeArrayType (1).MakeArrayType (), element.MakeArrayType ().MakeArrayType (1) };
				foreach (var type in types) {
					var array = (ArrayType) module.ImportReference (type);
					Assert.AreEqual (type.FullName, array.FullName);
					Assert.AreEqual (type == type.GetElementType ().MakeArrayType (), array.IsVector);
				}
			}
		}

		[TestCase (true)]
		[TestCase (false)]
		public void GenericSubstitutionPreservesArrayShape (bool vector)
		{
			using (var module = ModuleDefinition.CreateModule ("Arrays", ModuleKind.Dll)) {
				var owner = new TypeDefinition ("", "Generic`1", TypeAttributes.Public, module.TypeSystem.Object);
				module.Types.Add (owner);
				var parameter = new GenericParameter ("T", owner);
				owner.GenericParameters.Add (parameter);
				var instance = new GenericInstanceType (owner);
				instance.GenericArguments.Add (module.TypeSystem.Int32);
				var array = new ArrayType (parameter, vector);
				AssertResolvedArray (new TypeResolver (instance).Resolve (array), vector, null, null);
				var field = new FieldReference ("Value", array, instance);
				AssertResolvedArray (GenericParameterResolver.ResolveFieldTypeIfNeeded (field), vector, null, null);
				if (!vector) {
					array.Dimensions [0] = new ArrayDimension (-2, 5);
					AssertResolvedArray (new TypeResolver (instance).Resolve (array), false, -2, 5);
					AssertResolvedArray (GenericParameterResolver.ResolveFieldTypeIfNeeded (field), false, -2, 5);
				}
			}
		}

		static void AssertResolvedArray (TypeReference type, bool vector, int? lower, int? upper)
		{
			var array = (ArrayType) type;
			Assert.AreEqual ("System.Int32", array.ElementType.FullName);
			Assert.AreEqual (vector, array.IsVector);
			if (vector) {
				Assert.IsNull (array.Dimensions);
				Assert.AreEqual (1, array.Rank);
			} else {
				Assert.AreEqual (lower, array.Dimensions [0].LowerBound);
				Assert.AreEqual (upper, array.Dimensions [0].UpperBound);
			}
		}

		[TestCase (true)]
		[TestCase (false)]
		public void ImportGenericArrayWithContext (bool vector)
		{
			using (var source = ModuleDefinition.CreateModule ("Source", ModuleKind.Dll))
			using (var target = ModuleDefinition.CreateModule ("Target", ModuleKind.Dll)) {
				var owner = new TypeDefinition ("", "Generic`1", TypeAttributes.Public, source.TypeSystem.Object);
				source.Types.Add (owner);
				owner.GenericParameters.Add (new GenericParameter ("T", owner));
				var target_owner = new TypeDefinition ("", "Generic`1", TypeAttributes.Public, target.TypeSystem.Object);
				target.Types.Add (target_owner);
				target_owner.GenericParameters.Add (new GenericParameter ("T", target_owner));
				var array = new ArrayType (owner.GenericParameters [0], vector);
				var imported = (ArrayType) target.ImportReference (array, target_owner);
				Assert.AreSame (target_owner.GenericParameters [0], imported.ElementType);
				Assert.AreEqual (vector, imported.IsVector);
				Assert.AreEqual (vector ? "T[]" : "T[*]", imported.FullName);
				if (!vector) {
					array.Dimensions [0] = new ArrayDimension (-2, 5);
					imported = (ArrayType) target.ImportReference (array, target_owner);
					array.Dimensions [0] = new ArrayDimension ();
					Assert.AreEqual (-2, imported.Dimensions [0].LowerBound);
					Assert.AreEqual (5, imported.Dimensions [0].UpperBound);
				}
			}
		}

		[TestCase (true)]
		[TestCase (false)]
		public void GenericMethodSubstitutionPreservesArrayKind (bool vector)
		{
			using (var module = ModuleDefinition.CreateModule ("Arrays", ModuleKind.Dll)) {
				var owner = new TypeDefinition ("", "Owner", TypeAttributes.Public, module.TypeSystem.Object);
				module.Types.Add (owner);
				var method = new MethodDefinition ("Generic", MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
				owner.Methods.Add (method);
				var parameter = new GenericParameter ("T", method);
				method.GenericParameters.Add (parameter);
				method.ReturnType = new ArrayType (parameter, vector);
				var instance = new GenericInstanceMethod (method);
				instance.GenericArguments.Add (module.TypeSystem.Int32);
				AssertResolvedArray (new TypeResolver (instance).Resolve (method.ReturnType), vector, null, null);
				AssertResolvedArray (GenericParameterResolver.ResolveReturnTypeIfNeeded (instance), vector, null, null);
			}
		}

		[TestCaseSource (nameof (ArraySignatures))]
		public void PortablePdbNullArrayConstant (byte [] signature, string name, bool vector)
		{
			using (var module = ModuleDefinition.CreateModule ("Arrays", ModuleKind.Dll))
			using (var pe = new MemoryStream ())
			using (var pdb = new MemoryStream ()) {
				var owner = new TypeDefinition ("", "Holder", TypeAttributes.Public, module.TypeSystem.Object);
				module.Types.Add (owner);
				var method = new MethodDefinition ("M", MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
				owner.Methods.Add (method);
				var ret = Instruction.Create (OpCodes.Ret);
				method.Body.Instructions.Add (ret);
				method.DebugInformation.Scope = new ScopeDebugInformation (ret, null);
				method.DebugInformation.Scope.Constants.Add (new ConstantDebugInformation (
					"items", ReadArraySignature (module, signature), null));
				module.Write (pe, new WriterParameters {
					WriteSymbols = true,
					SymbolWriterProvider = new PortablePdbWriterProvider (),
					SymbolStream = pdb,
				});
#if NET_CORE
				// System.Reflection.Metadata is unavailable on the net40 test target.
				// Inspect the actual PDB blob with an independent metadata reader.
				// The array signature must retain its kind and bounds; null is four zero bytes.
				pdb.Position = 0;
				using (var provider = System.Reflection.Metadata.MetadataReaderProvider.FromPortablePdbStream (
					pdb, System.Reflection.Metadata.MetadataStreamOptions.LeaveOpen)) {
					var metadata = provider.GetMetadataReader ();
					var record = metadata.GetLocalConstant (metadata.LocalConstants.Single ());
					CollectionAssert.AreEqual (signature.Concat (new byte [4]).ToArray (), metadata.GetBlobBytes (record.Signature));
				}
#endif
				pe.Position = 0;
				pdb.Position = 0;
				using (var roundtrip = ModuleDefinition.ReadModule (pe, new ReaderParameters {
					ReadSymbols = true,
					SymbolReaderProvider = new PortablePdbReaderProvider (),
					SymbolStream = pdb,
				})) {
					var constants = roundtrip.GetType ("Holder").Methods [0].DebugInformation.Scope.Constants;
					Assert.AreEqual (1, constants.Count);
					Assert.AreEqual ("items", constants [0].Name);
					Assert.IsNull (constants [0].Value);
					var array = (ArrayType) constants [0].ConstantType;
					Assert.AreEqual (vector, array.IsVector);
					Assert.AreEqual (name, array.FullName);
					Assert.AreEqual (vector ? MetadataType.SzArray : MetadataType.Array, array.MetadataType);
					CollectionAssert.AreEqual (signature, WriteSignature (roundtrip, array));
				}
			}
		}

		[Test]
		public void ComparisonsAndOverloadResolutionDistinguishArrayKind ()
		{
			using (var module = ModuleDefinition.CreateModule ("Arrays", ModuleKind.Dll)) {
				var element = module.TypeSystem.Int32;
				var vector = new ArrayType (element);
				var array = new ArrayType (element, false);
				foreach (TypeComparisonMode mode in Enum.GetValues (typeof (TypeComparisonMode))) {
					Assert.IsFalse (TypeReferenceEqualityComparer.AreEqual (vector, array, mode));
					Assert.IsFalse (TypeReferenceEqualityComparer.AreEqual (array, vector, mode));
					Assert.IsTrue (TypeReferenceEqualityComparer.AreEqual (array, new ArrayType (element, false), mode));
				}
				var comparer = new TypeReferenceEqualityComparer ();
				var set = new HashSet<TypeReference> (comparer) { vector, array, new ArrayType (element, false) };
				Assert.AreEqual (2, set.Count);
				Assert.AreEqual (comparer.GetHashCode (array), comparer.GetHashCode (new ArrayType (element, false)));

				var owner = new TypeDefinition ("", "Overloads", TypeAttributes.Public, module.TypeSystem.Object);
				module.Types.Add (owner);
				foreach (var type in new [] { vector, array }) {
					var method = new MethodDefinition ("Accept", MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
					method.Parameters.Add (new ParameterDefinition (type));
					method.Body.GetILProcessor ().Emit (OpCodes.Ret);
					owner.Methods.Add (method);
				}
				for (int i = 0; i < 2; i++) {
					var reference = new MethodReference ("Accept", module.TypeSystem.Void, owner);
					reference.Parameters.Add (new ParameterDefinition (new ArrayType (element, i == 0)));
					Assert.AreSame (owner.Methods [i], reference.Resolve ());
				}
				using (var stream = new MemoryStream ()) {
					module.Write (stream);
					stream.Position = 0;
					using (var roundtrip = ModuleDefinition.ReadModule (stream)) {
						owner = roundtrip.GetType ("Overloads");
						for (int i = 0; i < 2; i++) {
							var reference = new MethodReference ("Accept", roundtrip.TypeSystem.Void, owner);
							reference.Parameters.Add (new ParameterDefinition (new ArrayType (roundtrip.TypeSystem.Int32, i == 0)));
							Assert.AreSame (owner.Methods [i], reference.Resolve ());
						}
					}
				}
			}
		}
	}
}
