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

using Mono.Collections.Generic;

using Microsoft.Cci.Pdb;

using Mono.Cecil.Cil;

namespace Mono.Cecil.Pdb {

	public class PdbReader : ISymbolReader {

		int age;
		Guid guid;

		readonly Disposable<Stream> pdb_file;
		readonly Dictionary<string, Document> documents = new Dictionary<string, Document> ();
		readonly Dictionary<uint, PdbFunction> functions = new Dictionary<uint, PdbFunction> ();

		internal PdbReader (Disposable<Stream> file)
		{
			this.pdb_file = file;
		}

		/*
		uint Magic = 0x53445352;
		Guid Signature;
		uint Age;
		string FileName;
		 */

		public bool ProcessDebugHeader (ImageDebugDirectory directory, byte [] header)
		{
			if (directory.Type != 2) //IMAGE_DEBUG_TYPE_CODEVIEW
				return false;
			if (directory.MajorVersion != 0 || directory.MinorVersion != 0)
				return false;

			if (header.Length < 24)
				return false;

			var magic = ReadInt32 (header, 0);
			if (magic != 0x53445352)
				return false;

			var guid_bytes = new byte [16];
			Buffer.BlockCopy (header, 4, guid_bytes, 0, 16);

			this.guid = new Guid (guid_bytes);
			this.age = ReadInt32 (header, 20);

			return PopulateFunctions ();
		}

		static int ReadInt32 (byte [] bytes, int start)
		{
			return (bytes [start]
				| (bytes [start + 1] << 8)
				| (bytes [start + 2] << 16)
				| (bytes [start + 3] << 24));
		}

		bool PopulateFunctions ()
		{
			using (pdb_file) {
				Dictionary<uint, PdbTokenLine> tokenToSourceMapping;
				string sourceServerData;
				int age;
				Guid guid;

				var funcs = PdbFile.LoadFunctions (pdb_file.value, out tokenToSourceMapping,  out sourceServerData, out age, out guid);

				if (this.guid != guid)
					return false;

				foreach (PdbFunction function in funcs)
					functions.Add (function.token, function);
			}

			return true;
		}

		public MethodDebugInformation Read (MethodDefinition method)
		{
			var method_token = method.MetadataToken;

			PdbFunction function;
			if (!functions.TryGetValue (method_token.ToUInt32 (), out function))
				return null;

			var symbol = new MethodDebugInformation (method);

			ReadSequencePoints (function, symbol);

			if (function.scopes.Length > 1)
				throw new NotSupportedException ();
			else if (function.scopes.Length == 1)
				symbol.scope = ReadScopeAndLocals (function.scopes [0], symbol);

			return symbol;
		}

		static Collection<ScopeDebugInformation> ReadScopeAndLocals (PdbScope [] scopes, MethodDebugInformation info)
		{
			var symbols = new Collection<ScopeDebugInformation> (scopes.Length);

			foreach (PdbScope scope in scopes)
				if (scope != null)
					symbols.Add (ReadScopeAndLocals (scope, info));

			return symbols;
		}

		static ScopeDebugInformation ReadScopeAndLocals (PdbScope scope, MethodDebugInformation info)
		{
			var parent = new ScopeDebugInformation ();
			parent.Start = new InstructionOffset ((int) scope.offset);
			parent.End = new InstructionOffset ((int) (scope.offset + scope.length));

			if (!scope.slots.IsNullOrEmpty()) {
				parent.variables = new Collection<VariableDebugInformation> (scope.slots.Length);

				foreach (PdbSlot slot in scope.slots) {
					var index = (int) slot.slot;
					var variable = new VariableDebugInformation (index, slot.name);
					if (slot.flags == 4)
						variable.IsDebuggerHidden = true;
					parent.variables.Add (variable);
				}
			}

			if (!scope.constants.IsNullOrEmpty ()) {
				parent.constants = new Collection<ConstantDebugInformation> (scope.constants.Length);

				foreach (var constant in scope.constants) {
					parent.constants.Add (new ConstantDebugInformation (
						constant.name,
						(TypeReference) info.method.Module.LookupToken ((int) constant.token),
						constant.value));
				}
			}

			parent.scopes = ReadScopeAndLocals (scope.scopes, info);

			return parent;
		}

		void ReadSequencePoints (PdbFunction function, MethodDebugInformation info)
		{
			if (function.lines == null)
				return;

			info.sequence_points = new Collection<SequencePoint> ();

			foreach (PdbLines lines in function.lines)
				ReadLines (lines, info);
		}

		void ReadLines (PdbLines lines, MethodDebugInformation info)
		{
			var document = GetDocument (lines.file);

			foreach (var line in lines.lines)
				ReadLine (line, document, info);
		}

		static void ReadLine (PdbLine line, Document document, MethodDebugInformation info)
		{
			var sequence_point = new SequencePoint ((int) line.offset, document);
			sequence_point.StartLine = (int) line.lineBegin;
			sequence_point.StartColumn = (int) line.colBegin;
			sequence_point.EndLine = (int) line.lineEnd;
			sequence_point.EndColumn = (int) line.colEnd;

			info.sequence_points.Add (sequence_point);
		}

		Document GetDocument (PdbSource source)
		{
			string name = source.name;
			Document document;
			if (documents.TryGetValue (name, out document))
				return document;

			document = new Document (name) {
				Language = source.language.ToLanguage (),
				LanguageVendor = source.vendor.ToVendor (),
				Type = source.doctype.ToType (),
			};
			documents.Add (name, document);
			return document;
		}

		public void Dispose ()
		{
			pdb_file.Dispose ();
		}
	}
}
