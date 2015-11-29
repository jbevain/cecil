using System;

using Mono.Cecil;

using NUnit.Framework;

namespace Mono.Cecil.Tests {

	[TestFixture]
	public class NestedTypesTests : BaseTestFixture {

		[Test]
		public void NestedTypes ()
		{
			TestCSharp ("NestedTypes.cs", module => {
				var foo = module.GetType ("Foo");

				Assert.AreEqual ("Foo", foo.Name);
				Assert.AreEqual ("Foo", foo.FullName);
				Assert.AreEqual (module, foo.Module);
				Assert.AreEqual (1, foo.NestedTypes.Count);

				var bar = foo.NestedTypes [0];

				Assert.AreEqual ("Bar", bar.Name);
				Assert.AreEqual ("Foo/Bar", bar.FullName);
				Assert.AreEqual (module, bar.Module);
				Assert.AreEqual (1, bar.NestedTypes.Count);

				var baz = bar.NestedTypes [0];

				Assert.AreEqual ("Baz", baz.Name);
				Assert.AreEqual ("Foo/Bar/Baz", baz.FullName);
				Assert.AreEqual (module, baz.Module);
			});
		}

		[Test]
		public void DirectNestedType ()
		{
			TestCSharp ("NestedTypes.cs", module => {
				var bingo = module.GetType ("Bingo");
				var get_fuel = bingo.GetMethod ("GetFuel");

				Assert.AreEqual ("Bingo/Fuel", get_fuel.ReturnType.FullName);
			});
		}

		[Test]
		public void NestedTypeWithOwnNamespace()
		{
			TestModule("bug-185.dll", module => {
				var foo = module.GetType("Foo");
				var foo_child = foo.NestedTypes[0];

				Assert.AreEqual("<IFoo<System.Byte[]>", foo_child.Namespace);
				Assert.AreEqual("Do>d__0", foo_child.Name);

				Assert.AreEqual("Foo/<IFoo<System.Byte[]>.Do>d__0", foo_child.FullName);
			});
		}

		[Test]
		public void BugFix248Test()
		{
			var foo = new TypeDefinition(null, "foo", TypeAttributes.Class);
			var bar = new TypeDefinition(null, "bar", TypeAttributes.Class);
			var baz = new TypeDefinition(null, "baz", TypeAttributes.Class);

			foo.NestedTypes.Add(bar);
			bar.NestedTypes.Add(baz);

			Assert.AreEqual ("foo/bar/baz", baz.FullName);

			foo.Namespace = "fns";

			Assert.AreEqual("fns.foo/bar", bar.FullName);
			Assert.AreEqual("fns.foo/bar/baz", baz.FullName);

			bar.Namespace = "bns";

			Assert.AreEqual("fns.foo/bns.bar", bar.FullName);
			Assert.AreEqual("fns.foo/bns.bar/baz", baz.FullName);
		}
	}
}
