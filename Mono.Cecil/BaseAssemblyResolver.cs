//
// BaseAssemblyResolver.cs
//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2010 Jb Evain
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Mono.Collections.Generic;

namespace Mono.Cecil {

	public abstract class BaseAssemblyResolver : IAssemblyResolver {

		static readonly bool on_mono = Type.GetType ("Mono.Runtime") != null;

		readonly Collection<string> directories;
#if !SILVERLIGHT
		Collection<string> mono_gac_paths;
#endif

		public void AddSearchDirectory (string directory)
		{
			directories.Add (directory);
		}

		public void RemoveSearchDirectory (string directory)
		{
			directories.Remove (directory);
		}

		public string [] GetSearchDirectories ()
		{
			var directories = new string [this.directories.size];
			Array.Copy (this.directories.items, directories, directories.Length);
			return directories;
		}

		public virtual AssemblyDefinition Resolve (string fullName)
		{
			return Resolve (AssemblyNameReference.Parse (fullName));
		}

		protected BaseAssemblyResolver ()
		{
			directories = new Collection<string> { ".", "bin" };
		}

		AssemblyDefinition GetAssembly (string file)
		{
			return ModuleDefinition.ReadModule (file, new ReaderParameters { AssemblyResolver = this}).Assembly;
		}

		public virtual AssemblyDefinition Resolve (AssemblyNameReference name)
		{
			var assembly = SearchDirectory (name, directories);
			if (assembly != null)
				return assembly;

#if !SILVERLIGHT
			var framework_dir = Path.GetDirectoryName (typeof (object).Module.FullyQualifiedName);

			if (IsZero (name.Version)) {
				assembly = SearchDirectory (name, new [] { framework_dir });
				if (assembly != null)
					return assembly;
			}

			if (name.Name == "mscorlib") {
				assembly = GetCorlib (name);
				if (assembly != null)
					return assembly;
			}

			assembly = GetAssemblyInGac (name);
			if (assembly != null)
				return assembly;

			assembly = SearchDirectory (name, new [] { framework_dir });
			if (assembly != null)
				return assembly;
#endif

			throw new FileNotFoundException ("Could not resolve: " + name);
		}

		AssemblyDefinition SearchDirectory (AssemblyNameReference name, IEnumerable<string> directories)
		{
			var extensions = new [] { ".exe", ".dll" };
			foreach (var directory in directories) {
				foreach (var extension in extensions) {
					string file = Path.Combine (directory, name.Name + extension);
					if (File.Exists (file))
						return GetAssembly (file);
				}
			}

			return null;
		}

		static bool IsZero (Version version)
		{
			return version.Major == 0 && version.Minor == 0 && version.Build == 0 && version.Revision == 0;
		}

#if !SILVERLIGHT
		AssemblyDefinition GetCorlib (AssemblyNameReference reference)
		{
			var corlib = typeof (object).Assembly.GetName ();
			if (corlib.Version == reference.Version || IsZero (reference.Version))
				return GetAssembly (typeof (object).Module.FullyQualifiedName);

			var path = Directory.GetParent (
				Directory.GetParent (
					typeof (object).Module.FullyQualifiedName).FullName
				).FullName;

			if (on_mono) {
				if (reference.Version.Major == 1)
					path = Path.Combine (path, "1.0");
				else if (reference.Version.Major == 2) {
					if (reference.Version.Minor == 1)
						path = Path.Combine (path, "2.1");
					else
						path = Path.Combine (path, "2.0");
				} else if (reference.Version.Major == 4)
					path = Path.Combine (path, "4.0");
				else
					throw new NotSupportedException ("Version not supported: " + reference.Version);
			} else {
				if (reference.Version.ToString () == "1.0.3300.0")
					path = Path.Combine (path, "v1.0.3705");
				else if (reference.Version.ToString () == "1.0.5000.0")
					path = Path.Combine (path, "v1.1.4322");
				else if (reference.Version.ToString () == "2.0.0.0")
					path = Path.Combine (path, "v2.0.50727");
				else if (reference.Version.ToString () == "4.0.0.0")
					path = Path.Combine (path, "v4.0.30319");
				else
					throw new NotSupportedException ("Version not supported: " + reference.Version);
			}

			var file = Path.Combine (path, "mscorlib.dll");
			if (File.Exists (file))
				return GetAssembly (file);

			return null;
		}

		static Collection<string> GetDefaultMonoGacPaths ()
		{
			var paths = new Collection<string> ();
			string gac = GetCurrentGacPath ();
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

		IEnumerable<string> GetMonoGacPaths ()
		{
			if (mono_gac_paths == null)
				mono_gac_paths = GetDefaultMonoGacPaths ();

			return mono_gac_paths;
		}

		AssemblyDefinition GetAssemblyInGac (AssemblyNameReference reference)
		{
			if (reference.PublicKeyToken == null || reference.PublicKeyToken.Length == 0)
				return null;

			if (on_mono) {
				foreach (var gacpath in GetMonoGacPaths ()) {
					var file = GetAssemblyFile (reference, gacpath);
					if (File.Exists (file))
						return GetAssembly (file);
				}
			} else {
				var current_gac = GetCurrentGacPath ();
				if (current_gac == null)
					return null;

				var gacs = new [] {"GAC_MSIL", "GAC_32", "GAC"};
				for (int i = 0; i < gacs.Length; i++) {
					var gac = Path.Combine (Directory.GetParent (current_gac).FullName, gacs [i]);
					var file = GetAssemblyFile (reference, gac);
					if (Directory.Exists (gac) && File.Exists (file))
						return GetAssembly (file);
				}
			}

			return null;
		}

		static string GetAssemblyFile (AssemblyNameReference reference, string gac)
		{
			var gac_folder = new StringBuilder ();
			gac_folder.Append (reference.Version);
			gac_folder.Append ("__");
			for (int i = 0; i < reference.PublicKeyToken.Length; i++)
				gac_folder.Append (reference.PublicKeyToken [i].ToString ("x2"));

			return Path.Combine (
				Path.Combine (
					Path.Combine (gac, reference.Name), gac_folder.ToString ()),
				reference.Name + ".dll");
		}

		static string GetCurrentGacPath ()
		{
			var file = typeof (Uri).Module.FullyQualifiedName;
			if (!File.Exists (file))
				return null;

			return Directory.GetParent (
				Directory.GetParent (
					Path.GetDirectoryName (
						file)
					).FullName
				).FullName;
		}
#endif
	}
}
