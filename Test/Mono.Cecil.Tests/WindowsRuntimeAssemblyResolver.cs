using System;
using System.IO;
using Microsoft.Win32;

namespace Mono.Cecil.Tests {
	public class WindowsRuntimeAssemblyResolver : DefaultAssemblyResolver {
		public static WindowsRuntimeAssemblyResolver CreateInstance ()
		{
			if (Platform.OnMono)
				return null;
			try {
				return new WindowsRuntimeAssemblyResolver ();
			} catch {
				return null;
			}
		}

		private WindowsRuntimeAssemblyResolver ()
		{
			LoadWindowsSdk ("v8.1", "8.1", (installationFolder) => {
				var fileName = Path.Combine (installationFolder, @"References\CommonConfiguration\Neutral\Windows.winmd");
				var assembly = AssemblyDefinition.ReadAssembly (fileName);
				RegisterAssembly (assembly);
			});

			LoadWindowsSdk ("v10.0", "10", (installationFolder) => {
				var referencesFolder = Path.Combine (installationFolder, "References");
				var assemblies = Directory.GetFiles (referencesFolder, "*.winmd", SearchOption.AllDirectories);

				foreach (var assemblyPath in assemblies) {
					var assembly = AssemblyDefinition.ReadAssembly (assemblyPath);
					RegisterAssembly (assembly);
				}
			});
		}

		private void LoadWindowsSdk (string registryVersion, string windowsKitsVersion, Action<string> registerAssembliesCallback)
		{
#if NET_4_0
			using (var localMachine32Key = RegistryKey.OpenBaseKey (RegistryHive.LocalMachine, RegistryView.Registry32)) {
				using (var sdkKey = localMachine32Key.OpenSubKey (@"SOFTWARE\Microsoft\Microsoft SDKs\Windows\v" + registryVersion)) {
#else
			{
				// this will fail on 64-bit process as there's no way (other than pinoke) to read from 32-bit registry view
				using (var sdkKey = Registry.LocalMachine.OpenSubKey (@"SOFTWARE\Microsoft\Microsoft SDKs\Windows\" + version)) {
#endif
					string installationFolder = null;
					if (sdkKey != null)
						installationFolder = (string)sdkKey.GetValue ("InstallationFolder");
					if (string.IsNullOrEmpty (installationFolder)) {
#if NET_4_0
						var programFilesX86 = Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86);
#else
						var programFilesX86 = Environment.GetEnvironmentVariable ("ProgramFiles(x86)");
#endif
						installationFolder = Path.Combine (programFilesX86, @"Windows Kits\" + windowsKitsVersion);
					}
					registerAssembliesCallback (installationFolder);
				}
			}
		}
	}
}
