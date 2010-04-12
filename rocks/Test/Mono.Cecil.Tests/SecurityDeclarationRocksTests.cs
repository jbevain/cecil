using NUnit.Framework;

using Mono.Cecil.Rocks;

namespace Mono.Cecil.Tests {

	[TestFixture]
	public class SecurityDeclarationRocksTests : BaseTestFixture {

		[TestModule ("decsec-xml.dll")]
		public void ToPermissionSetFromPermissionSetAttribute (ModuleDefinition module)
		{
			var type = module.GetType ("SubLibrary");

			Assert.IsTrue (type.HasSecurityDeclarations);
			Assert.AreEqual (1, type.SecurityDeclarations.Count);

			var declaration = type.SecurityDeclarations [0];

			var permission_set = declaration.ToPermissionSet ();

			Assert.IsNotNull (permission_set);

			const string permission_set_value = "<PermissionSet class=\"System.Security.PermissionSe"
	+ "t\"\r\nversion=\"1\">\r\n<IPermission class=\"System.Security.Permis"
	+ "sions.SecurityPermission, mscorlib, Version=2.0.0.0, Culture"
	+ "=neutral, PublicKeyToken=b77a5c561934e089\"\r\nversion=\"1\"\r\nFla"
	+ "gs=\"UnmanagedCode\"/>\r\n</PermissionSet>\r\n";

			Assert.AreEqual (Normalize (permission_set_value), Normalize (permission_set.ToXml ().ToString ()));
		}

		[TestModule ("decsec-att.dll")]
		public void ToPermissionSetFromSecurityAttribute (ModuleDefinition module)
		{
			var type = module.GetType ("SubLibrary");

			Assert.IsTrue (type.HasSecurityDeclarations);
			Assert.AreEqual (1, type.SecurityDeclarations.Count);

			var declaration = type.SecurityDeclarations [0];

			var permission_set = declaration.ToPermissionSet ();

			Assert.IsNotNull (permission_set);

			const string permission_set_value = "<PermissionSet class=\"System.Security.PermissionSe"
	+ "t\"\r\nversion=\"1\">\r\n<IPermission class=\"System.Security.Permis"
	+ "sions.SecurityPermission, mscorlib, Version=2.0.0.0, Culture"
	+ "=neutral, PublicKeyToken=b77a5c561934e089\"\r\nversion=\"1\"\r\nFla"
	+ "gs=\"UnmanagedCode\"/>\r\n</PermissionSet>\r\n";

			Assert.AreEqual (Normalize (permission_set_value), Normalize (permission_set.ToXml ().ToString ()));
		}

		static string Normalize (string s)
		{
			return s.Replace ("\n", "").Replace ("\r", "");
		}
	}
}
