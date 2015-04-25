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

using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using Mono.CompilerServices.SymbolWriter;

namespace Mono.Cecil.Mdb {

	public class MdbReaderProvider : ISymbolReaderProvider {

		public ISymbolReader GetSymbolReader (ModuleDefinition module, string fileName)
		{
			return new MdbReader (module, MonoSymbolFile.ReadSymbolFile (fileName + ".mdb", module.Mvid));
		}

		public ISymbolReader GetSymbolReader (ModuleDefinition module, Stream symbolStream)
		{
			var file = MonoSymbolFile.ReadSymbolFile (symbolStream);
			if (module.Mvid != file.Guid) {
				var file_stream = symbolStream as FileStream;
				if (file_stream != null)
					throw new MonoSymbolFileException ("Symbol file `{0}' does not match assembly", file_stream.Name);

				throw new MonoSymbolFileException ("Symbol file from stream does not match assembly");
			}
			return new MdbReader (module, file);
		}
	}

	public class MdbReader : ISymbolReader {

		readonly ModuleDefinition module;
		readonly MonoSymbolFile symbol_file;
		readonly Dictionary<string, Document> documents;

		public MdbReader (ModuleDefinition module, MonoSymbolFile symFile)
		{
			this.module = module;
			this.symbol_file = symFile;
			this.documents = new Dictionary<string, Document> ();
		}

		public bool ProcessDebugHeader (ImageDebugDirectory directory, byte [] header)
		{
			return symbol_file.Guid == module.Mvid;
		}

		public void Read (MethodBody body, InstructionMapper mapper)
		{
			var method_token = body.Method.MetadataToken;
			var entry = symbol_file.GetMethodByToken (method_token.ToInt32	());
			if (entry == null)
				return;

			var scopes = ReadScopes (entry, body, mapper);
			ReadLineNumbers (entry, mapper);
			ReadLocalVariables (entry, body, scopes);
		}

		static void ReadLocalVariables (MethodEntry entry, MethodBody body, Scope [] scopes)
		{
			var locals = entry.GetLocals ();

			foreach (var local in locals) {
				if (local.Index < 0 || local.Index >= body.Variables.Count) // Mono 2.6 emits wrong local infos for iterators
					continue;
				
				var variable = body.Variables [local.Index];
				variable.Name = local.Name;

				var index = local.BlockIndex;
				if (index < 0 || index >= scopes.Length)
					continue;

				var scope = scopes [index];
				if (scope == null)
					continue;

				scope.Variables.Add (variable);
			}
		}

		void ReadLineNumbers (MethodEntry entry, InstructionMapper mapper)
		{
			Document document = null;
			var table = entry.GetLineNumberTable ();

			foreach (var line in table.LineNumbers) {
				var instruction = mapper (line.Offset);
				if (instruction == null)
					continue;

				if (document == null)
					document = GetDocument (entry.CompileUnit.SourceFile);

				instruction.SequencePoint = LineToSequencePoint (line, entry, document);
			}
		}

		Document GetDocument (SourceFileEntry file)
		{
			var file_name = file.FileName;

			Document document;
			if (documents.TryGetValue (file_name, out document))
				return document;

			document = new Document (file_name);
			documents.Add (file_name, document);

			return document;
		}

		static Scope [] ReadScopes (MethodEntry entry, MethodBody body, InstructionMapper mapper)
		{
			var blocks = entry.GetCodeBlocks ();
			var scopes = new Scope [blocks.Length];

			foreach (var block in blocks) {
				if (block.BlockType != CodeBlockEntry.Type.Lexical)
					continue;

				var scope = new Scope ();
				scope.Start = mapper (block.StartOffset);
				scope.End = mapper (block.EndOffset);

				scopes [block.Index] = scope;

				if (body.Scope == null)
					body.Scope = scope;

				if (!AddScope (body.Scope, scope))
					body.Scope = scope;
			}

			return scopes;
		}

		static bool AddScope (Scope provider, Scope scope)
		{
			foreach (var sub_scope in provider.Scopes) {
				if (AddScope (sub_scope, scope))
					return true;

				if (scope.Start.Offset >= sub_scope.Start.Offset && scope.End.Offset <= sub_scope.End.Offset) {
					sub_scope.Scopes.Add (scope);
					return true;
				}
			}

			return false;
		}

		public void Read (MethodSymbols symbols)
		{
			var entry = symbol_file.GetMethodByToken (symbols.MethodToken.ToInt32 ());
			if (entry == null)
				return;

			ReadLineNumbers (entry, symbols);
			ReadLocalVariables (entry, symbols);
		}

		void ReadLineNumbers (MethodEntry entry, MethodSymbols symbols)
		{
			var table = entry.GetLineNumberTable ();
			var lines = table.LineNumbers;

			var instructions = symbols.instructions = new Collection<InstructionSymbol> (lines.Length);

			for (int i = 0; i < lines.Length; i++) {
				var line = lines [i];

				instructions.Add (new InstructionSymbol (
					line.Offset,
					LineToSequencePoint (line, entry, GetDocument (entry.CompileUnit.SourceFile))));
			}
		}

		static void ReadLocalVariables (MethodEntry entry, MethodSymbols symbols)
		{
			foreach (var local in entry.GetLocals ()) {
				if (local.Index < 0 || local.Index >= symbols.Variables.Count) // Mono 2.6 emits wrong local infos for iterators
					continue;

				var variable = symbols.Variables [local.Index];
				variable.Name = local.Name;
			}
		}

		static SequencePoint LineToSequencePoint (LineNumberEntry line, MethodEntry entry, Document document)
		{
			return new SequencePoint (document) {
				StartLine = line.Row,
				EndLine = line.EndRow,
				StartColumn = line.Column,
				EndColumn = line.EndColumn,
			};
		}

		public void Dispose ()
		{
			symbol_file.Dispose ();
		}
	}

	static class MethodEntryExtensions {

		public static bool HasColumnInfo (this MethodEntry entry)
		{
			return (entry.MethodFlags & MethodEntry.Flags.ColumnsInfoIncluded) != 0;
		}

		public static bool HasEndInfo (this MethodEntry entry)
		{
			return (entry.MethodFlags & MethodEntry.Flags.EndInfoIncluded) != 0;
		}
	}
}
