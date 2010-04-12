//
// PdbWriter.cs
//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2010 Jb Evain
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

namespace Mono.Cecil.Pdb {

	public class PdbWriter : Cil.ISymbolWriter {

		readonly SymWriter writer;
		readonly Dictionary<string, SymDocumentWriter> documents;

		internal PdbWriter (SymWriter writer)
		{
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

			writer.OpenMethod (sym_token);
			DefineSequencePoints (body);
			DefineVariables (body);
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

		void DefineVariables (MethodBody body)
		{
			if (!body.HasVariables)
				return;

			var start_offset = 0;
			var end_offset = body.Instructions [body.Instructions.Count - 1].Offset;

			writer.OpenScope (start_offset);

			var sym_token = new SymbolToken (body.LocalVarToken.ToInt32 ());

			var variables = body.Variables;
			for (int i = 0; i < variables.Count; i++) {
				var variable = variables [i];
				CreateLocalVariable (variable, sym_token, start_offset, end_offset);
			}

			writer.CloseScope (end_offset);
		}

		void DefineSequencePoints (MethodBody body)
		{
			var instructions = CollectInstructions (body);
			var count = instructions.Count;
			if (count == 0)
				return;

			Document document = null;

			var offsets = new int [count];
			var start_rows = new int [count];
			var start_columns = new int [count];
			var end_rows = new int [count];
			var end_columns = new int [count];

			for (int i = 0; i < count; i++) {
				var instruction = instructions [i];
				offsets [i] = instruction.Offset;

				var sequence_point = instruction.SequencePoint;
				if (document == null)
					document = sequence_point.Document;

				start_rows [i] = sequence_point.StartLine;
				start_columns [i] = sequence_point.StartColumn;
				end_rows [i] = sequence_point.EndLine;
				end_columns [i] = sequence_point.EndColumn;
			}

			writer.DefineSequencePoints (
				GetDocument (document),
			    offsets,
				start_rows,
				start_columns,
				end_rows,
				end_columns);
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
			var sym_token = new SymbolToken (symbols.MethodToken.ToInt32 ());

			writer.OpenMethod (sym_token);
			DefineSequencePoints (symbols);
			DefineVariables (symbols);
			writer.CloseMethod ();
		}

		void DefineSequencePoints (MethodSymbols symbols)
		{
			writer.DefineSequencePoints (
				GetDocument (symbols.Document),
				symbols.Offsets,
				symbols.StartRows,
				symbols.StartColumns,
				symbols.EndRows,
				symbols.EndColumns);
		}

		void DefineVariables (MethodSymbols symbols)
		{
			if (!symbols.HasVariables)
				return;

			var start_offset = 0;
			var end_offset = symbols.CodeSize;

			writer.OpenScope (start_offset);

			var sym_token = new SymbolToken (symbols.LocalVarToken.ToInt32 ());

			var variables = symbols.Variables;
			for (int i = 0; i < variables.Count; i++) {
				var variable = variables [i];
				CreateLocalVariable (variable, sym_token, start_offset, end_offset);
			}

			writer.CloseScope (end_offset);
		}

		public void Dispose ()
		{
			writer.Close ();
		}
	}
}
