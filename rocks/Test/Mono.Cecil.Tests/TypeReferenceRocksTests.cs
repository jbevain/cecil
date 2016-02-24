using System;
using System.Linq;

using Mono.Cecil.Rocks;

using NUnit.Framework;

namespace Mono.Cecil.Tests {

	[TestFixture]
	public class TypeReferenceRocksTests {
        interface IFoo { }
        interface IBar : IFoo { }

        interface IFooBar : IBar { }
        class FooBar : IFooBar { }

        [Test]
        public void AreSame()
        {
            var ifoo = typeof(IFoo).ToDefinition();
            var ibar = typeof(IBar).ToDefinition();
            var ibarfoo = ibar.Interfaces.Single();

            // for reasons, TypeDefinitions got from ToDefinition() are not same as their "nature" equivalent
            // this makes testing code involves TypeDefinition relations hard
            Assert.AreNotEqual(typeof(object).ToDefinition(), typeof(object).ToDefinition());
            Assert.AreNotEqual(ifoo, ibarfoo);

            Assert.IsTrue(TypeReferenceRocks.AreSame(typeof(object).ToDefinition(), typeof(object).ToDefinition()));
            Assert.IsTrue(TypeReferenceRocks.AreSame(ibarfoo, ifoo));
            Assert.IsFalse(TypeReferenceRocks.AreSame(ifoo, null));
            Assert.IsFalse(TypeReferenceRocks.AreSame(null, ibar));

            Assert.IsTrue(typeof(object).ToDefinition().IsSameAs(typeof(object).ToDefinition()));
            Assert.IsTrue(ibarfoo.IsSameAs(ifoo));
            Assert.Throws<ArgumentNullException>(() => ifoo.IsSameAs(null));
            Assert.Throws<NullReferenceException>(() => ((TypeReference)null).IsSameAs(ibar));
            Assert.Throws<NullReferenceException>(() => ((TypeReference)null).IsSameAs(null));
        }

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

        [Test]
		public void MakeArrayType ()
		{
			var @string = GetTypeReference (typeof (string));

			var string_array = @string.MakeArrayType ();

			Assert.IsInstanceOf (typeof (ArrayType), string_array);
			Assert.AreEqual (1, string_array.Rank);
		}

		[Test]
		public void MakeArrayTypeRank ()
		{
			var @string = GetTypeReference (typeof (string));

			var string_array = @string.MakeArrayType (3);

			Assert.IsInstanceOf (typeof (ArrayType), string_array);
			Assert.AreEqual (3, string_array.Rank);
		}

		[Test]
		public void MakePointerType ()
		{
			var @string = GetTypeReference (typeof (string));

			var string_ptr = @string.MakePointerType ();

			Assert.IsInstanceOf (typeof (PointerType), string_ptr);
		}

		[Test]
		public void MakeByReferenceType ()
		{
			var @string = GetTypeReference (typeof (string));

			var string_byref = @string.MakeByReferenceType ();

			Assert.IsInstanceOf (typeof (ByReferenceType), string_byref);
		}

		class OptionalModifier {}

		[Test]
		public void MakeOptionalModifierType ()
		{
			var @string = GetTypeReference (typeof (string));
			var modopt = GetTypeReference (typeof (OptionalModifier));

			var string_modopt = @string.MakeOptionalModifierType (modopt);

			Assert.IsInstanceOf (typeof (OptionalModifierType), string_modopt);
			Assert.AreEqual (modopt, string_modopt.ModifierType);
		}

		class RequiredModifier { }

		[Test]
		public void MakeRequiredModifierType ()
		{
			var @string = GetTypeReference (typeof (string));
			var modreq = GetTypeReference (typeof (RequiredModifierType));

			var string_modreq = @string.MakeRequiredModifierType (modreq);

			Assert.IsInstanceOf (typeof (RequiredModifierType), string_modreq);
			Assert.AreEqual (modreq, string_modreq.ModifierType);
		}

		[Test]
		public void MakePinnedType ()
		{
			var byte_array = GetTypeReference (typeof (byte []));

			var pinned_byte_array = byte_array.MakePinnedType ();

			Assert.IsInstanceOf (typeof (PinnedType), pinned_byte_array);
		}

		[Test]
		public void MakeSentinelType ()
		{
			var @string = GetTypeReference (typeof (string));

			var string_sentinel = @string.MakeSentinelType ();

			Assert.IsInstanceOf (typeof (SentinelType), string_sentinel);
		}

		class Foo<T1, T2> {}

		[Test]
		public void MakeGenericInstanceType ()
		{
			var foo = GetTypeReference (typeof (Foo<,>));
			var @string = GetTypeReference (typeof (string));
			var @int = GetTypeReference (typeof (int));

			var foo_string_int = foo.MakeGenericInstanceType (@string, @int);

			Assert.IsInstanceOf (typeof (GenericInstanceType), foo_string_int);
			Assert.AreEqual (2, foo_string_int.GenericArguments.Count);
			Assert.AreEqual (@string, foo_string_int.GenericArguments [0]);
			Assert.AreEqual (@int, foo_string_int.GenericArguments [1]);
		}

		static TypeReference GetTypeReference (Type type)
		{
			return ModuleDefinition.ReadModule (typeof (TypeReferenceRocksTests).Module.FullyQualifiedName).ImportReference (type);
		}
	}
}