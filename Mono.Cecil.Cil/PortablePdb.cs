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

using Mono.Cecil.Metadata;
using Mono.Cecil.PE;

namespace Mono.Cecil.Cil {

	public sealed class PortablePdbReaderProvider : ISymbolReaderProvider {

#if !PCL
		public ISymbolReader GetSymbolReader (ModuleDefinition module, string fileName)
		{
			Mixin.CheckModule (module);
			Mixin.CheckFileName (fileName);

			var file = File.OpenRead (Mixin.GetPdbFileName (fileName));
			return GetSymbolReader (module, Disposable.Owned (file as Stream), file.Name);
		}
#endif

		public ISymbolReader GetSymbolReader (ModuleDefinition module, Stream symbolStream)
		{
			Mixin.CheckModule (module);
			Mixin.CheckStream (symbolStream);

			return GetSymbolReader (module, Disposable.NotOwned (symbolStream), "");
		}

		ISymbolReader GetSymbolReader (ModuleDefinition module, Disposable<Stream> symbolStream, string fileName)
		{
			return new PortablePdbReader (ImageReader.ReadPortablePdb (symbolStream, fileName), module);
		}
	}

	public sealed class PortablePdbReader : ISymbolReader {

		readonly Image image;
		readonly ModuleDefinition module;
		readonly MetadataReader reader;
		readonly MetadataReader debug_reader;

		bool IsEmbedded { get { return reader.image == debug_reader.image; } }

		internal PortablePdbReader (Image image, ModuleDefinition module)
		{
			this.image = image;
			this.module = module;
			this.reader = module.reader;
			this.debug_reader = new MetadataReader (image, module, this.reader);
		}

		public bool ProcessDebugHeader (ImageDebugDirectory directory, byte [] header)
		{
			if (image == module.Image)
				return true;

			if (header.Length < 24)
				return false;

			var magic = ReadInt32 (header, 0);
			if (magic != 0x53445352)
				return false;

			var buffer = new byte [16];
			Buffer.BlockCopy (header, 4, buffer, 0, 16);

			var module_guid = new Guid (buffer);

			Buffer.BlockCopy (image.PdbHeap.Id, 0, buffer, 0, 16);

			var pdb_guid = new Guid (buffer);

			return module_guid == pdb_guid;
		}

		static int ReadInt32 (byte [] bytes, int start)
		{
			return (bytes [start]
				| (bytes [start + 1] << 8)
				| (bytes [start + 2] << 16)
				| (bytes [start + 3] << 24));
		}

		public MethodDebugInformation Read (MethodDefinition method)
		{
			var info = new MethodDebugInformation (method);
			ReadSequencePoints (info);
			ReadScope (info);
			ReadStateMachineKickOffMethod (info);
			ReadCustomDebugInformations (info);
			return info;
		}

		void ReadSequencePoints (MethodDebugInformation method_info)
		{
			method_info.sequence_points = debug_reader.ReadSequencePoints (method_info.method);
		}

		void ReadScope (MethodDebugInformation method_info)
		{
			method_info.scope = debug_reader.ReadScope (method_info.method);
		}

		void ReadStateMachineKickOffMethod (MethodDebugInformation method_info)
		{
			method_info.kickoff_method = debug_reader.ReadStateMachineKickoffMethod (method_info.method);
		}

		void ReadCustomDebugInformations (MethodDebugInformation info)
		{
			info.method.custom_infos = debug_reader.GetCustomDebugInformation (info.method);
		}

		public void Dispose ()
		{
			if (IsEmbedded)
				return;

			image.Dispose ();
		}
	}

#if !READ_ONLY

	public sealed class PortablePdbWriterProvider : ISymbolWriterProvider
	{
#if !PCL
		public ISymbolWriter GetSymbolWriter (ModuleDefinition module, string fileName)
		{
			Mixin.CheckModule (module);
			Mixin.CheckFileName (fileName);

			var file = File.OpenWrite (Mixin.GetPdbFileName (fileName));
			return GetSymbolWriter (module, Disposable.Owned (file as Stream));
		}
#endif

		public ISymbolWriter GetSymbolWriter (ModuleDefinition module, Stream symbolStream)
		{
			Mixin.CheckModule (module);
			Mixin.CheckStream (symbolStream);

			return GetSymbolWriter (module, Disposable.NotOwned (symbolStream));
		}

		ISymbolWriter GetSymbolWriter (ModuleDefinition module, Disposable<Stream> stream)
		{
			var metadata = new MetadataBuilder (module, this);
			var writer = ImageWriter.CreateDebugWriter (module, metadata, stream);

			return new PortablePdbWriter (metadata, module, writer);
		}
	}

	sealed class PortablePdbWriter : ISymbolWriter {

		readonly MetadataBuilder pdb_metadata;
		readonly ModuleDefinition module;
		readonly ImageWriter writer;

		MetadataBuilder module_metadata;

		bool IsEmbedded { get { return writer == null; } }

		public PortablePdbWriter (MetadataBuilder pdb_metadata, ModuleDefinition module)
		{
			this.pdb_metadata = pdb_metadata;
			this.module = module;
		}

		public PortablePdbWriter (MetadataBuilder pdb_metadata, ModuleDefinition module, ImageWriter writer)
			: this (pdb_metadata, module)
		{
			this.writer = writer;
		}

		public void SetModuleMetadata (MetadataBuilder metadata)
		{
			this.module_metadata = metadata;

			if (module_metadata != pdb_metadata)
				this.pdb_metadata.metadata_builder = metadata;
		}

		public bool GetDebugHeader (out ImageDebugDirectory directory, out byte [] header)
		{
			if (IsEmbedded) {
				directory = new ImageDebugDirectory ();
				header = Empty<byte>.Array;
				return false;
			}

			directory = new ImageDebugDirectory () {
				MajorVersion = 256,
				MinorVersion = 20577,
				Type = 2,
			};

			var buffer = new ByteBuffer ();
			// RSDS
			buffer.WriteUInt32 (0x53445352);
			// Module ID
			buffer.WriteBytes (module.Mvid.ToByteArray ());
			// PDB Age
			buffer.WriteUInt32 (1);
			// PDB Path
			buffer.WriteBytes (System.Text.Encoding.UTF8.GetBytes (writer.BaseStream.GetFileName ()));
			buffer.WriteByte (0);

			header = new byte [buffer.length];
			Buffer.BlockCopy (buffer.buffer, 0, header, 0, buffer.length);
			directory.SizeOfData = header.Length;
			return true;
		}

		public void Write (MethodDebugInformation info)
		{
			CheckMethodDebugInformationTable ();

			pdb_metadata.AddMethodDebugInformation (info);
		}

		void CheckMethodDebugInformationTable ()
		{
			var mdi = pdb_metadata.table_heap.GetTable<MethodDebugInformationTable> (Table.MethodDebugInformation);
			if (mdi.length > 0)
				return;

			// The MethodDebugInformation table has the same length as the Method table
			mdi.rows = new Row<uint, uint> [module_metadata.method_rid - 1];
			mdi.length = mdi.rows.Length;
		}

		public void Dispose ()
		{
			if (IsEmbedded)
				return;

			WritePdbHeap ();
			WriteTableHeap ();

			writer.BuildMetadataTextMap ();
			writer.WriteMetadataHeader ();
			writer.WriteMetadata ();

			writer.stream.Dispose ();
		}

		void WritePdbHeap ()
		{
			var pdb_heap = pdb_metadata.pdb_heap;

			pdb_heap.WriteBytes (module.Mvid.ToByteArray ());
			pdb_heap.WriteUInt32 (module_metadata.time_stamp);

			pdb_heap.WriteUInt32 (module_metadata.entry_point.ToUInt32 ());

			var table_heap = module_metadata.table_heap;
			var tables = table_heap.tables;

			ulong valid = 0;
			for (int i = 0; i < tables.Length; i++) {
				if (tables [i] == null || tables [i].Length == 0)
					continue;

				valid |= (1UL << i);
			}

			pdb_heap.WriteUInt64 (valid);

			for (int i = 0; i < tables.Length; i++) {
				if (tables [i] == null || tables [i].Length == 0)
					continue;

				pdb_heap.WriteUInt32 ((uint) tables [i].Length);
			}
		}

		void WriteTableHeap ()
		{
			pdb_metadata.table_heap.WriteTableHeap ();
		}
	}

#endif

	static class PdbGuidMapping {

		static readonly Dictionary<Guid, DocumentLanguage> guid_language = new Dictionary<Guid, DocumentLanguage> ();
		static readonly Dictionary<DocumentLanguage, Guid> language_guid = new Dictionary<DocumentLanguage, Guid> ();

		static PdbGuidMapping ()
		{
			AddMapping (DocumentLanguage.C, new Guid ("63a08714-fc37-11d2-904c-00c04fa302a1"));
			AddMapping (DocumentLanguage.Cpp, new Guid ("3a12d0b7-c26c-11d0-b442-00a0244a1dd2"));
			AddMapping (DocumentLanguage.CSharp, new Guid ("3f5162f8-07c6-11d3-9053-00c04fa302a1"));
			AddMapping (DocumentLanguage.Basic, new Guid ("3a12d0b8-c26c-11d0-b442-00a0244a1dd2"));
			AddMapping (DocumentLanguage.Java, new Guid ("3a12d0b4-c26c-11d0-b442-00a0244a1dd2"));
			AddMapping (DocumentLanguage.Cobol, new Guid ("af046cd1-d0e1-11d2-977c-00a0c9b4d50c"));
			AddMapping (DocumentLanguage.Pascal, new Guid ("af046cd2-d0e1-11d2-977c-00a0c9b4d50c"));
			AddMapping (DocumentLanguage.Cil, new Guid ("af046cd3-d0e1-11d2-977c-00a0c9b4d50c"));
			AddMapping (DocumentLanguage.JScript, new Guid ("3a12d0b6-c26c-11d0-b442-00a0244a1dd2"));
			AddMapping (DocumentLanguage.Smc, new Guid ("0d9b9f7b-6611-11d3-bd2a-0000f80849bd"));
			AddMapping (DocumentLanguage.MCpp, new Guid ("4b35fde8-07c6-11d3-9053-00c04fa302a1"));
			AddMapping (DocumentLanguage.FSharp, new Guid ("ab4f38c9-b6e6-43ba-be3b-58080b2ccce3"));
		}

		static void AddMapping (DocumentLanguage language, Guid guid)
		{
			guid_language.Add (guid, language);
			language_guid.Add (language, guid);
		}

		static readonly Guid type_text = new Guid ("5a869d0b-6611-11d3-bd2a-0000f80849bd");

		public static DocumentType ToType (this Guid guid)
		{
			if (guid == type_text)
				return DocumentType.Text;

			return DocumentType.Other;
		}

		public static Guid ToGuid (this DocumentType type)
		{
			if (type == DocumentType.Text)
				return type_text;

			return new Guid ();
		}

		static readonly Guid hash_md5 = new Guid ("406ea660-64cf-4c82-b6f0-42d48172a799");
		static readonly Guid hash_sha1 = new Guid ("ff1816ec-aa5e-4d10-87f7-6f4963833460");
		static readonly Guid hash_sha256 = new Guid ("8829d00f-11b8-4213-878b-770e8597ac16");

		public static DocumentHashAlgorithm ToHashAlgorithm (this Guid guid)
		{
			if (guid == hash_md5)
				return DocumentHashAlgorithm.MD5;

			if (guid == hash_sha1)
				return DocumentHashAlgorithm.SHA1;

			if (guid == hash_sha256)
				return DocumentHashAlgorithm.SHA256;

			return DocumentHashAlgorithm.None;
		}

		public static Guid ToGuid (this DocumentHashAlgorithm hash_algo)
		{
			if (hash_algo == DocumentHashAlgorithm.MD5)
				return hash_md5;

			if (hash_algo == DocumentHashAlgorithm.SHA1)
				return hash_sha1;

			return new Guid ();
		}

		public static DocumentLanguage ToLanguage (this Guid guid)
		{
			DocumentLanguage language;
			if (!guid_language.TryGetValue (guid, out language))
				return DocumentLanguage.Other;

			return language;
		}

		public static Guid ToGuid (this DocumentLanguage language)
		{
			Guid guid;
			if (!language_guid.TryGetValue (language, out guid))
				return new Guid ();

			return guid;
		}

		static readonly Guid vendor_ms = new Guid ("994b45c4-e6e9-11d2-903f-00c04fa302a1");

		public static DocumentLanguageVendor ToVendor (this Guid guid)
		{
			if (guid == vendor_ms)
				return DocumentLanguageVendor.Microsoft;

			return DocumentLanguageVendor.Other;
		}

		public static Guid ToGuid (this DocumentLanguageVendor vendor)
		{
			if (vendor == DocumentLanguageVendor.Microsoft)
				return vendor_ms;

			return new Guid ();
		}
	}
}
