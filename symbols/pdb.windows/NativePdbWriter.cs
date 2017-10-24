// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

#if !READ_ONLY

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Mono.Cecil.Cil;
using Mono.Cecil.PE;
using Mono.Collections.Generic;

using Microsoft.DiaSymReader;

namespace Mono.Cecil.WindowsPdb {

	public class NativePdbWriter : ISymbolWriter, IMetadataSymbolWriter {
		const int Age = 1;

		readonly ModuleDefinition module;
		readonly SymUnmanagedWriter writer;
		readonly SymUnmanagedSequencePointsWriter sequence_point_writer;
		readonly string pdb_path;
		readonly Dictionary<string, int> documents;
		readonly Dictionary<ImportDebugInformation, MetadataToken> import_info_to_parent;
		readonly Guid id;
		readonly uint timestamp;

		MetadataBuilder metadata;

		internal NativePdbWriter (ModuleDefinition module, SymUnmanagedWriter writer, string pdb_path)
		{
			this.module = module;
			this.writer = writer;
			this.sequence_point_writer = new SymUnmanagedSequencePointsWriter (writer, capacity: 64);
			this.pdb_path = pdb_path;
			this.documents = new Dictionary<string, int> ();
			this.import_info_to_parent = new Dictionary<ImportDebugInformation, MetadataToken> ();

			// TODO: calculate id based on the content to produce deterministic output
			this.id = Guid.NewGuid ();
			this.timestamp = module.timestamp;
		}

		public ISymbolReaderProvider GetReaderProvider ()
		{
			return new NativePdbReaderProvider ();
		}

		public ImageDebugHeader GetDebugHeader ()
		{
			var data = Mixin.GetCodeViewData (id, pdb_path, Age);

			var directory = new ImageDebugDirectory () {
				MajorVersion = 0,
				MinorVersion = 0,
				Type = ImageDebugType.CodeView,
				TimeDateStamp = (int)timestamp,
				SizeOfData = data.Length,
			};

			return new ImageDebugHeader (new ImageDebugHeaderEntry (directory, data));
		}

		public void Write (MethodDebugInformation info)
		{
			var method_token = info.method.MetadataToken;
			var sym_token = method_token.ToInt32 ();

			if (!info.HasSequencePoints && info.scope == null && !info.HasCustomDebugInformations && info.StateMachineKickOffMethod == null)
				return;

			writer.OpenMethod (sym_token);

			if (!info.sequence_points.IsNullOrEmpty ())
				DefineSequencePoints (info.sequence_points);

			var import_parent = new MetadataToken ();

			if (info.scope != null)
				DefineScope (info.scope, info, out import_parent);

			DefineCustomMetadata (info, import_parent);

			writer.CloseMethod ();
		}

		void IMetadataSymbolWriter.SetMetadata (MetadataBuilder metadata)
		{
			this.metadata = metadata;
		}

		void IMetadataSymbolWriter.WriteModule ()
		{
		}

		void DefineCustomMetadata (MethodDebugInformation info, MetadataToken import_parent)
		{
			var metadata = new CustomMetadataWriter (this.writer);

			if (import_parent.RID != 0) {
				metadata.WriteForwardInfo (import_parent);
			} else if (info.scope != null && info.scope.Import != null && info.scope.Import.HasTargets) {
				metadata.WriteUsingInfo (info.scope.Import);
			}

			if (info.Method.HasCustomAttributes) {
				foreach (var attribute in info.Method.CustomAttributes) {
					const string compiler_services = "System.Runtime.CompilerServices";
					var attribute_type = attribute.AttributeType;

					if (!attribute_type.IsTypeOf (compiler_services, "IteratorStateMachineAttribute") && !attribute_type.IsTypeOf (compiler_services, "AsyncStateMachineAttribute"))
						continue;

					var type = attribute.ConstructorArguments [0].Value as TypeReference;
					if (type == null)
						continue;

					metadata.WriteForwardIterator (type);
				}
			}

			if (info.HasCustomDebugInformations) {
				var scopes = info.CustomDebugInformations.OfType<StateMachineScopeDebugInformation> ().ToArray ();

				if (scopes.Length > 0)
					metadata.WriteIteratorScopes (scopes, info);
			}

			metadata.WriteCustomMetadata ();

			DefineAsyncCustomMetadata (info);
		}

		void DefineAsyncCustomMetadata (MethodDebugInformation info)
		{
			if (!info.HasCustomDebugInformations)
				return;

			foreach (var custom_info in info.CustomDebugInformations) {
				var async_debug_info = custom_info as AsyncMethodBodyDebugInformation;
				if (async_debug_info == null)
					continue;

				int offsetCount = async_debug_info.Resumes.Count;

				int[] yieldOffsets = new int[offsetCount];
				int[] resumeOffsets = new int[offsetCount];
				for (int i = 0; i < offsetCount; i++) {
					yieldOffsets[i] = async_debug_info.Yields[i].Offset;
					resumeOffsets[i] = async_debug_info.Resumes[i].Offset;
				}

				writer.SetAsyncInfo (
					moveNextMethodToken: async_debug_info.MoveNextMethod.MetadataToken.ToInt32 (),
					kickoffMethodToken: info.StateMachineKickOffMethod.MetadataToken.ToInt32 (),
					catchHandlerOffset: async_debug_info.CatchHandler.Offset,
					yieldOffsets: yieldOffsets,
					resumeOffsets: resumeOffsets);
			}
		}

		void DefineScope (ScopeDebugInformation scope, MethodDebugInformation info, out MetadataToken import_parent)
		{
			var start_offset = scope.Start.Offset;
			var end_offset = scope.End.IsEndOfMethod
				? info.code_size
				: scope.End.Offset;

			import_parent = new MetadataToken (0u);

			writer.OpenScope (start_offset);

			if (scope.Import != null && scope.Import.HasTargets && !import_info_to_parent.TryGetValue (info.scope.Import, out import_parent)) {
				foreach (var target in scope.Import.Targets) {
					switch (target.Kind) {
					case ImportTargetKind.ImportNamespace:
						writer.UsingNamespace ("U" + target.@namespace);
						break;
					case ImportTargetKind.ImportType:
						writer.UsingNamespace ("T" + TypeParser.ToParseable (target.type));
						break;
					case ImportTargetKind.DefineNamespaceAlias:
						writer.UsingNamespace ("A" + target.Alias + " U" + target.@namespace);
						break;
					case ImportTargetKind.DefineTypeAlias:
						writer.UsingNamespace ("A" + target.Alias + " T" + TypeParser.ToParseable (target.type));
						break;
					}
				}

				import_info_to_parent.Add (info.scope.Import, info.method.MetadataToken);
			}

			var sym_token = info.local_var_token.ToInt32 ();

			if (!scope.variables.IsNullOrEmpty ()) {
				for (int i = 0; i < scope.variables.Count; i++) {
					var variable = scope.variables [i];
					DefineLocalVariable (variable, sym_token);
				}
			}

			if (!scope.constants.IsNullOrEmpty ()) {
				for (int i = 0; i < scope.constants.Count; i++) {
					var constant = scope.constants [i];
					DefineConstant (constant);
				}
			}

			if (!scope.scopes.IsNullOrEmpty ()) {
				for (int i = 0; i < scope.scopes.Count; i++) {
					MetadataToken _;
					DefineScope (scope.scopes [i], info, out _);
				}
			}

			writer.CloseScope (end_offset);
		}

		void DefineSequencePoints (Collection<SequencePoint> sequence_points)
		{
			int current_doc_index = -1;
			Document current_doc = null;

			for (int i = 0; i < sequence_points.Count; i++) {
				var sequence_point = sequence_points[i];

				if (!ReferenceEquals(current_doc, sequence_point.Document)) {
					current_doc_index = GetDocumentIndex (sequence_point.Document);
					current_doc = sequence_point.Document;
				}

				sequence_point_writer.Add (
					current_doc_index,
					sequence_point.Offset,
					sequence_point.StartLine,
					sequence_point.StartColumn,
					sequence_point.EndLine,
					sequence_point.EndColumn);
			}

			sequence_point_writer.Flush ();
		}

		void DefineLocalVariable (VariableDebugInformation variable, int local_var_token)
		{
			writer.DefineLocalVariable (
				variable.Index,
				variable.Name,
				(int)variable.Attributes,
				local_var_token);
		}

		void DefineConstant (ConstantDebugInformation constant)
		{
			var row = metadata.AddStandAloneSignature (metadata.GetConstantTypeBlobIndex (constant.ConstantType));
			var token = new MetadataToken (TokenType.Signature, row);

			writer.DefineLocalConstant (constant.Name, constant.Value, token.ToInt32 ());
		}

		int GetDocumentIndex (Document document)
		{
			if (document == null)
				return -1;

			int doc_index;
			if (documents.TryGetValue (document.Url, out doc_index))
				return doc_index;

			doc_index = writer.DefineDocument (
				document.Url,
				document.Language.ToGuid (),
				document.LanguageVendor.ToGuid (),
				document.Type.ToGuid (),
				document.HashAlgorithm.ToGuid (),
				document.Hash,
				source: null); // TODO: implement support for embedded source

			documents [document.Url] = doc_index;
			return doc_index;
		}

		public void Dispose ()
		{
			var entry_point = module.EntryPoint;
			if (entry_point != null)
				writer.SetEntryPoint (entry_point.MetadataToken.ToInt32 ());

			writer.UpdateSignature (id, timestamp, Age);

			using (var stream = new FileStream (pdb_path, FileMode.Create, FileAccess.Write, FileShare.None)) {
				writer.WriteTo (stream);
			}

			writer.Dispose ();
		}
	}

	enum CustomMetadataType : byte {
		UsingInfo = 0,
		ForwardInfo = 1,
		IteratorScopes = 3,
		ForwardIterator = 4,
	}

	class CustomMetadataWriter : IDisposable {

		readonly SymUnmanagedWriter sym_writer;
		readonly MemoryStream stream;
		readonly BinaryStreamWriter writer;

		int count;

		const byte version = 4;

		public CustomMetadataWriter (SymUnmanagedWriter sym_writer)
		{
			this.sym_writer = sym_writer;
			this.stream = new MemoryStream ();
			this.writer = new BinaryStreamWriter (stream);

			writer.WriteByte (version);
			writer.WriteByte (0); // count
			writer.Align (4);
		}

		public void WriteUsingInfo (ImportDebugInformation import_info)
		{
			Write (CustomMetadataType.UsingInfo, () => {
				writer.WriteUInt16 ((ushort) 1);
				writer.WriteUInt16 ((ushort) import_info.Targets.Count);
			});
		}

		public void WriteForwardInfo (MetadataToken import_parent)
		{
			Write (CustomMetadataType.ForwardInfo, () => writer.WriteUInt32 (import_parent.ToUInt32 ()));
		}

		public void WriteIteratorScopes (StateMachineScopeDebugInformation [] scopes, MethodDebugInformation debug_info)
		{
			Write (CustomMetadataType.IteratorScopes, () => {
				writer.WriteInt32 (scopes.Length);
				foreach (var scope in scopes) {
					var start = scope.Start.Offset;
					var end = scope.End.IsEndOfMethod ? debug_info.code_size : scope.End.Offset;
					writer.WriteInt32 (start);
					writer.WriteInt32 (end - 1);
				}
			});
		}

		public void WriteForwardIterator (TypeReference type)
		{
			Write (CustomMetadataType.ForwardIterator, () => writer.WriteBytes(Encoding.Unicode.GetBytes(type.Name)));
		}

		void Write (CustomMetadataType type, Action write)
		{
			count++;
			writer.WriteByte (version);
			writer.WriteByte ((byte) type);
			writer.Align (4);

			var length_position = writer.Position;
			writer.WriteUInt32 (0);

			write ();
			writer.Align (4);

			var end = writer.Position;
			var length = end - length_position + 4; // header is 4 bytes long

			writer.Position = length_position;
			writer.WriteInt32 (length);

			writer.Position = end;
		}

		public void WriteCustomMetadata ()
		{
			if (count == 0)
				return;

			writer.BaseStream.Position = 1;
			writer.WriteByte ((byte) count);
			writer.Flush ();

			sym_writer.DefineCustomMetadata (stream.ToArray ());
		}

		public void Dispose ()
		{
			stream.Dispose ();
		}
	}
}

#endif
