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

	public class PdbWriter : Cil.ISymbolWriter {

		readonly ModuleDefinition module;
		readonly SymWriter writer;
		readonly Dictionary<string, SymDocumentWriter> documents;

		internal PdbWriter (ModuleDefinition module, SymWriter writer)
		{
			this.module = module;
			this.writer = writer;
			this.documents = new Dictionary<string, SymDocumentWriter> ();
		}

		public bool GetDebugHeader (out ImageDebugDirectory directory, out byte [] header)
		{
			header = writer.GetDebugInfo (out directory);
			return true;
		}

		public void Write (MethodBody body)
		{
			var method_token = body.Method.MetadataToken;
			var sym_token = new SymbolToken(method_token.ToInt32());

			var instructions = CollectInstructions(body);
			if (instructions.Count == 0 && !body.HasVariables)
				return;

			var pdbSymbols = body.Symbols as PdbMethodSymbols;

			writer.OpenMethod(sym_token);

			DefineSequencePoints(instructions);

            if (body.Scope != null)
                WriteScope(body, body.Scope, true);
            else
            {
                writer.OpenScope(0);
                DefineUsedNamespaces(pdbSymbols);
                if (body.HasVariables)
                    DefineVariables(body, body.Variables, 0, body.CodeSize);
                writer.CloseScope(body.CodeSize);
            }

            DefineCustomMetadata(pdbSymbols);

			writer.CloseMethod();
		}

		void WriteScope (MethodBody body, Scope scope, bool rootScope)
		{
			var start_offset = scope.Start.Offset;
			var end_offset   = scope.End.Next != null ? scope.End.Next.Offset : body.CodeSize;
			
			writer.OpenScope  (start_offset);

            // TODO: Store used namespace per scope?
		    if (rootScope && body.Symbols is PdbMethodSymbols)
		        DefineUsedNamespaces ((PdbMethodSymbols)body.Symbols);

			foreach (var s in scope.Scopes)
				WriteScope (body, s, false);

			DefineVariables   (body, scope.Variables, start_offset, end_offset);
			writer.CloseScope (end_offset);
		}

		Collection<Instruction> CollectInstructions (MethodBody body)
		{
			var collection = new Collection<Instruction> ();
			var instructions = body.Instructions;

			for (int i = 0; i < instructions.Count; i++) {
				var instruction = instructions [i];
				var sequence_point = instruction.SequencePoint;
				if (sequence_point == null)
					continue;

				GetDocument (sequence_point.Document);
				collection.Add (instruction);
			}

			return collection;
		}

		void DefineVariables (MethodBody body, Collection<VariableDefinition> variables, int start_offset, int end_offset)
		{
			var sym_token = new SymbolToken (body.LocalVarToken.ToInt32 ());

			for (int i = 0; i < variables.Count; i++) {
				var variable = variables [i];
				if (!string.IsNullOrEmpty(variable.Name))
				CreateLocalVariable (variable, sym_token, start_offset, end_offset);
			}
		}

		void DefineSequencePoints (Collection<Instruction> instructions)
		{
			for (int i = 0; i < instructions.Count; i++) {
				var instruction = instructions [i];
				var sequence_point = instruction.SequencePoint;

				writer.DefineSequencePoints (
					GetDocument (sequence_point.Document),
					new [] { instruction.Offset },
					new [] { sequence_point.StartLine },
					new [] { sequence_point.StartColumn },
					new [] { sequence_point.EndLine },
					new [] { sequence_point.EndColumn });
			}
		}

		void CreateLocalVariable (VariableDefinition variable, SymbolToken local_var_token, int start_offset, int end_offset)
		{
			writer.DefineLocalVariable2 (
				variable.Name,
				0,
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

		public void Write (MethodSymbols symbols)
		{
            if (symbols.instructions.IsNullOrEmpty() && !symbols.HasVariables)
                return;

			var sym_token = new SymbolToken (symbols.OriginalMethodToken.ToInt32 ());

			var pdbSymbols = symbols as PdbMethodSymbols;

			writer.OpenMethod (sym_token);

			DefineSequencePoints (symbols);

			if (symbols.Scope != null)
				WriteScope (pdbSymbols, symbols.Scope, true);
			else
			{
				writer.OpenScope  (0);
			    DefineUsedNamespaces (pdbSymbols);
                if (symbols.HasVariables)
			        DefineVariables(symbols, symbols.Variables, 0, symbols.CodeSize);
                writer.CloseScope (symbols.CodeSize);
			}

            DefineCustomMetadata(pdbSymbols);

            writer.CloseMethod ();
		}

		private void DefineCustomMetadata (PdbMethodSymbols pdbSymbols)
		{
			// Custom PDB metadata
			if (pdbSymbols != null) {
				using (var memoryStream = new MemoryStream ()) {
					var metadata = new BinaryStreamWriter (memoryStream);
					metadata.WriteByte (4); // version
					metadata.WriteByte ((byte) 1); // count
					metadata.WriteInt16 (0); // padding

					var metadataStartPosition = metadata.BaseStream.Position;
					var customMetadataCount = 0;

					if (pdbSymbols.IteratorClass != null) {
						customMetadataCount++;
						metadata.WriteByte (4); // version
						metadata.WriteByte (4); // forward iterator
						metadata.Align (4);
						using (new PdbBinaryStreamWriterSizeHelper (metadata)) {
							metadata.WriteString (pdbSymbols.IteratorClass);
							metadata.WriteInt16 (0);
							metadata.Align (4);
						}
					}

					if (pdbSymbols.UsingCounts != null) {
						customMetadataCount++;
						metadata.WriteByte (4); // version
						metadata.WriteByte (0); // using info
						metadata.Align (4);
						using (new PdbBinaryStreamWriterSizeHelper (metadata)) {
							metadata.WriteUInt16 ((ushort) pdbSymbols.UsingCounts.Count);
							foreach (var uc in pdbSymbols.UsingCounts) {
								metadata.WriteUInt16 (uc);
							}
							metadata.Align (4);
						}
					}

					if (pdbSymbols.MethodWhoseUsingInfoAppliesToThisMethod != null) {
						customMetadataCount++;
						metadata.WriteByte (4); // version
						metadata.WriteByte (1); // forward info
						metadata.Align (4);
						using (new PdbBinaryStreamWriterSizeHelper (metadata)) {
							metadata.WriteUInt32 (pdbSymbols.MethodWhoseUsingInfoAppliesToThisMethod.MetadataToken.ToUInt32 ());
						}
					}

					if (pdbSymbols.IteratorScopes != null) {
						customMetadataCount++;
						metadata.WriteByte (4); // version
						metadata.WriteByte (3); // iterator scopes
						metadata.Align (4);
						using (new PdbBinaryStreamWriterSizeHelper (metadata)) {
							metadata.WriteInt32 (pdbSymbols.IteratorScopes.Count);
							foreach (var scope in pdbSymbols.IteratorScopes) {
								metadata.WriteInt32 (scope.Start.Offset);
								metadata.WriteInt32 (scope.End.Offset);
							}
						}
					}

					if (metadata.BaseStream.Position != metadataStartPosition) {
						// Update number of entries
						metadata.Flush ();
						metadata.BaseStream.Position = 1;
						metadata.WriteByte ((byte) customMetadataCount);
						metadata.Flush ();
	
						writer.DefineCustomMetadata ("MD2", memoryStream.ToArray ());
					}
				}

				// Save back asyncMethodInfo
				if (pdbSymbols.SynchronizationInformation != null) {
					using (var memoryStream = new MemoryStream ()) {
						var metadata = new BinaryStreamWriter (memoryStream);
						metadata.WriteUInt32 (pdbSymbols.SynchronizationInformation.KickoffMethod != null ? pdbSymbols.SynchronizationInformation.KickoffMethod.MetadataToken.ToUInt32 () : 0);
						metadata.WriteUInt32 (pdbSymbols.SynchronizationInformation.GeneratedCatchHandlerIlOffset);
						metadata.WriteUInt32 ((uint) pdbSymbols.SynchronizationInformation.SynchronizationPoints.Count);
						foreach (var syncPoint in pdbSymbols.SynchronizationInformation.SynchronizationPoints) {
							metadata.WriteUInt32 (syncPoint.SynchronizeOffset);
							metadata.WriteUInt32 (syncPoint.ContinuationMethod != null ? syncPoint.ContinuationMethod.MetadataToken.ToUInt32 () : 0);
							metadata.WriteUInt32 (syncPoint.ContinuationOffset);
						}
	
						writer.DefineCustomMetadata ("asyncMethodInfo", memoryStream.ToArray ());
					}
				}
			}
		}

		private void DefineUsedNamespaces (PdbMethodSymbols pdbSymbols)
		{
	    	// Used namespaces
			if (pdbSymbols != null && pdbSymbols.UsedNamespaces != null) {
				foreach (var @namespace in pdbSymbols.UsedNamespaces) {
					writer.UsingNamespace (@namespace);
				}
			}
		}

		void DefineSequencePoints (MethodSymbols symbols)
		{
			var instructions = symbols.instructions;
			if (instructions == null)
				return;

			for (int i = 0; i < instructions.Count; i++) {
				var instruction = instructions [i];
				var sequence_point = instruction.SequencePoint;

				writer.DefineSequencePoints (
					GetDocument (sequence_point.Document),
					new [] { instruction.Offset },
					new [] { sequence_point.StartLine },
					new [] { sequence_point.StartColumn },
					new [] { sequence_point.EndLine },
					new [] { sequence_point.EndColumn });
			}
		}

		void WriteScope (PdbMethodSymbols symbols, ScopeSymbol scope, bool rootScope)
		{
			writer.OpenScope  (scope.Start);

            // TODO: Store used namespace per scope?
            if (rootScope)
                DefineUsedNamespaces(symbols);

            foreach (var s in scope.Scopes)
				WriteScope (symbols, s, false);

			DefineVariables   (symbols, scope.Variables, scope.Start, scope.End);
			writer.CloseScope (scope.End);
		}

		void DefineVariables (MethodSymbols symbols, Collection<VariableDefinition> variables, int start_offset, int end_offset)
		{
			var sym_token = new SymbolToken (symbols.LocalVarToken.ToInt32 ());

			for (int i = 0; i < variables.Count; i++) {
				var variable = variables [i];
				if (!string.IsNullOrEmpty(variable.Name))
				CreateLocalVariable (variable, sym_token, start_offset, end_offset);
			}
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
	struct PdbBinaryStreamWriterSizeHelper : IDisposable {
		private readonly BinaryStreamWriter streamWriter;
		private readonly uint startPosition;

		public PdbBinaryStreamWriterSizeHelper (BinaryStreamWriter streamWriter)
		{
			this.streamWriter = streamWriter;

			// Remember start position
			this.startPosition = (uint)streamWriter.BaseStream.Position;
			
			// Write 0 for now
			streamWriter.WriteUInt32 (0);
		}

		public void Dispose ()
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
				streamWriter.Write ((byte)0);
		}

		public static void WriteString(this BinaryStreamWriter streamWriter, string str)
		{
			foreach (var c in str) {
				streamWriter.WriteInt16 ((short)c);
			}
			streamWriter.WriteInt16 (0);
		}
	}
}

#endif
