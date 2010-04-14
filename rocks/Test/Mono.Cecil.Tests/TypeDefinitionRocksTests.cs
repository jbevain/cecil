using System;
using System.Linq;
using Mono.Cecil.Rocks;

using NUnit.Framework;

namespace Mono.Cecil.Tests {

	[TestFixture]
	public class TypeDefinitionRocksTests {

		class Foo {

			static Foo ()
			{
			}

			public Foo (int a)
			{
			}

			public Foo (int a, string s)
			{
			}

			public static void Bar ()
			{
			}

			void Baz ()
			{
			}
		}

		[Test]
		public void GetConstructors ()
		{
			var foo = typeof (Foo).ToDefinition ();
			var ctors = foo.GetConstructors ().ToArray ();

			Assert.AreEqual (3, ctors.Length);
			Assert.AreEqual ("System.Void Mono.Cecil.Tests.TypeDefinitionRocksTests/Foo::.cctor()", ctors [0].FullName);
			Assert.AreEqual ("System.Void Mono.Cecil.Tests.TypeDefinitionRocksTests/Foo::.ctor(System.Int32)", ctors [1].FullName);
			Assert.AreEqual ("System.Void Mono.Cecil.Tests.TypeDefinitionRocksTests/Foo::.ctor(System.Int32,System.String)", ctors [2].FullName);
		}

		[Test]
		public void GetStaticConstructor ()
		{
			var foo = typeof (Foo).ToDefinition ();
			var cctor = foo.GetStaticConstructor ();

			Assert.IsNotNull (cctor);
			Assert.AreEqual ("System.Void Mono.Cecil.Tests.TypeDefinitionRocksTests/Foo::.cctor()", cctor.FullName);
		}

		[Test]
		public void GetMethods ()
		{
			var foo = typeof (Foo).ToDefinition ();
			var methods = foo.GetMethods ().ToArray ();

			Assert.AreEqual (2, methods.Length);
			Assert.AreEqual ("System.Void Mono.Cecil.Tests.TypeDefinitionRocksTests/Foo::Bar()", methods [0].FullName);
			Assert.AreEqual ("System.Void Mono.Cecil.Tests.TypeDefinitionRocksTests/Foo::Baz()", methods [1].FullName);
		}

		enum Pan : byte {
			Pin,
			Pon,
		}

		[Test]
		public void GetEnumUnderlyingType ()
		{
			var pan = typeof (Pan).ToDefinition ();

			Assert.IsNotNull (pan);
			Assert.IsTrue (pan.IsEnum);

			var underlying_type = pan.GetEnumUnderlyingType ();
			Assert.IsNotNull (underlying_type);

			Assert.AreEqual ("System.Byte", underlying_type.FullName);
		}
	}
}