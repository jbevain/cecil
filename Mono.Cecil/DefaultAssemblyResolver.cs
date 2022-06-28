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
using System.Linq;

namespace Mono.Cecil {

	public class DefaultAssemblyResolverProvider : IAssemblyResolverProvider {
		public IAssemblyResolver Create (AssemblyDefinition assembly)
		{
			return new DefaultAssemblyResolver ();
		}
	}

	public class DefaultAssemblyResolver : BaseAssemblyResolver {
		private readonly IDictionary<string, AssemblyDefinition> cache;

		public DefaultAssemblyResolver ()
		{
			cache = new Dictionary<string, AssemblyDefinition> (StringComparer.Ordinal);
		}

		public DefaultAssemblyResolver (bool asNetCore, bool asMono, System.Reflection.Module core) : this ()
		{
			base.NetCore = asNetCore;
			base.AsMono = asMono;
			base.CoreModule = core;
		}

		public override AssemblyDefinition Resolve (AssemblyNameReference name)
		{
			Mixin.CheckName (name);

			AssemblyDefinition assembly;
			if (cache.TryGetValue (name.FullName, out assembly))
				return assembly;

			assembly = base.Resolve (name);
			cache [name.FullName] = assembly;

			return assembly;
		}

		protected void RegisterAssembly (AssemblyDefinition assembly)
		{
			if (assembly == null)
				throw new ArgumentNullException ("assembly");

			var name = assembly.Name.FullName;
			if (cache.ContainsKey (name))
				return;

			cache [name] = assembly;
		}

		protected override void Dispose (bool disposing)
		{
			foreach (var assembly in cache.Values)
				assembly.Dispose ();

			cache.Clear ();

			base.Dispose (disposing);
		}
	}

	public class AdaptiveAssemblyResolverProvider : IAssemblyResolverProvider {
		public IAssemblyResolver Create (AssemblyDefinition assembly)
		{
			var blob = assembly.CustomAttributes
				.Where (a => a.AttributeType.FullName.Equals ("System.Runtime.Versioning.TargetFrameworkAttribute", StringComparison.Ordinal))
				.FirstOrDefault ()?.GetBlob ();
			var core = !System.Text.Encoding.ASCII.GetString ((blob ?? new byte [3]).Skip (3).ToArray ()).StartsWith (".NETFramework");
			return new AdaptiveAssemblyResolver (core);
		}
	}

	public class AdaptiveAssemblyResolver : DefaultAssemblyResolver {
		public AdaptiveAssemblyResolver (bool core) :
			base (core, false, typeof (object).Module) // harmless by experiment
		{
		}

		protected override AssemblyDefinition LastChanceResolution (AssemblyDefinition assembly, AssemblyNameReference name, ReaderParameters parameters)
		{
			if (!base.NetCore) {
				try // the mono gac
				{
					base.AsMono = true;
					assembly = base.SearchFrameworkAssemblies (name, parameters);
					if (assembly != null)
						return assembly;
				}
				finally { base.AsMono = false; }
			}

			return base.LastChanceResolution (assembly, name, parameters);
		}
	}
}