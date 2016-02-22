using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil.Rocks;

using NUnit.Framework;

namespace Mono.Cecil.Tests {

	[TestFixture]
	public class TypeDefinitionRocksTests {

        interface IFoo { }
        interface IBar : IFoo { }
        interface IFooBar : IBar { }
        class FooBar : IFooBar { }

        [Test]
        public void Assignable()
        {
            var foobar = typeof(FooBar).ToDefinition();
            var ifoobar = typeof(IFooBar).ToDefinition();

            Assert.Throws<ArgumentNullException>(() => foobar.IsAssignableFrom(null));
            Assert.Throws<ArgumentNullException>(() => foobar.IsSubclassOf(null));

            Assert.IsTrue(foobar.IsAssignableFrom(foobar));
            Assert.IsFalse(foobar.IsSubclassOf(foobar));
            Assert.Throws<NullReferenceException>(() => ((TypeDefinition)null).IsAssignableFrom(ifoobar));
            Assert.Throws<NullReferenceException>(() => ((TypeDefinition)null).IsSubclassOf(ifoobar));

            Assert.IsTrue(ifoobar.IsAssignableFrom(foobar));
            Assert.IsTrue(foobar.IsSubclassOf(ifoobar));
            Assert.Throws<NullReferenceException>(() => ((TypeDefinition)null).IsAssignableFrom(ifoobar));
            Assert.Throws<NullReferenceException>(() => ((TypeDefinition)null).IsSubclassOf(ifoobar));

            Assert.Throws<NullReferenceException>(() => ((TypeDefinition)null).IsAssignableFrom(null));
            Assert.Throws<NullReferenceException>(() => ((TypeDefinition)null).IsSubclassOf(null));
        }

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
			var ctors = foo.GetConstructors ().Select (ctor => ctor.FullName);

			var expected = new [] {
				"System.Void Mono.Cecil.Tests.TypeDefinitionRocksTests/Foo::.cctor()",
				"System.Void Mono.Cecil.Tests.TypeDefinitionRocksTests/Foo::.ctor(System.Int32)",
				"System.Void Mono.Cecil.Tests.TypeDefinitionRocksTests/Foo::.ctor(System.Int32,System.String)",
			};

			AssertSet (expected, ctors);
		}

		static void AssertSet<T> (IEnumerable<T> expected, IEnumerable<T> actual)
		{
			Assert.IsFalse (expected.Except (actual).Any ());
			Assert.IsTrue (expected.Intersect (actual).SequenceEqual (expected));
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

        public class Public { }
        protected class Family { }
        protected internal class FamilyOrAssembly { }
        internal class AssemblyOnly { }
        private class Private { }

        [Test]
        public void IsEventuallyAccessible()
        {
            Assert.IsTrue(typeof(Public).ToDefinition().IsEventuallyAccessible());
            Assert.IsTrue(typeof(TypeDefinitionRocksTests).ToDefinition().IsEventuallyAccessible());
            Assert.IsTrue(typeof(Family).ToDefinition().IsEventuallyAccessible());
            Assert.IsTrue(typeof(FamilyOrAssembly).ToDefinition().IsEventuallyAccessible());

            Assert.IsFalse(typeof(AssemblyOnly).ToDefinition().IsEventuallyAccessible());
            Assert.IsFalse(typeof(Private).ToDefinition().IsEventuallyAccessible());
            Assert.IsFalse(typeof(NotPublic).ToDefinition().IsEventuallyAccessible());
        }
    }

    // for the purpose of testing, these classes can't be defined inside another type
    public class Public { }
    internal class NotPublic { }
}