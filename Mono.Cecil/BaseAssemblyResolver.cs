//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

using Mono.Collections.Generic;

namespace Mono.Cecil {

	public delegate AssemblyDefinition AssemblyResolveEventHandler (object sender, AssemblyNameReference reference);

	public sealed class AssemblyResolveEventArgs : EventArgs {
		private readonly AssemblyNameReference reference;

		public AssemblyNameReference AssemblyReference {
			get { return reference; }
		}

		public AssemblyResolveEventArgs (AssemblyNameReference reference)
		{
			this.reference = reference;
		}
	}

#if !NET_CORE
	[Serializable]
#endif

	public sealed class AssemblyResolutionException : FileNotFoundException {
		private readonly AssemblyNameReference reference;

		public AssemblyNameReference AssemblyReference {
			get { return reference; }
		}

		public AssemblyResolutionException (AssemblyNameReference reference)
			: this (reference, null)
		{
		}

		public AssemblyResolutionException (AssemblyNameReference reference, Exception innerException)
			: base (string.Format ("Failed to resolve assembly: '{0}'", reference), innerException)
		{
			this.reference = reference;
		}

#if !NET_CORE
		AssemblyResolutionException (
			System.Runtime.Serialization.SerializationInfo info,
			System.Runtime.Serialization.StreamingContext context)
			: base (info, context)
		{
		}
#endif
	}

	public abstract class BaseAssemblyResolver : IAssemblyResolver {
		private static readonly bool on_mono = Type.GetType ("Mono.Runtime") != null;

		private readonly Collection<string> directories;

		// Maps file names of available trusted platform assemblies to their full paths.
		// Internal for testing.
		internal static readonly Lazy<Dictionary<string, string>> TrustedPlatformAssemblies = new Lazy<Dictionary<string, string>> (CreateTrustedPlatformAssemblyMap);

		private Collection<string> gac_paths;

		public void AddSearchDirectory (string directory)
		{
			directories.Add (directory);
		}

		public void RemoveSearchDirectory (string directory)
		{
			directories.Remove (directory);
		}

		protected bool NetCore { get; set; }

		protected bool AsMono { get; set; }

		protected Module CoreModule { get; set; }

		public string [] GetSearchDirectories ()
		{
			var directories = new string [this.directories.size];
			Array.Copy (this.directories.items, directories, directories.Length);
			return directories;
		}

		public event AssemblyResolveEventHandler ResolveFailure;

		protected BaseAssemblyResolver ()
		{
			directories = new Collection<string> (2) { ".", "bin" };
#if NET_CORE
			NetCore = true;
#else
			NetCore = false;
#endif

			AsMono = on_mono;
			CoreModule = typeof (object).Module;
		}

		private AssemblyDefinition GetAssembly (string file, ReaderParameters parameters)
		{
			if (parameters.AssemblyResolver == null)
				parameters.AssemblyResolver = this;

			return ModuleDefinition.ReadModule (file, parameters).Assembly;
		}

		public virtual AssemblyDefinition Resolve (AssemblyNameReference name)
		{
			return Resolve (name, new ReaderParameters ());
		}

		public virtual AssemblyDefinition Resolve (AssemblyNameReference name, ReaderParameters parameters)
		{
			Mixin.CheckName (name);
			Mixin.CheckParameters (parameters);

			var assembly = SearchDirectory (name, directories, parameters);
			if (assembly != null)
				return assembly;

			if (name.IsRetargetable) {
				// if the reference is retargetable, zero it
				name = new AssemblyNameReference (name.Name, Mixin.ZeroVersion) {
					PublicKeyToken = Empty<byte>.Array,
				};
			}

			assembly = NetCore ? SearchTrustedPlatformAssemblies (name, parameters) :
								 SearchFrameworkAssemblies (name, parameters);
			if (assembly != null)
				return assembly;

			assembly = LastChanceResolution (assembly, name, parameters);
			if (assembly != null)
				return assembly;

			throw new AssemblyResolutionException (name);
		}

		protected virtual AssemblyDefinition LastChanceResolution (AssemblyDefinition assembly, AssemblyNameReference name, ReaderParameters parameters)
		{
			if (ResolveFailure != null) {
				assembly = ResolveFailure (this, name);
			}

			return assembly;
		}

		protected AssemblyDefinition SearchFrameworkAssemblies (AssemblyNameReference name, ReaderParameters parameters)
		{
			AssemblyDefinition assembly = null;

			var framework_dir = Path.GetDirectoryName (CoreModule.FullyQualifiedName);
			var framework_dirs = AsMono
				? new [] { framework_dir, Path.Combine (framework_dir, "Facades") }
				: new [] { framework_dir };

			if (IsZero (name.Version)) {
				assembly = SearchDirectory (name, framework_dirs, parameters);
				if (assembly != null)
					return assembly;
			}

			if (name.Name == "mscorlib") {
				assembly = GetCorlib (name, parameters);
				if (assembly != null)
					return assembly;
			}

			assembly = GetAssemblyInGac (name, parameters);
			if (assembly != null)
				return assembly;

			assembly = SearchDirectory (name, framework_dirs, parameters);
			return assembly;
		}

		protected AssemblyDefinition SearchTrustedPlatformAssemblies (AssemblyNameReference name, ReaderParameters parameters)
		{
			if (name.IsWindowsRuntime)
				return null;

			if (TrustedPlatformAssemblies.Value.TryGetValue (name.Name, out string path))
				return GetAssembly (path, parameters);

			return null;
		}

		private static Dictionary<string, string> CreateTrustedPlatformAssemblyMap ()
		{
			var result = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);

			string paths;

			try {
				paths = (string)AppDomain.CurrentDomain.GetData ("TRUSTED_PLATFORM_ASSEMBLIES");
			}
			catch {
				paths = null;
			}

			if (paths == null)
				return result;

			foreach (var path in paths.Split (Path.PathSeparator))
				if (string.Equals (Path.GetExtension (path), ".dll", StringComparison.OrdinalIgnoreCase))
					result [Path.GetFileNameWithoutExtension (path)] = path;

			return result;
		}

		protected virtual AssemblyDefinition SearchDirectory (AssemblyNameReference name, IEnumerable<string> directories, ReaderParameters parameters)
		{
			var extensions = name.IsWindowsRuntime ? new [] { ".winmd", ".dll" } : new [] { ".exe", ".dll" };
			foreach (var directory in directories) {
				foreach (var extension in extensions) {
					string file = Path.Combine (directory, name.Name + extension);
					if (!File.Exists (file))
						continue;
					try {
						return GetAssembly (file, parameters);
					}
					catch (System.BadImageFormatException) {
						continue;
					}
				}
			}

			return null;
		}

		private static bool IsZero (Version version)
		{
			return version.Major == 0 && version.Minor == 0 && version.Build == 0 && version.Revision == 0;
		}

		private AssemblyDefinition GetCorlib (AssemblyNameReference reference, ReaderParameters parameters)
		{
			var version = reference.Version;
			var corlib = CoreModule.Assembly.GetName (); // GetFramework
			if (corlib.Version == version || IsZero (version))
				return GetAssembly (CoreModule.FullyQualifiedName, parameters);

			var path = Directory.GetParent (
				Directory.GetParent (
					CoreModule.FullyQualifiedName).FullName
				).FullName;

			if (AsMono) {
				if (version.Major == 1)
					path = Path.Combine (path, "1.0");
				else if (version.Major == 2) {
					if (version.MajorRevision == 5)
						path = Path.Combine (path, "2.1");
					else
						path = Path.Combine (path, "2.0");
				} else if (version.Major == 4)
					path = Path.Combine (path, "4.0");
				else
					throw new NotSupportedException ("Version not supported: " + version);
			} else {
				switch (version.Major) {
				case 1:
					if (version.MajorRevision == 3300)
						path = Path.Combine (path, "v1.0.3705");
					else
						path = Path.Combine (path, "v1.1.4322");
					break;

				case 2:
					path = Path.Combine (path, "v2.0.50727");
					break;

				case 4:
					path = Path.Combine (path, "v4.0.30319");
					break;

				default:
					throw new NotSupportedException ("Version not supported: " + version);
				}
			}

			var file = Path.Combine (path, "mscorlib.dll");
			if (File.Exists (file))
				return GetAssembly (file, parameters);

			if (AsMono && Directory.Exists (path + "-api")) {
				file = Path.Combine (path + "-api", "mscorlib.dll");
				if (File.Exists (file))
					return GetAssembly (file, parameters);
			}

			return null;
		}

		private static Collection<string> GetGacPaths (bool mono, Module core)
		{
			if (mono)
				return GetDefaultMonoGacPaths (core);

			var paths = new Collection<string> (2);
			var windir = Environment.GetEnvironmentVariable ("WINDIR");
			if (windir == null)
				return paths;

			paths.Add (Path.Combine (windir, "assembly"));
			paths.Add (Path.Combine (windir, Path.Combine ("Microsoft.NET", "assembly")));
			return paths;
		}

		private static Collection<string> GetDefaultMonoGacPaths (Module core)
		{
			var paths = new Collection<string> (1);
			var gac = GetCurrentMonoGac (core);
			if (gac != null)
				paths.Add (gac);

			var gac_paths_env = Environment.GetEnvironmentVariable ("MONO_GAC_PREFIX");
			if (string.IsNullOrEmpty (gac_paths_env))
				return paths;

			var prefixes = gac_paths_env.Split (Path.PathSeparator);
			foreach (var prefix in prefixes) {
				if (string.IsNullOrEmpty (prefix))
					continue;

				var gac_path = Path.Combine (Path.Combine (Path.Combine (prefix, "lib"), "mono"), "gac");
				if (Directory.Exists (gac_path) && !paths.Contains (gac))
					paths.Add (gac_path);
			}

			return paths;
		}

		private static string GetCurrentMonoGac (Module core)
		{
			return Path.Combine (
				Directory.GetParent (
					Path.GetDirectoryName (core.FullyQualifiedName)).FullName,  // GetFrameworkDirectory
				"gac");
		}

		private AssemblyDefinition GetAssemblyInGac (AssemblyNameReference reference, ReaderParameters parameters)
		{
			if (reference.PublicKeyToken == null || reference.PublicKeyToken.Length == 0)
				return null;

			if (gac_paths == null)
				gac_paths = GetGacPaths (AsMono, CoreModule);

			if (AsMono)
				return GetAssemblyInMonoGac (reference, parameters);

			return GetAssemblyInNetGac (reference, parameters);
		}

		private AssemblyDefinition GetAssemblyInMonoGac (AssemblyNameReference reference, ReaderParameters parameters)
		{
			for (int i = 0; i < gac_paths.Count; i++) {
				var gac_path = gac_paths [i];
				var file = GetAssemblyFile (reference, string.Empty, gac_path);
				if (File.Exists (file))
					return GetAssembly (file, parameters);
			}

			return null;
		}

		private AssemblyDefinition GetAssemblyInNetGac (AssemblyNameReference reference, ReaderParameters parameters)
		{
			var gacs = new [] { "GAC_MSIL", "GAC_32", "GAC_64", "GAC" };
			var prefixes = new [] { string.Empty, "v4.0_" };

			for (int i = 0; i < gac_paths.Count; i++) {
				for (int j = 0; j < gacs.Length; j++) {
					var gac = Path.Combine (gac_paths [i], gacs [j]);
					var file = GetAssemblyFile (reference, prefixes [i], gac);
					if (Directory.Exists (gac) && File.Exists (file))
						return GetAssembly (file, parameters);
				}
			}

			return null;
		}

		private static string GetAssemblyFile (AssemblyNameReference reference, string prefix, string gac)
		{
			var gac_folder = new StringBuilder ()
				.Append (prefix)
				.Append (reference.Version)
				.Append ("__");

			for (int i = 0; i < reference.PublicKeyToken.Length; i++)
				gac_folder.Append (reference.PublicKeyToken [i].ToString ("x2"));

			return Path.Combine (
				Path.Combine (
					Path.Combine (gac, reference.Name), gac_folder.ToString ()),
				reference.Name + ".dll");
		}

		public void Dispose ()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
		}

		protected virtual void Dispose (bool disposing)
		{
		}
	}
}