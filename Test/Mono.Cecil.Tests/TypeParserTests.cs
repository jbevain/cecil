using System;
using System.Linq;

using Mono.Cecil;
using Mono.Cecil.Cil;

using NUnit.Framework;

namespace Mono.Cecil.Tests {

	[TestFixture]
	public class TypeParserTests : BaseTestFixture {

		[Test]
		public void SimpleTypeReference ()
		{
			var module = GetCurrentModule ();
			var corlib = module.TypeSystem.Corlib;

			const string fullname = "System.String";

			var type = TypeParser.ParseType (module, fullname);
			Assert.IsNotNull (type);
			Assert.AreEqual (corlib, type.Scope);
			Assert.AreEqual (module, type.Module);
			Assert.AreEqual ("System", type.Namespace);
			Assert.AreEqual ("String", type.Name);
			Assert.IsInstanceOfType (typeof (TypeReference), type);
		}

		[Test]
		public void ByRefTypeReference ()
		{
			var module = GetCurrentModule ();
			var corlib = module.TypeSystem.Corlib;

			const string fullname = "System.String&";

			var type = TypeParser.ParseType (module, fullname);

			Assert.IsInstanceOfType (typeof (ByReferenceType), type);

			type = ((ByReferenceType) type).ElementType;

			Assert.IsNotNull (type);
			Assert.AreEqual (corlib, type.Scope);
			Assert.AreEqual (module, type.Module);
			Assert.AreEqual ("System", type.Namespace);
			Assert.AreEqual ("String", type.Name);
			Assert.IsInstanceOfType (typeof (TypeReference), type);
		}

		static ModuleDefinition GetCurrentModule ()
		{
			return ModuleDefinition.ReadModule (typeof (TypeParserTests).Module.FullyQualifiedName);
		}
	}
}
