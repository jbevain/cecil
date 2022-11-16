#if !NET_CORE

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace Mono.Cecil.Tests {
	public class WindowsRuntimeAssemblyResolver : DefaultAssemblyResolver {

		readonly Dictionary<string, AssemblyDefinition> assemblies = new Dictionary<string, AssemblyDefinition> ();

		public static WindowsRuntimeAssemblyResolver CreateInstance (bool applyWindowsRuntimeProjections)
		{
			if (Platform.OnMono)
				return null;
			try {
				return new WindowsRuntimeAssemblyResolver (applyWindowsRuntimeProjections);
			} catch {
				return null;
			}
		}

		public override AssemblyDefinition Resolve (AssemblyNameReference name)
		{
			AssemblyDefinition assembly;
			if (assemblies.TryGetValue(name.Name, out assembly))
				return assembly;

			return base.Resolve (name);
		}

		private WindowsRuntimeAssemblyResolver (bool applyWindowsRuntimeProjections)
		{
			var readerParameters = new ReaderParameters {
				ApplyWindowsRuntimeProjections = applyWindowsRuntimeProjections
			};

			LoadWindowsSdk ("v10.0", "10", (installationFolder) => {
				var referencesFolder = Path.Combine (installationFolder, "References");
				var assemblies = Directory.GetFiles (referencesFolder, "*.winmd", SearchOption.AllDirectories);
				
				var latestVersionDir = Directory.GetDirectories (Path.Combine (installationFolder, "UnionMetadata"))
					.Where (d => Path.GetFileName (d) != "Facade")
					.OrderBy (d => Path.GetFileName (d))
					.Last ();

				var windowsWinMdPath = Path.Combine (latestVersionDir, "Windows.winmd");
				if (!File.Exists (windowsWinMdPath))
					throw new FileNotFoundException (windowsWinMdPath);

				var windowsWinmdAssembly = AssemblyDefinition.ReadAssembly (windowsWinMdPath, readerParameters);
				Register (windowsWinmdAssembly);

				foreach (var assemblyPath in assemblies) {
					var assembly = AssemblyDefinition.ReadAssembly (assemblyPath, readerParameters);
					Register (assembly);
				}
			});
		}

		void Register (AssemblyDefinition assembly)
		{
			assemblies [assembly.Name.Name] = assembly;
			RegisterAssembly (assembly);
		}

		protected override void Dispose (bool disposing)
		{
			if (!disposing)
				return;

			foreach (var assembly in assemblies.Values)
				assembly.Dispose ();

			base.Dispose (true);
		}

		void LoadWindowsSdk (string registryVersion, string windowsKitsVersion, Action<string> registerAssembliesCallback)
		{
			using (var localMachine32Key = RegistryKey.OpenBaseKey (RegistryHive.LocalMachine, RegistryView.Registry32)) {
				using (var sdkKey = localMachine32Key.OpenSubKey (@"SOFTWARE\Microsoft\Microsoft SDKs\Windows\v" + registryVersion)) {
					string installationFolder = null;
					if (sdkKey != null)
						installationFolder = (string)sdkKey.GetValue ("InstallationFolder");
					if (string.IsNullOrEmpty (installationFolder)) {
						var programFilesX86 = Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86);
						installationFolder = Path.Combine (programFilesX86, @"Windows Kits\" + windowsKitsVersion);
					}
					registerAssembliesCallback (installationFolder);
				}
			}
		}
	}
}
#endif