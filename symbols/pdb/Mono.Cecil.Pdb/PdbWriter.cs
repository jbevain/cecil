//
// PdbWriter.cs
//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2011 Jb Evain
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
using System.Diagnostics.SymbolStore;

using Mono.Cecil.Cil;
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
			var sym_token = new SymbolToken (method_token.ToInt32 ());

			var instructions = CollectInstructions (body);
			if (instructions.Count == 0)
				return;

			var start_offset = 0;
			var end_offset = body.CodeSize;

			writer.OpenMethod (sym_token);
			writer.OpenScope (start_offset);

			DefineSequencePoints (instructions);
			DefineVariables (body, start_offset, end_offset);

			writer.CloseScope (end_offset);

			if (body.IteratorType != null)
				DefineIteratorType   (sym_token, body.IteratorType.Name);

			if (body.iterator_scopes != null)
				DefineIteratorScopes (sym_token, body.IteratorScopes, body.CodeSize);

			writer.CloseMethod ();
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

		void DefineVariables (MethodBody body, int start_offset, int end_offset)
		{
			if (!body.HasVariables)
				return;

			var sym_token = new SymbolToken (body.LocalVarToken.ToInt32 ());

			var variables = body.Variables;
			for (int i = 0; i < variables.Count; i++) {
				var variable = variables [i];
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

		void DefineIteratorType (SymbolToken method_token, string name)
		{
			var buffer = new PE.ByteBuffer ();
			buffer.WriteByte (4);
			buffer.WriteByte (1);
			buffer.Align     (4);
			buffer.WriteByte (4);
			buffer.WriteByte (4);
			buffer.Align	 (4);
		
			var length = 10 + (uint) name.Length * 2;
			while (length % 4 > 0)
				length++;
		
			buffer.WriteUInt32 (length);
			buffer.WriteBytes (System.Text.Encoding.Unicode.GetBytes (name));
			buffer.WriteByte (0);
			buffer.Align	 (4);
		
			writer.SetSymAttribute (method_token, "MD2", buffer.length, buffer.buffer);
		}

		void DefineIteratorScopes (SymbolToken method_token, Collection<RangeSymbol> scopes)
		{
			var buffer = new PE.ByteBuffer ();
			buffer.WriteByte (4);
			buffer.WriteByte (1);
			buffer.Align     (4);
			buffer.WriteByte (4);
			buffer.WriteByte (3);
			buffer.Align	 (4);
		
			buffer.WriteInt32 (scopes.Count * 8 + 12);
			buffer.WriteInt32 (scopes.Count);
		
			foreach (RangeSymbol scope in scopes)
			{
				buffer.WriteInt32 (scope.Start);
				buffer.WriteInt32 (scope.End);
			}
		
			writer.SetSymAttribute (method_token, "MD2", buffer.length, buffer.buffer);
		}

		void DefineIteratorScopes (SymbolToken method_token, Collection<InstructionRange> scopes, int code_size)
		{
			var buffer = new PE.ByteBuffer ();
			buffer.WriteByte (4);
			buffer.WriteByte (1);
			buffer.Align     (4);
			buffer.WriteByte (4);
			buffer.WriteByte (3);
			buffer.Align	 (4);
		
			buffer.WriteInt32 (scopes.Count * 8 + 12);
			buffer.WriteInt32 (scopes.Count);
		
			foreach (InstructionRange scope in scopes)
			{
                buffer.WriteInt32(scope.Start != null ? scope.Start.Offset : code_size);
				buffer.WriteInt32 (scope.End.Next != null ? scope.End.Next.Offset : code_size);
			}
		
			writer.SetSymAttribute (method_token, "MD2", buffer.length, buffer.buffer);
		}

		public void Write (MethodSymbols symbols)
		{
			if (symbols.instructions.IsNullOrEmpty () && !symbols.HasVariables)
				return;

			var sym_token = new SymbolToken (symbols.MethodToken.ToInt32 ());

			var start_offset = 0;
			var end_offset = symbols.CodeSize;

			writer.OpenMethod (sym_token);
			writer.OpenScope (start_offset);

			DefineSequencePoints (symbols);
			DefineVariables (symbols, start_offset, end_offset);

			writer.CloseScope (end_offset);

			if (!string.IsNullOrEmpty (symbols.IteratorType))
				DefineIteratorType   (sym_token, symbols.IteratorType);

			if (symbols.iterator_scopes != null)
				DefineIteratorScopes (sym_token, symbols.IteratorScopes);

			writer.CloseMethod ();
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

		void DefineVariables (MethodSymbols symbols, int start_offset, int end_offset)
		{
			if (!symbols.HasVariables)
				return;

			var sym_token = new SymbolToken (symbols.LocalVarToken.ToInt32 ());

			var variables = symbols.Variables;
			for (int i = 0; i < variables.Count; i++) {
				var variable = variables [i];
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
}

#endif
