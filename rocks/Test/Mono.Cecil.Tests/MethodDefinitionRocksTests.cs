using System.Linq;
using Mono.Cecil.Rocks;
using NUnit.Framework;

namespace Mono.Cecil.Tests
{
	[TestFixture]
	public class MethodDefinitionRocksTests : BaseTestFixture
	{
		private abstract class Foo
		{
			public abstract void DoFoo();

			public abstract void NewFoo();
		}

		private class Bar : Foo
		{
			public override void DoFoo()
			{
			}

			public override void NewFoo()
			{
			}
		}

		private class Baz : Bar
		{
			public override void DoFoo()
			{
			}

			// This method does not override but is still virtual
			public virtual new void NewFoo()
			{
			}
		}

		[Test]
		public void GetBaseMethod()
		{
			var baz = typeof(Baz).ToDefinition();
			var baz_dofoo = baz.GetMethod("DoFoo");

			var @base = baz_dofoo.GetBaseMethod();
			Assert.AreEqual("Bar", @base.DeclaringType.Name);

			@base = @base.GetBaseMethod();
			Assert.AreEqual("Foo", @base.DeclaringType.Name);

			Assert.AreEqual(@base, @base.GetBaseMethod());

			// Stop on virtual new method
			var baz_newfoo = baz.GetMethod("NewFoo");
			@base = baz_newfoo.GetBaseMethod();
			Assert.AreEqual("Baz", @base.DeclaringType.Name);
		}

		[Test]
		public void GetOriginalBaseMethod()
		{
			var baz = typeof(Baz).ToDefinition();
			var baz_dofoo = baz.GetMethod("DoFoo");

			var @base = baz_dofoo.GetOriginalBaseMethod();
			Assert.AreEqual("Foo", @base.DeclaringType.Name);
		}
	}
}
