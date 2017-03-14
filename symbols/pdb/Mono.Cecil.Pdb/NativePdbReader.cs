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

	public class NativePdbReader : ISymbolReader {

		int age;
		Guid guid;

		readonly Disposable<Stream> pdb_file;
		readonly Dictionary<string, Document> documents = new Dictionary<string, Document> ();
		readonly Dictionary<uint, PdbFunction> functions = new Dictionary<uint, PdbFunction> ();

		internal NativePdbReader (Disposable<Stream> file)
		{
			this.pdb_file = file;
		}

		/*
		uint Magic = 0x53445352;
		Guid Signature;
		uint Age;
		string FileName;
		 */

		public bool ProcessDebugHeader (ImageDebugHeader header)
		{
			if (!header.HasEntries)
				return false;

			var entry = header.GetCodeViewEntry ();
			if (entry == null)
				return false;

			var directory = entry.Directory;

			if (directory.Type != ImageDebugType.CodeView)
				return false;
			if (directory.MajorVersion != 0 || directory.MinorVersion != 0)
				return false;

			var data = entry.Data;

			if (data.Length < 24)
				return false;

			var magic = ReadInt32 (data, 0);
			if (magic != 0x53445352)
				return false;

			var guid_bytes = new byte [16];
			Buffer.BlockCopy (data, 4, guid_bytes, 0, 16);

			this.guid = new Guid (guid_bytes);
			this.age = ReadInt32 (data, 20);

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

			if (!function.scopes.IsNullOrEmpty())
				symbol.scope = ReadScopeAndLocals (function.scopes [0], symbol);
			else
				symbol.scope = new ScopeDebugInformation { Start = new InstructionOffset(0), End = new InstructionOffset((int)function.length) };

			if (function.tokenOfMethodWhoseUsingInfoAppliesToThisMethod != method.MetadataToken.ToUInt32 () && function.tokenOfMethodWhoseUsingInfoAppliesToThisMethod != 0) {
				var methodWhoseUsingInfoAppliesToThisMethod = (MethodDefinition)method.Module.LookupToken((int)function.tokenOfMethodWhoseUsingInfoAppliesToThisMethod);
				if (methodWhoseUsingInfoAppliesToThisMethod != null && methodWhoseUsingInfoAppliesToThisMethod.DebugInformation.Scope != null)
					symbol.scope.Import = methodWhoseUsingInfoAppliesToThisMethod.DebugInformation.Scope.Import;
			}

			if (function.scopes.Length > 1) {
				for (int i = 1; i < function.scopes.Length; i++) {
					var s = ReadScopeAndLocals (function.scopes [i], symbol);
					if (!AddScope (symbol.scope.Scopes, s))
						symbol.scope.Scopes.Add (s);
				}
			}

			if (function.iteratorScopes != null) {
				foreach (var iteratorScope in function.iteratorScopes) {
					symbol.CustomDebugInformations.Add (new StateMachineScopeDebugInformation ((int)iteratorScope.Offset, (int)iteratorScope.Offset + (int)iteratorScope.Length + 1));
				}
			}

			if (function.synchronizationInformation != null) {
				var asyncDebugInfo = new AsyncMethodBodyDebugInformation ((int)function.synchronizationInformation.GeneratedCatchHandlerOffset);

				foreach (var synchronizationPoint in function.synchronizationInformation.synchronizationPoints) {
					asyncDebugInfo.Yields.Add (new InstructionOffset ((int)synchronizationPoint.SynchronizeOffset));
					asyncDebugInfo.Resumes.Add (new InstructionOffset ((int)synchronizationPoint.ContinuationOffset));
				}

				symbol.CustomDebugInformations.Add (asyncDebugInfo);

				asyncDebugInfo.MoveNextMethod = method;
				symbol.StateMachineKickOffMethod = (MethodDefinition)method.Module.LookupToken((int)function.synchronizationInformation.kickoffMethodToken);
			}

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
					if (slot.flags == 1) // parameter names
						continue;

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
					var type = (TypeReference) info.method.Module.LookupToken ((int) constant.token);
					var value = constant.value;

					// Object "null" is encoded as integer
					if (type != null && !type.IsValueType && value is int && (int)value == 0)
						value = null;

					parent.constants.Add(new ConstantDebugInformation(
						constant.name,
						type,
						value));
				}
			}

			if (!scope.usedNamespaces.IsNullOrEmpty()) {
				parent.import = new ImportDebugInformation();

				foreach (var usedNamespace in scope.usedNamespaces) {
					var usedNamespaceKind = usedNamespace[0];
					switch (usedNamespaceKind) {
						case 'U':
							parent.import.Targets.Add(new ImportTarget(ImportTargetKind.ImportNamespace) { @namespace = usedNamespace.Substring(1) });
							break;
						case 'T':
						{
							var type = info.Method.Module.GetType(usedNamespace.Substring(1), true);
							if (type != null)
								parent.import.Targets.Add(new ImportTarget(ImportTargetKind.ImportType) {type = type});
							break;
						}
						case 'A':
						{
							var aliasSplit = usedNamespace.IndexOf(' ');
							var alias = usedNamespace.Substring(1, aliasSplit - 1);
							var aliasTarget = usedNamespace.Substring(aliasSplit + 2);
							switch (usedNamespace[aliasSplit + 1])
							{
								case 'U':
									parent.import.Targets.Add(new ImportTarget(ImportTargetKind.DefineNamespaceAlias) { alias = alias, @namespace = aliasTarget });
									break;
								case 'T':
									var type = info.Method.Module.GetType(aliasTarget, true);
									if (type != null)
										parent.import.Targets.Add(new ImportTarget(ImportTargetKind.DefineTypeAlias) { alias = alias, type = type });
									break;
							}
							break;
						}
					}
				}
			}

			parent.scopes = ReadScopeAndLocals (scope.scopes, info);

			return parent;
		}

		static bool AddScope (Collection<ScopeDebugInformation> scopes, ScopeDebugInformation scope)
		{
			foreach (var sub_scope in scopes) {
				if (sub_scope.HasScopes && AddScope (sub_scope.Scopes, scope))
					return true;

				if (scope.Start.Offset >= sub_scope.Start.Offset && scope.End.Offset <= sub_scope.End.Offset) {
					sub_scope.Scopes.Add (scope);
					return true;
				}
			}

			return false;
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
