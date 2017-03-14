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
using System.Diagnostics.SymbolStore;
using System.IO;
using Mono.Cecil.Cil;
using Mono.Cecil.PE;
using Mono.Collections.Generic;

#if !READ_ONLY

namespace Mono.Cecil.Pdb {

	public class NativePdbWriter : Cil.ISymbolWriter {

		readonly ModuleDefinition module;
		readonly SymWriter writer;
		readonly Dictionary<string, SymDocumentWriter> documents;
		readonly Dictionary<ImportDebugInformation, uint> importToMethods;

		internal NativePdbWriter (ModuleDefinition module, SymWriter writer)
		{
			this.module = module;
			this.writer = writer;
			this.documents = new Dictionary<string, SymDocumentWriter> ();
			this.importToMethods = new Dictionary<ImportDebugInformation, uint> ();
		}

		public ImageDebugHeader GetDebugHeader ()
		{
			ImageDebugDirectory directory;
			var data = writer.GetDebugInfo (out directory);
			directory.TimeDateStamp = (int) module.timestamp;
			return new ImageDebugHeader (new ImageDebugHeaderEntry (directory, data));
		}

		public void Write (MethodDebugInformation info)
		{
			var method_token = info.method.MetadataToken;
			var sym_token = new SymbolToken (method_token.ToInt32 ());

			// Nothing interesting to save
			if (!info.HasSequencePoints && info.scope == null && !info.HasCustomDebugInformations && info.StateMachineKickOffMethod == null)
				return;

			writer.OpenMethod (sym_token);

			if (!info.sequence_points.IsNullOrEmpty ())
				DefineSequencePoints (info.sequence_points);

			uint methodWhoseUsingInfoAppliesToThisMethod = 0;

			if (info.scope != null)
				DefineScope(info.scope, info, out methodWhoseUsingInfoAppliesToThisMethod);

			DefineCustomMetadata (info, methodWhoseUsingInfoAppliesToThisMethod);

			writer.CloseMethod ();
		}

		private void DefineCustomMetadata (MethodDebugInformation info, uint methodWhoseUsingInfoAppliesToThisMethod)
		{
			// Custom PDB metadata
			using (var memoryStream = new MemoryStream())
			{
				var metadata = new BinaryStreamWriter(memoryStream);
				metadata.WriteByte(4); // version
				metadata.WriteByte((byte)1); // count
				metadata.WriteInt16(0); // padding

				var metadataStartPosition = metadata.BaseStream.Position;
				var customMetadataCount = 0;

				// Using informations
				if (methodWhoseUsingInfoAppliesToThisMethod != 0)
				{
					customMetadataCount++;
					metadata.WriteByte(4); // version
					metadata.WriteByte(1); // forward info
					metadata.Align(4);
					using (new PdbBinaryStreamWriterSizeHelper(metadata))
					{
						metadata.WriteUInt32(methodWhoseUsingInfoAppliesToThisMethod);
					}
				}
				else if (info.scope != null && info.scope.Import != null && info.scope.Import.HasTargets)
				{
					customMetadataCount++;
					metadata.WriteByte(4); // version
					metadata.WriteByte(0); // using info
					metadata.Align(4);
					using (new PdbBinaryStreamWriterSizeHelper(metadata))
					{
						metadata.WriteUInt16((ushort)1);
						metadata.WriteUInt16((ushort)info.scope.Import.Targets.Count);
						metadata.Align(4);
					}
				}

				// Note: This code detects state machine attributes automatically (rather than adding an IteratorClassDebugInformation only for Native PDB)
				if (info.Method.HasCustomAttributes)
				{
					foreach (var customAttribute in info.Method.CustomAttributes)
					{
						if (customAttribute.AttributeType.FullName == "System.Runtime.CompilerServices.IteratorStateMachineAttribute"
							|| customAttribute.AttributeType.FullName == "System.Runtime.CompilerServices.AsyncStateMachineAttribute")
						{
							var type = customAttribute.ConstructorArguments[0].Value as TypeReference;
							if (type == null)
								continue;

							customMetadataCount++;
							metadata.WriteByte(4); // version
							metadata.WriteByte(4); // forward iterator
							metadata.Align(4);
							using (new PdbBinaryStreamWriterSizeHelper(metadata))
							{
								metadata.WriteString(type.Name);
								metadata.Align(4);
							}
						}
					}
				}

				// StateMachineScopeDebugInformation
				var stateMachineDebugInformationCount = 0;
				if (info.HasCustomDebugInformations)
				{
					// Count
					foreach (var customDebugInformation in info.CustomDebugInformations)
					{
						if (customDebugInformation is StateMachineScopeDebugInformation)
							stateMachineDebugInformationCount++;
					}

					if (stateMachineDebugInformationCount > 0)
					{
						customMetadataCount++;
						metadata.WriteByte(4); // version
						metadata.WriteByte(3); // iterator scopes
						metadata.Align(4);
						using (new PdbBinaryStreamWriterSizeHelper(metadata))
						{
							metadata.WriteInt32(stateMachineDebugInformationCount);
							foreach (var customDebugInformation in info.CustomDebugInformations)
							{
								var stateMachineDebugInformation = customDebugInformation as StateMachineScopeDebugInformation;
								if (stateMachineDebugInformation != null)
								{
									var start = stateMachineDebugInformation.Start.Offset;
									var end = stateMachineDebugInformation.End.IsEndOfMethod ? info.code_size : stateMachineDebugInformation.End.Offset;
									metadata.WriteInt32(start);
									metadata.WriteInt32(end - 1);
								}
							}
						}
					}
				}

				if (metadata.BaseStream.Position != metadataStartPosition)
				{
					// Update number of entries
					metadata.Flush();
					metadata.BaseStream.Position = 1;
					metadata.WriteByte((byte)customMetadataCount);
					metadata.Flush();

					writer.DefineCustomMetadata("MD2", memoryStream.ToArray());
				}
			}

			foreach (var customDebugInformation in info.CustomDebugInformations)
			{
				// Save back asyncMethodInfo
				var asyncDebugInfo = customDebugInformation as AsyncMethodBodyDebugInformation;
				if (asyncDebugInfo != null)
				{
					using (var asyncMemoryStream = new MemoryStream())
					{
						var asyncMetadata = new BinaryStreamWriter(asyncMemoryStream);
						asyncMetadata.WriteUInt32(info.StateMachineKickOffMethod != null ? info.StateMachineKickOffMethod.MetadataToken.ToUInt32() : 0);
						asyncMetadata.WriteUInt32((uint)asyncDebugInfo.CatchHandler.Offset);
						asyncMetadata.WriteUInt32((uint)asyncDebugInfo.Resumes.Count);
						for (int i = 0; i < asyncDebugInfo.Resumes.Count; ++i)
						{
							asyncMetadata.WriteUInt32((uint)asyncDebugInfo.Yields[i].Offset);
							asyncMetadata.WriteUInt32(asyncDebugInfo.MoveNextMethod != null ? asyncDebugInfo.MoveNextMethod.MetadataToken.ToUInt32() : 0);
							asyncMetadata.WriteUInt32((uint)asyncDebugInfo.Resumes[i].Offset);
						}

						writer.DefineCustomMetadata("asyncMethodInfo", asyncMemoryStream.ToArray());
					}
				}
			}
		}

	    void DefineScope (ScopeDebugInformation scope, MethodDebugInformation info, out uint methodWhoseUsingInfoAppliesToThisMethod)
		{
			var start_offset = scope.Start.Offset;
			var end_offset = scope.End.IsEndOfMethod
				? info.code_size
				: scope.End.Offset;

			methodWhoseUsingInfoAppliesToThisMethod = 0;

			writer.OpenScope (start_offset);

			if (scope.Import != null && scope.Import.HasTargets) {
				if (!importToMethods.TryGetValue (info.scope.Import, out methodWhoseUsingInfoAppliesToThisMethod)) {

					foreach (var target in scope.Import.Targets) {
						switch (target.Kind)
						{
							case ImportTargetKind.ImportNamespace:
								writer.UsingNamespace("U" + target.@namespace);
								break;
							case ImportTargetKind.ImportType:
								writer.UsingNamespace("T" + TypeParser.ToParseable(target.type, false));
								break;
							case ImportTargetKind.DefineNamespaceAlias:
								writer.UsingNamespace("A" + target.Alias + " U" + target.@namespace);
								break;
							case ImportTargetKind.DefineTypeAlias:
								writer.UsingNamespace("A" + target.Alias + " T" + TypeParser.ToParseable(target.type, false));
								break;
						}
					}

					importToMethods.Add (info.scope.Import, info.method.MetadataToken.ToUInt32 ());
				}
			}

			var sym_token = new SymbolToken (info.local_var_token.ToInt32 ());

			if (!scope.variables.IsNullOrEmpty ()) {
				for (int i = 0; i < scope.variables.Count; i++) {
					var variable = scope.variables [i];
					CreateLocalVariable (variable, sym_token, start_offset, end_offset);
				}
			}

			if (!scope.scopes.IsNullOrEmpty ()) {
				for (int i = 0; i < scope.scopes.Count; i++) {
					uint ignored;
					DefineScope (scope.scopes [i], info, out ignored);
				}
			}

			writer.CloseScope (end_offset);
		}

		void DefineSequencePoints (Collection<SequencePoint> sequence_points)
		{
			for (int i = 0; i < sequence_points.Count; i++) {
				var sequence_point = sequence_points [i];

				writer.DefineSequencePoints (
					GetDocument (sequence_point.Document),
					new [] { sequence_point.Offset },
					new [] { sequence_point.StartLine },
					new [] { sequence_point.StartColumn },
					new [] { sequence_point.EndLine },
					new [] { sequence_point.EndColumn });
			}
		}

		void CreateLocalVariable (VariableDebugInformation variable, SymbolToken local_var_token, int start_offset, int end_offset)
		{
			writer.DefineLocalVariable2 (
				variable.Name,
				variable.Attributes,
				local_var_token,
				SymAddressKind.ILOffset,
				variable.Index,
				0,
				0,
				start_offset,
				end_offset);
		}

		SymDocumentWriter GetDocument (Document document)
		{
			if (document == null)
				return null;

			SymDocumentWriter doc_writer;
			if (documents.TryGetValue (document.Url, out doc_writer))
				return doc_writer;

			doc_writer = writer.DefineDocument (
				document.Url,
				document.Language.ToGuid (),
				document.LanguageVendor.ToGuid (),
				document.Type.ToGuid ());

			documents [document.Url] = doc_writer;
			return doc_writer;
		}

		public void Dispose ()
		{
			var entry_point = module.EntryPoint;
			if (entry_point != null)
				writer.SetUserEntryPoint (new SymbolToken (entry_point.MetadataToken.ToInt32 ()));

			writer.Close ();
		}
	}

	// Helper that will write back total after dispose
	struct PdbBinaryStreamWriterSizeHelper : IDisposable
	{
		private readonly BinaryStreamWriter streamWriter;
		private readonly uint startPosition;

		public PdbBinaryStreamWriterSizeHelper(BinaryStreamWriter streamWriter)
		{
			this.streamWriter = streamWriter;

			// Remember start position
			this.startPosition = (uint)streamWriter.BaseStream.Position;

			// Write 0 for now
			streamWriter.WriteUInt32(0);
		}

		public void Dispose()
		{
			streamWriter.Flush();
			var endPosition = (uint)streamWriter.BaseStream.Position;

			// Write updated size
			streamWriter.BaseStream.Position = startPosition;
			streamWriter.WriteUInt32(endPosition - startPosition + 4); // adds 4 for header
			streamWriter.Flush();

			streamWriter.BaseStream.Position = endPosition;
		}
	}

	static class StreamExtensions
	{
		public static void Align(this BinaryStreamWriter streamWriter, int alignment)
		{
			var position = (int)streamWriter.BaseStream.Position;
			var paddingLength = (position + alignment - 1) / alignment * alignment - position;
			for (var i = 0; i < paddingLength; ++i)
				streamWriter.Write((byte)0);
		}

		public static void WriteString(this BinaryStreamWriter streamWriter, string str)
		{
			foreach (var c in str)
			{
				streamWriter.WriteInt16((short)c);
			}
			streamWriter.WriteInt16(0);
		}
	}
}

#endif
