using System;
using System.IO;
using System.Reflection;
using Mono.Cecil.Cil;
using NUnit.Framework;

using Mono.Cecil.PE;

namespace Mono.Cecil.Tests {

	public abstract class BaseTestFixture {

		protected static void IgnoreOnMono ()
		{
			if (Platform.OnMono)
				Assert.Ignore ();
		}

		public static string GetResourcePath (string name, Assembly assembly)
		{
			return Path.Combine (FindResourcesDirectory (assembly), name);
		}

		public static string GetAssemblyResourcePath (string name, Assembly assembly)
		{
			return GetResourcePath (Path.Combine ("assemblies", name), assembly);
		}

		public static string GetCSharpResourcePath (string name, Assembly assembly)
		{
			return GetResourcePath (Path.Combine ("cs", name), assembly);
		}

		public static string GetILResourcePath (string name, Assembly assembly)
		{
			return GetResourcePath (Path.Combine ("il", name), assembly);
		}

		public ModuleDefinition GetResourceModule (string name)
		{
			return ModuleDefinition.ReadModule (GetAssemblyResourcePath (name, GetType ().GetTypeInfo().Assembly));
		}

		public ModuleDefinition GetResourceModule (string name, ReaderParameters parameters)
		{
			return ModuleDefinition.ReadModule (GetAssemblyResourcePath (name, GetType ().GetTypeInfo ().Assembly), parameters);
		}

		public ModuleDefinition GetResourceModule (string name, ReadingMode mode)
		{
			return ModuleDefinition.ReadModule (GetAssemblyResourcePath (name, GetType ().GetTypeInfo ().Assembly), new ReaderParameters (mode));
		}

		internal Image GetResourceImage (string name)
		{
			var file = new FileStream (GetAssemblyResourcePath (name, GetType ().GetTypeInfo ().Assembly), FileMode.Open, FileAccess.Read);
			return ImageReader.ReadImage (Disposable.Owned (file as Stream), file.Name);
		}

		public ModuleDefinition GetCurrentModule ()
		{
			return ModuleDefinition.ReadModule (GetType ().GetTypeInfo ().Module.FullyQualifiedName);
		}

		public ModuleDefinition GetCurrentModule (ReaderParameters parameters)
		{
			return ModuleDefinition.ReadModule (GetType ().GetTypeInfo().Module.FullyQualifiedName, parameters);
		}

		public static string FindResourcesDirectory (Assembly assembly)
		{
			var path = Path.GetDirectoryName (assembly.ManifestModule.FullyQualifiedName);
			while (!Directory.Exists (Path.Combine (path, "Resources"))) {
				var old = path;
				path = Path.GetDirectoryName (path);
				Assert.AreNotEqual (old, path);
			}

			return Path.Combine (path, "Resources");
		}

		public static void AssertCode (string expected, MethodDefinition method)
		{
			Assert.IsTrue (method.HasBody);
			Assert.IsNotNull (method.Body);

			Assert.AreEqual (Normalize (expected), Normalize (Formatter.FormatMethodBody (method)));
		}

		public static string Normalize (string str)
		{
			return str.Trim ().Replace ("\r\n", "\n");
		}

		public static void TestModule (string file, Action<ModuleDefinition> test, bool verify = true, bool readOnly = false, Type symbolReaderProvider = null, Type symbolWriterProvider = null, IAssemblyResolver assemblyResolver = null, bool applyWindowsRuntimeProjections = false)
		{
			Run (new ModuleTestCase (file, test, verify, readOnly, symbolReaderProvider, symbolWriterProvider, assemblyResolver, applyWindowsRuntimeProjections));
		}

		public static void TestCSharp (string file, Action<ModuleDefinition> test, bool verify = true, bool readOnly = false, Type symbolReaderProvider = null, Type symbolWriterProvider = null, IAssemblyResolver assemblyResolver = null, bool applyWindowsRuntimeProjections = false)
		{
			Run (new CSharpTestCase (file, test, verify, readOnly, symbolReaderProvider, symbolWriterProvider, assemblyResolver, applyWindowsRuntimeProjections));
		}

		public static void TestIL (string file, Action<ModuleDefinition> test, bool verify = true, bool readOnly = false, Type symbolReaderProvider = null, Type symbolWriterProvider = null, IAssemblyResolver assemblyResolver = null, bool applyWindowsRuntimeProjections = false)
		{
			Run (new ILTestCase (file, test, verify, readOnly, symbolReaderProvider, symbolWriterProvider, assemblyResolver, applyWindowsRuntimeProjections));
		}

		static void Run (TestCase testCase)
		{
			using (var runner = new TestRunner (testCase, TestCaseType.ReadDeferred))
				runner.RunTest ();

			using (var runner = new TestRunner (testCase, TestCaseType.ReadImmediate))
				runner.RunTest ();

			if (testCase.ReadOnly)
				return;

#if !READ_ONLY
			using (var runner = new TestRunner (testCase, TestCaseType.WriteFromDeferred))
				runner.RunTest ();

			using (var runner = new TestRunner (testCase, TestCaseType.WriteFromImmediate))
				runner.RunTest ();
#endif
		}
	}

	abstract class TestCase {

		public readonly bool Verify;
		public readonly bool ReadOnly;
		public readonly Type SymbolReaderProvider;
		public readonly Type SymbolWriterProvider;
		public readonly IAssemblyResolver AssemblyResolver;
		public readonly Action<ModuleDefinition> Test;
		public readonly bool ApplyWindowsRuntimeProjections;

		public abstract string ModuleLocation { get; }

		protected Assembly Assembly { get { return Test.GetMethodInfo().Module.Assembly; } }

		protected TestCase (Action<ModuleDefinition> test, bool verify, bool readOnly, Type symbolReaderProvider, Type symbolWriterProvider, IAssemblyResolver assemblyResolver, bool applyWindowsRuntimeProjections)
		{
			Test = test;
			Verify = verify;
			ReadOnly = readOnly;
			SymbolReaderProvider = symbolReaderProvider;
			SymbolWriterProvider = symbolWriterProvider;
			AssemblyResolver = assemblyResolver;
			ApplyWindowsRuntimeProjections = applyWindowsRuntimeProjections;
		}
	}

	class ModuleTestCase : TestCase {

		public readonly string Module;

		public ModuleTestCase (string module, Action<ModuleDefinition> test, bool verify, bool readOnly, Type symbolReaderProvider, Type symbolWriterProvider, IAssemblyResolver assemblyResolver, bool applyWindowsRuntimeProjections)
			: base (test, verify, readOnly, symbolReaderProvider, symbolWriterProvider, assemblyResolver, applyWindowsRuntimeProjections)
		{
			Module = module;
		}

		public override string ModuleLocation
		{
			get { return BaseTestFixture.GetAssemblyResourcePath (Module, Assembly); }
		}
	}

	class CSharpTestCase : TestCase {

		public readonly string File;

		public CSharpTestCase (string file, Action<ModuleDefinition> test, bool verify, bool readOnly, Type symbolReaderProvider, Type symbolWriterProvider, IAssemblyResolver assemblyResolver, bool applyWindowsRuntimeProjections)
			: base (test, verify, readOnly, symbolReaderProvider, symbolWriterProvider, assemblyResolver, applyWindowsRuntimeProjections)
		{
			File = file;
		}

		public override string ModuleLocation
		{
			get
			{
				return CompilationService.CompileResource (BaseTestFixture.GetCSharpResourcePath (File, Assembly));
			}
		}
	}

	class ILTestCase : TestCase {

		public readonly string File;

		public ILTestCase (string file, Action<ModuleDefinition> test, bool verify, bool readOnly, Type symbolReaderProvider, Type symbolWriterProvider, IAssemblyResolver assemblyResolver, bool applyWindowsRuntimeProjections)
			: base (test, verify, readOnly, symbolReaderProvider, symbolWriterProvider, assemblyResolver, applyWindowsRuntimeProjections)
		{
			File = file;
		}

		public override string ModuleLocation
		{
			get
			{
				return CompilationService.CompileResource (BaseTestFixture.GetILResourcePath (File, Assembly)); ;
			}
		}
	}

	class TestRunner : IDisposable {

		readonly TestCase test_case;
		readonly TestCaseType type;

		ModuleDefinition test_module;
		DefaultAssemblyResolver test_resolver;

		public TestRunner (TestCase testCase, TestCaseType type)
		{
			this.test_case = testCase;
			this.type = type;
		}

		ModuleDefinition GetModule ()
		{
			var location = test_case.ModuleLocation;

			var parameters = new ReaderParameters {
				SymbolReaderProvider = GetSymbolReaderProvider (),
				AssemblyResolver = GetAssemblyResolver (),
				ApplyWindowsRuntimeProjections = test_case.ApplyWindowsRuntimeProjections
			};

			switch (type) {
			case TestCaseType.ReadImmediate:
				parameters.ReadingMode = ReadingMode.Immediate;
				return ModuleDefinition.ReadModule (location, parameters);
			case TestCaseType.ReadDeferred:
				parameters.ReadingMode = ReadingMode.Deferred;
				return ModuleDefinition.ReadModule (location, parameters);
#if !READ_ONLY
			case TestCaseType.WriteFromImmediate:
				parameters.ReadingMode = ReadingMode.Immediate;
				return RoundTrip (location, parameters, "cecil-irt");
			case TestCaseType.WriteFromDeferred:
				parameters.ReadingMode = ReadingMode.Deferred;
				return RoundTrip (location, parameters, "cecil-drt");
#endif
			default:
				return null;
			}
		}

		ISymbolReaderProvider GetSymbolReaderProvider ()
		{
			if (test_case.SymbolReaderProvider == null)
				return null;

			return (ISymbolReaderProvider) Activator.CreateInstance (test_case.SymbolReaderProvider);
		}

#if !READ_ONLY
		ISymbolWriterProvider GetSymbolWriterProvider ()
		{
			if (test_case.SymbolReaderProvider == null)
				return null;

			return (ISymbolWriterProvider) Activator.CreateInstance (test_case.SymbolWriterProvider);
		}
#endif

		IAssemblyResolver GetAssemblyResolver ()
		{
			if (test_case.AssemblyResolver != null)
				return test_case.AssemblyResolver;

			test_resolver = new DefaultAssemblyResolver ();
			var directory = Path.GetDirectoryName (test_case.ModuleLocation);
			test_resolver.AddSearchDirectory (directory);
			return test_resolver;
		}

#if !READ_ONLY
		ModuleDefinition RoundTrip (string location, ReaderParameters reader_parameters, string folder)
		{
			var rt_folder = Path.Combine (Path.GetTempPath (), folder);
			if (!Directory.Exists (rt_folder))
				Directory.CreateDirectory (rt_folder);
			var rt_module = Path.Combine (rt_folder, Path.GetFileName (location));

			using (var module = ModuleDefinition.ReadModule (location, reader_parameters)) {
				var writer_parameters = new WriterParameters {
					SymbolWriterProvider = GetSymbolWriterProvider (),
				};

				test_case.Test (module);

				module.Write (rt_module, writer_parameters);
			}

			if (test_case.Verify)
				CompilationService.Verify (rt_module);

			return ModuleDefinition.ReadModule (rt_module, reader_parameters);
		}
#endif
		public void RunTest ()
		{
			var module = GetModule ();
			if (module == null)
				return;

			test_module = module;
			test_case.Test (module);
		}

		public void Dispose ()
		{
			if (test_module != null)
				test_module.Dispose ();

			if (test_resolver != null)
				test_resolver.Dispose ();
		}
	}

	enum TestCaseType {
		ReadImmediate,
		ReadDeferred,
#if !READ_ONLY
		WriteFromImmediate,
		WriteFromDeferred,
#endif
	}
}
