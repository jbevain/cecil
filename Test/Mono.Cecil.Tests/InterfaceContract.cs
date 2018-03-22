using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using Mono.Cecil;

using NUnit.Framework;

namespace Mono.Cecil.Tests
{
	[TestFixture] 
	public class InterfaceContract
    {
		private IEnumerable<TypeDefinition> GetNestedTypes(TypeDefinition main)
		{
			return main.NestedTypes.SelectMany (t => GetNestedTypes (t)).Concat(new [] { main });
		}


		[Test]
		public void EnsureConsistentVersioning ()
		{
			var cecilDll = typeof (Mono.TypeExtensions).Assembly.Location;
			var assembly = AssemblyDefinition.ReadAssembly (cecilDll);
			var version = assembly.Name;
			Console.WriteLine (version);
			var visibleTypes = assembly.Modules
				.SelectMany (m => m.Types)
				.SelectMany (GetNestedTypes)
				.Where (t => !t.IsNestedPrivate && !t.IsNestedFamily)
				.ToList ();
			var visibleFields = visibleTypes
				.SelectMany (t => t.Fields)
				.Where (m => m.IsPublic || m.IsAssembly)
				.ToList ();
			var visibleMethods = visibleTypes
				.SelectMany (t => t.Methods)
				.Where (m => m.IsPublic || m.IsAssembly)
				.ToList ();

			byte [] signature = null;

			using (var hash = SHA256.Create ()) {
				visibleTypes.ForEach (t => {
					var ta = Encoding.UTF8.GetBytes (t.FullName);
					hash.TransformBlock (ta, 0, ta.Length, ta, 0);
				});

				visibleFields.ForEach (t => {
					var ta = Encoding.UTF8.GetBytes (t.FullName);
					hash.TransformBlock (ta, 0, ta.Length, ta, 0);
				});

				visibleMethods.ForEach (t => {
					var ta = Encoding.UTF8.GetBytes (t.FullName);
					hash.TransformBlock (ta, 0, ta.Length, ta, 0);
				});

				var a = Encoding.UTF8.GetBytes (version.FullName);
				hash.TransformFinalBlock (a, 0, a.Length);

				signature = hash.Hash;
			}

			StringBuilder str = new StringBuilder ();
			Array.ForEach (signature,
				i => str.AppendFormat ("{0:X2}", i));
			var result = str.ToString ();
			var expected = "4EDFA26604A9E9FF7536DA6280624B8B35492B17BB2007329C259398E75930B7";

			Assert.That (result, Is.EqualTo (expected),
				"The interface signature has changed and is now '" +
				result + "'.  If there has been a public release, " +
				"increment the assembly version to reflect this fact," +
				"then rerun this test and record the new value from this failure." +
				"Update 'expected' with that value, and verify that the test is now clean."
				);
		}
	}
}
