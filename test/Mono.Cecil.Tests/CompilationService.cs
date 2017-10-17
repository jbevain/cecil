using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

#if NETCOREAPP
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.VisualBasic;
#else
using System.CodeDom.Compiler;
#endif

namespace Mono.Cecil.Tests {

	struct CompilationResult {
		internal DateTime source_write_time;
		internal string result_file;

		public CompilationResult (DateTime write_time, string result_file)
		{
			this.source_write_time = write_time;
			this.result_file = result_file;
		}
	}

	public static class Platform {

		public static bool OnMono { get { return typeof (object).Assembly.GetType ("Mono.Runtime") != null; } }
	}

	abstract class CompilationService {

		Dictionary<string, CompilationResult> files = new Dictionary<string, CompilationResult> ();

		bool TryGetResult (string name, out string file_result)
		{
			file_result = null;
			CompilationResult result;
			if (!files.TryGetValue (name, out result))
				return false;

			if (result.source_write_time != File.GetLastWriteTime (name))
				return false;

			file_result = result.result_file;
			return true;
		}

		public string Compile (string name)
		{
			string result_file;
			if (TryGetResult (name, out result_file))
				return result_file;

			result_file = CompileFile (name);
			RegisterFile (name, result_file);
			return result_file;
		}

		void RegisterFile (string name, string result_file)
		{
			files [name] = new CompilationResult (File.GetLastWriteTime (name), result_file);
		}

		protected abstract string CompileFile (string name);

		public static string CompileResource (string name)
		{
			var extension = Path.GetExtension (name);
			if (extension == ".il")
				return IlasmCompilationService.Instance.Compile (name);

			if (extension == ".cs" || extension == ".vb")
#if NETCOREAPP
				return RoslynCompilationService.Instance.Compile (name);
#else
				return CodeDomCompilationService.Instance.Compile (name);
#endif

			throw new NotSupportedException (extension);
		}

		protected static string GetCompiledFilePath (string file_name)
		{
			var tmp_cecil = Path.Combine (Path.GetTempPath (), "cecil");
			if (!Directory.Exists (tmp_cecil))
				Directory.CreateDirectory (tmp_cecil);

			return Path.Combine (tmp_cecil, Path.GetFileName (file_name) + ".dll");
		}

		public static void Verify (string name)
		{
			var output = Platform.OnMono ? ShellService.PEDump (name) : ShellService.PEVerify (name);
			if (output.ExitCode != 0)
				Assert.Fail (output.ToString ());
		}
	}

	class IlasmCompilationService : CompilationService {

		public static readonly IlasmCompilationService Instance = new IlasmCompilationService ();

		protected override string CompileFile (string name)
		{
			string file = GetCompiledFilePath (name);

			var output = ShellService.ILAsm (name, file);

			AssertAssemblerResult (output);

			return file;
		}

		static void AssertAssemblerResult (ShellService.ProcessOutput output)
		{
			if (output.ExitCode != 0)
				Assert.Fail (output.ToString ());
		}
	}

#if NETCOREAPP

	/// <summary>
	/// Roslyn implementation for a <see cref="CompilationService"/>.
	/// </summary>
	class RoslynCompilationService : CompilationService {

		/// <summary>
		/// Global instance of the compilation service.
		/// </summary>
		public static readonly RoslynCompilationService Instance = new RoslynCompilationService ();

		/// <summary>
		/// Compiles a source code file.
		/// </summary>
		/// <param name="sourceCodeFilePath">Path to the source code file to be compiled.</param>
		/// <returns>Path to the compiled assembly file.</returns>
		protected override string CompileFile (string sourceCodeFilePath)
		{
			string fileExtension = Path.GetExtension (sourceCodeFilePath);
			string assemblyFilePath = GetCompiledFilePath (sourceCodeFilePath);

			EmitResult results;
			switch (fileExtension) {
			case ".cs":
				results = CompileCSharp (sourceCodeFilePath, assemblyFilePath);
				break;

			case ".vb":
				results = CompileVisualBasic (sourceCodeFilePath, assemblyFilePath);
				break;

			default:
				throw new NotSupportedException ($"Cannot compile file '{sourceCodeFilePath}'.");
			}

			AssertCompilerResults (results);

			return assemblyFilePath;
		}

		/// <summary>
		/// Asserts the result of a compilation.
		/// </summary>
		/// <param name="results">Result of a compilation.</param>
		static void AssertCompilerResults (EmitResult results)
		{
			Assert.IsTrue (results.Success, GetErrorMessage (results));
		}

		/// <summary>
		/// Compiles a C# source code file.
		/// </summary>
		/// <param name="sourceCodeFilePath">Path to the source code file.</param>
		/// <param name="assemblyFilePath">Path to the destination compiled assembly file.</param>
		/// <returns>Result of the compilation.</returns>
		static EmitResult CompileCSharp (string sourceCodeFilePath, string assemblyFilePath)
		{
			string assemblyName = Path.GetFileNameWithoutExtension (assemblyFilePath);
			MetadataReference [] references = GetAssemblyReferences ();
			CSharpCompilationOptions options = new CSharpCompilationOptions (OutputKind.DynamicallyLinkedLibrary);

			SyntaxTree [] syntaxTree;
			using (Stream stream = File.OpenRead (sourceCodeFilePath)) {
				syntaxTree = new SyntaxTree [] { CSharpSyntaxTree.ParseText (SourceText.From (stream)) };
			}

			CSharpCompilation compilation = CSharpCompilation.Create (assemblyName, syntaxTree, references, options);

			using (Stream output = File.Create (assemblyFilePath)) {
				return compilation.Emit (output);
			}
		}

		/// <summary>
		/// Compiles a Visual Basic source code file.
		/// </summary>
		/// <param name="sourceCodeFilePath">Path to the source code file.</param>
		/// <param name="assemblyFilePath">Path to the destination compiled file.</param>
		/// <returns>Result of the compilation.</returns>
		static EmitResult CompileVisualBasic (string sourceCodeFilePath, string assemblyFilePath)
		{
			string assemblyName = Path.GetFileNameWithoutExtension (assemblyFilePath);
			MetadataReference [] references = GetAssemblyReferences ();
			VisualBasicCompilationOptions options = new VisualBasicCompilationOptions (OutputKind.DynamicallyLinkedLibrary);

			SyntaxTree [] syntaxTree;
			using (Stream stream = File.OpenRead (sourceCodeFilePath)) {
				syntaxTree = new SyntaxTree [] { VisualBasicSyntaxTree.ParseText (SourceText.From (stream)) };
			}

			VisualBasicCompilation compilation = VisualBasicCompilation.Create (assemblyName, syntaxTree, references, options);

			using (Stream output = File.Create (assemblyFilePath)) {
				return compilation.Emit (output);
			}
		}

		/// <summary>
		/// Gets a list of assembly references.
		/// </summary>
		/// <returns>List of assembly references.</returns>
		static MetadataReference [] GetAssemblyReferences ()
		{
			return new MetadataReference []
			{
				MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
			};
		}

		/// <summary>
		/// Gets the error message for a compilation.
		/// </summary>
		/// <param name="results">Result of a compilation.</param>
		/// <returns>Error message for the compilation results.</returns>
		static string GetErrorMessage (EmitResult results)
		{
			if (results.Success)
				return string.Empty;

			var builder = new StringBuilder ();
			foreach (Diagnostic error in results.Diagnostics)
				builder.AppendLine (error.ToString ());
			return builder.ToString ();
		}
	}

#else

	class CodeDomCompilationService : CompilationService {

		public static readonly CodeDomCompilationService Instance = new CodeDomCompilationService ();

		protected override string CompileFile (string name)
		{
			string file = GetCompiledFilePath (name);

			using (var provider = GetProvider (name)) {
				var parameters = GetDefaultParameters (name);
				parameters.IncludeDebugInformation = false;
				parameters.GenerateExecutable = false;
				parameters.OutputAssembly = file;

				var results = provider.CompileAssemblyFromFile (parameters, name);
				AssertCompilerResults (results);
			}

			return file;
		}

		static void AssertCompilerResults (CompilerResults results)
		{
			Assert.IsFalse (results.Errors.HasErrors, GetErrorMessage (results));
		}

		static string GetErrorMessage (CompilerResults results)
		{
			if (!results.Errors.HasErrors)
				return string.Empty;

			var builder = new StringBuilder ();
			foreach (CompilerError error in results.Errors)
				builder.AppendLine (error.ToString ());
			return builder.ToString ();
		}

		static CompilerParameters GetDefaultParameters (string name)
		{
			return GetCompilerInfo (name).CreateDefaultCompilerParameters ();
		}

		static CodeDomProvider GetProvider (string name)
		{
			return GetCompilerInfo (name).CreateProvider ();
		}

		static CompilerInfo GetCompilerInfo (string name)
		{
			return CodeDomProvider.GetCompilerInfo (
				CodeDomProvider.GetLanguageFromExtension (Path.GetExtension (name)));
		}
	}

#endif

	class ShellService {

		public class ProcessOutput {

			public int ExitCode;
			public string StdOut;
			public string StdErr;

			public ProcessOutput (int exitCode, string stdout, string stderr)
			{
				ExitCode = exitCode;
				StdOut = stdout;
				StdErr = stderr;
			}

			public override string ToString ()
			{
				return StdOut + StdErr;
			}
		}

		static ProcessOutput RunProcess (string target, params string [] arguments)
		{
			var stdout = new StringWriter ();
			var stderr = new StringWriter ();

			var process = new Process {
				StartInfo = new ProcessStartInfo {
					FileName = target,
					Arguments = string.Join (" ", arguments),
					CreateNoWindow = true,
					UseShellExecute = false,
					RedirectStandardError = true,
					RedirectStandardInput = true,
					RedirectStandardOutput = true,
				},
			};

			process.Start ();

			process.OutputDataReceived += (_, args) => stdout.Write (args.Data);
			process.ErrorDataReceived += (_, args) => stderr.Write (args.Data);

			process.BeginOutputReadLine ();
			process.BeginErrorReadLine ();

			process.WaitForExit ();

			return new ProcessOutput (process.ExitCode, stdout.ToString (), stderr.ToString ());
		}

		public static ProcessOutput ILAsm (string source, string output)
		{
			var ilasm = "ilasm";
			if (!Platform.OnMono)
				ilasm = NetFrameworkTool ("ilasm");

			return RunProcess (ilasm, "/nologo", "/dll", "/out:" + Quote (output), Quote (source));
		}

		static string Quote (string file)
		{
			return "\"" + file + "\"";
		}

		public static ProcessOutput PEVerify (string source)
		{
			return RunProcess (WinSdkTool ("peverify"), "/nologo", Quote (source));
		}

		public static ProcessOutput PEDump (string source)
		{
			return RunProcess ("pedump", "--verify code,metadata", Quote (source));
		}

		static string NetFrameworkTool (string tool)
		{
			return Path.Combine (
				Path.GetDirectoryName (typeof (object).Assembly.Location),
				tool + ".exe");
		}

		static string WinSdkTool (string tool)
		{
			var sdks = new [] {
				@"Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.7 Tools",
				@"Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.2 Tools",
				@"Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools",
				@"Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6 Tools",
				@"Microsoft SDKs\Windows\v8.1A\bin\NETFX 4.5.1 Tools",
				@"Microsoft SDKs\Windows\v8.0A\bin\NETFX 4.0 Tools",
				@"Microsoft SDKs\Windows\v7.0A\Bin",
			};

			foreach (var sdk in sdks) {
				var pgf = IntPtr.Size == 8
					? Environment.GetEnvironmentVariable("ProgramFiles(x86)")
					: Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

				var exe = Path.Combine (
					Path.Combine (pgf, sdk),
					tool + ".exe");

				if (File.Exists(exe))
					return exe;
			}

			return tool;
		}
	}
}
