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
using System.IO;

using Mono.Cecil.Cil;

namespace Mono.Cecil.Pdb {

	public sealed class NativePdbReaderProvider : ISymbolReaderProvider {

		public ISymbolReader GetSymbolReader (ModuleDefinition module, string fileName)
		{
			Mixin.CheckModule (module);
			Mixin.CheckFileName (fileName);

			return new PdbReader (Disposable.Owned (File.OpenRead (Mixin.GetPdbFileName (fileName)) as Stream));
		}

		public ISymbolReader GetSymbolReader (ModuleDefinition module, Stream symbolStream)
		{
			Mixin.CheckModule (module);
			Mixin.CheckStream (symbolStream);

			return new PdbReader (Disposable.NotOwned (symbolStream));
		}
	}

	public sealed class PdbReaderProvider : ISymbolReaderProvider {

		public ISymbolReader GetSymbolReader (ModuleDefinition module, string fileName)
		{
			Mixin.CheckModule (module);
			Mixin.CheckFileName (fileName);

			return IsPortablePdb (Mixin.GetPdbFileName (fileName))
				? new PortablePdbReaderProvider ().GetSymbolReader (module, fileName)
				: new NativePdbReaderProvider ().GetSymbolReader (module, fileName);
		}

		public ISymbolReader GetSymbolReader (ModuleDefinition module, Stream symbolStream)
		{
			Mixin.CheckModule (module);
			Mixin.CheckStream (symbolStream);
			Mixin.CheckReadSeek (symbolStream);

			return IsPortablePdb (symbolStream)
				? new PortablePdbReaderProvider ().GetSymbolReader (module, symbolStream)
				: new NativePdbReaderProvider ().GetSymbolReader (module, symbolStream);
		}

		static bool IsPortablePdb (string fileName)
		{
			using (var file = new FileStream (fileName, FileMode.Open, FileAccess.Read, FileShare.None))
				return IsPortablePdb (file);
		}

		static bool IsPortablePdb (Stream stream)
		{
			const uint ppdb_signature = 0x424a5342;

			var position = stream.Position;
			try {
				var reader = new BinaryReader (stream);
				return reader.ReadUInt32 () == ppdb_signature;
			} finally {
				stream.Position = position;
			}
		}
	}

#if !READ_ONLY

	public sealed class NativePdbWriterProvider : ISymbolWriterProvider {

		public ISymbolWriter GetSymbolWriter (ModuleDefinition module, string fileName)
		{
			Mixin.CheckModule (module);
			Mixin.CheckFileName (fileName);

			return new PdbWriter (module, CreateWriter (module, Mixin.GetPdbFileName (fileName)));
		}

		static SymWriter CreateWriter (ModuleDefinition module, string pdb)
		{
			var writer = new SymWriter ();

			if (File.Exists (pdb))
				File.Delete (pdb);

			writer.Initialize (new ModuleMetadata (module), pdb, true);

			return writer;
		}

		public ISymbolWriter GetSymbolWriter (ModuleDefinition module, Stream symbolStream)
		{
			throw new NotImplementedException ();
		}
	}

	public sealed class PdbWriterProvider : ISymbolWriterProvider {

		public ISymbolWriter GetSymbolWriter (ModuleDefinition module, string fileName)
		{
			Mixin.CheckModule (module);
			Mixin.CheckFileName (fileName);

			if (HasPortablePdbSymbols (module))
				return new PortablePdbWriterProvider ().GetSymbolWriter (module, fileName);

			return new NativePdbWriterProvider ().GetSymbolWriter (module, fileName);
		}

		static bool HasPortablePdbSymbols (ModuleDefinition module)
		{
			return module.symbol_reader != null && module.symbol_reader is PortablePdbReader;
		}

		public ISymbolWriter GetSymbolWriter (ModuleDefinition module, Stream symbolStream)
		{
			Mixin.CheckModule (module);
			Mixin.CheckStream (symbolStream);
			Mixin.CheckReadSeek (symbolStream);

			if (HasPortablePdbSymbols (module))
				return new PortablePdbWriterProvider ().GetSymbolWriter (module, symbolStream);

			return new NativePdbWriterProvider ().GetSymbolWriter (module, symbolStream);
		}
	}

#endif
}
