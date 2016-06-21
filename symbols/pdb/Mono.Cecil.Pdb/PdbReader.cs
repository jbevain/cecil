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
using Microsoft.Cci.Pdb;

using Mono.Cecil.Cil;
using Mono.Collections.Generic;

namespace Mono.Cecil.Pdb {

	public class PdbReader : ISymbolReader {

		int age;
		Guid guid;

		readonly Stream pdb_file;
		readonly Dictionary<string, Document> documents = new Dictionary<string, Document> ();
		readonly Dictionary<uint, PdbFunction> functions = new Dictionary<uint, PdbFunction> ();

		internal PdbReader (Stream file)
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

				var funcs = PdbFile.LoadFunctions (pdb_file, out tokenToSourceMapping,  out sourceServerData, out age, out guid);

				if (this.guid != guid)
					return false;

				foreach (PdbFunction function in funcs)
					functions.Add (function.token, function);
			}

			return true;
		}

		public MethodSymbols Create (MethodBody methodBody)
		{
			return new PdbMethodSymbols(methodBody);
		}

		public void Read (MethodBody body, InstructionMapper mapper, ISymbolReaderResolver symbolReaderResolver)
		{
			var method_token = body.Method.MetadataToken;

			PdbFunction function;
			if (!functions.TryGetValue (method_token.ToUInt32 (), out function))
				return;

			ReadSequencePoints (function, mapper);
			ReadScopeAndLocals (function.scopes, null, body, mapper);

			body.Symbols = new PdbMethodSymbols (body);
			ReadSymbols(body.Symbols, symbolReaderResolver, function);
		}

		static void SetInstructionRange (MethodBody body, InstructionMapper mapper,	InstructionRange range, uint offset, uint length)
		{
			range.Start = mapper ((int) offset);
			range.End   = mapper ((int)(offset + length));

			if (range.End == null) range.End = body.Instructions[body.Instructions.Count - 1];
			else                   range.End = range.End.Previous;

			if (range.Start == null) range.Start = range.End;
		}

		static void ReadScopeAndLocals (PdbScope [] scopes, Scope parent, MethodBody body, InstructionMapper mapper)
		{
			foreach (PdbScope scope in scopes)
				ReadScopeAndLocals (scope, parent, body, mapper);
		}

		static void ReadScopeAndLocals (PdbScope scope, Scope parent, MethodBody body, InstructionMapper mapper)
		{
			if (scope == null)
				return;

			Scope s = new Scope ();
			SetInstructionRange (body, mapper, s, scope.offset, scope.length);

			if (parent != null)
				parent.Scopes.Add (s);
			else
			if (body.Scope == null)
				body.Scope = s;
			else
				throw new InvalidDataException () ;

			foreach (PdbSlot slot in scope.slots) {
				int index = (int) slot.slot;
				if (index < 0 || index >= body.Variables.Count)
					continue;

				VariableDefinition variable = body.Variables [index];
				variable.Name = slot.name;

				s.Variables.Add (variable);
			}

			ReadScopeAndLocals (scope.scopes, s, body, mapper);
		}

		void ReadSequencePoints (PdbFunction function, InstructionMapper mapper)
		{
			if (function.lines == null)
				return;

			foreach (PdbLines lines in function.lines)
				ReadLines (lines, mapper);
		}

		void ReadLines (PdbLines lines, InstructionMapper mapper)
		{
			var document = GetDocument (lines.file);

			foreach (var line in lines.lines)
				ReadLine (line, document, mapper);
		}

		static void ReadLine (PdbLine line, Document document, InstructionMapper mapper)
		{
			var instruction = mapper ((int) line.offset);
			if (instruction == null)
				return;

			var sequence_point = new SequencePoint (document);
			sequence_point.StartLine = (int) line.lineBegin;
			sequence_point.StartColumn = (int) line.colBegin;
			sequence_point.EndLine = (int) line.lineEnd;
			sequence_point.EndColumn = (int) line.colEnd;

			instruction.SequencePoint = sequence_point;
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

		public void Read (MethodSymbols symbols, ISymbolReaderResolver symbolReaderResolver)
		{
			PdbFunction function;
			if (!functions.TryGetValue (symbols.OriginalMethodToken.ToUInt32 (), out function))
				return;

			ReadSequencePoints (function, symbols);
			ReadScopeAndLocals (function.scopes, null, symbols);

		    ReadSymbols (symbols, symbolReaderResolver, function);
		}

		private static void ReadSymbols (MethodSymbols symbols, ISymbolReaderResolver symbolReaderResolver, PdbFunction function)
		{
			var pdbSymbols = symbols as PdbMethodSymbols;
			if (pdbSymbols != null) {
				// Iterator info
				pdbSymbols.IteratorClass = function.iteratorClass;

			    if (function.iteratorScopes != null) {
			        pdbSymbols.IteratorScopes = new Collection<PdbIteratorScope> ();
					foreach (var iteratorScope in function.iteratorScopes) {
						pdbSymbols.IteratorScopes.Add (new PdbIteratorScope (
							CodeReader.GetInstruction(symbols.Body.Instructions, (int)iteratorScope.Offset), // start
							CodeReader.GetInstruction(symbols.Body.Instructions, (int)iteratorScope.Offset + (int)iteratorScope.Length) // end
								?? CodeReader.GetInstruction(symbols.Body.Instructions, (int)iteratorScope.Offset + (int)iteratorScope.Length + 1) // alternative end
							));
					}
			    }

				// Using info
				pdbSymbols.MethodWhoseUsingInfoAppliesToThisMethod = symbolReaderResolver.LookupMethod (new MetadataToken (function.tokenOfMethodWhoseUsingInfoAppliesToThisMethod));

				// Note: since we don't generate scopes from PDB, we use first scope and ignore custom data stored in function.usedNamespaces for now
			    if (function.scopes.Length > 0 && function.scopes [0].usedNamespaces.Length > 0) {
			        pdbSymbols.UsedNamespaces = new Collection<string> (function.scopes[0].usedNamespaces);
			    }
			    if (function.usingCounts != null) {
					pdbSymbols.UsingCounts = new Collection<ushort>(function.usingCounts);
				}

				// Store asyncMethodInfo
				if (function.synchronizationInformation != null) {
					pdbSymbols.SynchronizationInformation = new PdbSynchronizationInformation {
						KickoffMethod = symbolReaderResolver.LookupMethod (new MetadataToken (function.synchronizationInformation.kickoffMethodToken)),
						GeneratedCatchHandlerIlOffset = function.synchronizationInformation.generatedCatchHandlerIlOffset,
					};
					if (function.synchronizationInformation.synchronizationPoints != null) {
						pdbSymbols.SynchronizationInformation.SynchronizationPoints = new Collection<PdbSynchronizationPoint> ();
						foreach (var synchronizationPoint in function.synchronizationInformation.synchronizationPoints) {
							pdbSymbols.SynchronizationInformation.SynchronizationPoints.Add (new PdbSynchronizationPoint {
								SynchronizeOffset = synchronizationPoint.synchronizeOffset,
								ContinuationMethod = symbolReaderResolver.LookupMethod(new MetadataToken(synchronizationPoint.continuationMethodToken)),
								ContinuationOffset = synchronizationPoint.continuationOffset,
							});
						}
					}
				}
			}
		}


		static void ReadScopeAndLocals (PdbScope [] scopes, ScopeSymbol parent, MethodSymbols symbols)
		{
			foreach (PdbScope scope in scopes)
				ReadScopeAndLocals (scope, parent, symbols);
		}

		static void ReadScopeAndLocals (PdbScope scope, ScopeSymbol parent, MethodSymbols symbols)
		{
			if (scope == null)
				return;

			ScopeSymbol s = new ScopeSymbol ();
			s.start = (int) scope.offset;
			s.end   = (int)(scope.offset + scope.length);

			if (parent != null)
				parent.Scopes.Add (s);
			else if (symbols.scope == null)
				symbols.scope = s;
			else
				throw new InvalidDataException () ;

			foreach (PdbSlot slot in scope.slots) {
				int index = (int) slot.slot;
				if (index < 0 || index >= symbols.Variables.Count)
					continue;

				VariableDefinition variable = symbols.Variables [index];
				variable.Name = slot.name;

				s.Variables.Add (variable);
			}

			ReadScopeAndLocals (scope.scopes, s, symbols);
		}

		void ReadSequencePoints (PdbFunction function, MethodSymbols symbols)
		{
			if (function.lines == null)
				return;

			foreach (PdbLines lines in function.lines)
				ReadLines (lines, symbols);
		}

		void ReadLines (PdbLines lines, MethodSymbols symbols)
		{
			for (int i = 0; i < lines.lines.Length; i++) {
				var line = lines.lines [i];

				symbols.Instructions.Add (new InstructionSymbol ((int) line.offset, new SequencePoint (GetDocument (lines.file)) {
					StartLine = (int) line.lineBegin,
					StartColumn = (int) line.colBegin,
					EndLine = (int) line.lineEnd,
					EndColumn = (int) line.colEnd,
				}));
			}
		}

		public void Dispose ()
		{
			pdb_file.Close ();
		}
	}
}
