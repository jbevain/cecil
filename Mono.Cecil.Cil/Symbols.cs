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
using System.Runtime.InteropServices;
using SR = System.Reflection;

using Mono.Collections.Generic;

namespace Mono.Cecil.Cil {

	[StructLayout (LayoutKind.Sequential)]
	public struct ImageDebugDirectory {
		public int Characteristics;
		public int TimeDateStamp;
		public short MajorVersion;
		public short MinorVersion;
		public int Type;
		public int SizeOfData;
		public int AddressOfRawData;
		public int PointerToRawData;
	}

	public sealed class ScopeDebugInformation : DebugInformation {

		internal InstructionOffset start;
		internal InstructionOffset end;
		internal ImportDebugInformation import;
		internal Collection<ScopeDebugInformation> scopes;
		internal Collection<VariableDebugInformation> variables;
		internal Collection<ConstantDebugInformation> constants;

		public InstructionOffset Start {
			get { return start; }
			set { start = value; }
		}

		public InstructionOffset End {
			get { return end; }
			set { end = value; }
		}

		public ImportDebugInformation Import {
			get { return import; }
			set { import = value; }
		}

		public bool HasScopes {
			get { return !scopes.IsNullOrEmpty (); }
		}

		public Collection<ScopeDebugInformation> Scopes {
			get { return scopes ?? (scopes = new Collection<ScopeDebugInformation> ()); }
		}

		public bool HasVariables {
			get { return !variables.IsNullOrEmpty (); }
		}

		public Collection<VariableDebugInformation> Variables {
			get { return variables ?? (variables = new Collection<VariableDebugInformation> ()); }
		}

		public bool HasConstants {
			get { return !constants.IsNullOrEmpty (); }
		}

		public Collection<ConstantDebugInformation> Constants {
			get { return constants ?? (constants = new Collection<ConstantDebugInformation> ()); }
		}

		internal ScopeDebugInformation ()
		{
			this.token = new MetadataToken (TokenType.LocalScope);
		}

		public ScopeDebugInformation (Instruction start, Instruction end)
			: this ()
		{
			if (start == null)
				throw new ArgumentNullException ("start");

			this.start = new InstructionOffset (start);

			if (end != null)
				this.end = new InstructionOffset (end);
		}

		public bool TryGetName (VariableDefinition variable, out string name)
		{
			name = null;
			if (variables == null || variables.Count == 0)
				return false;

			for (int i = 0; i < variables.Count; i++) {
				if (variables [i].Index == variable.Index) {
					name = variables [i].Name;
					return true;
				}
			}

			return false;
		}
	}

	public struct InstructionOffset {

		readonly Instruction instruction;
		readonly int? offset;

		public int Offset {
			get {
				if (instruction != null)
					return instruction.Offset;
				if (offset.HasValue)
					return offset.Value;

				throw new NotSupportedException ();
			}
		}

		public bool IsEndOfMethod {
			get { return instruction == null && !offset.HasValue; }
		}

		public InstructionOffset (Instruction instruction)
		{
			if (instruction == null)
				throw new ArgumentNullException ("instruction");

			this.instruction = instruction;
			this.offset = null;
		}

		public InstructionOffset (int offset)
		{
			this.instruction = null;
			this.offset = offset;
		}
	}

	[Flags]
	public enum VariableAttributes : ushort {
		None = 0,
		DebuggerHidden = 1,
	}

	public struct VariableIndex {
		readonly VariableDefinition variable;
		readonly int? index;

		public int Index {
			get {
				if (variable != null)
					return variable.Index;
				if (index.HasValue)
					return index.Value;

				throw new NotSupportedException ();
			}
		}

		public VariableIndex (VariableDefinition variable)
		{
			if (variable == null)
				throw new ArgumentNullException ("variable");

			this.variable = variable;
			this.index = null;
		}

		public VariableIndex (int index)
		{
			this.variable = null;
			this.index = index;
		}
	}

	public abstract class DebugInformation : ICustomDebugInformationProvider {

		internal MetadataToken token;
		internal Collection<CustomDebugInformation> custom_infos;

		public MetadataToken MetadataToken {
			get { return token; }
			set { token = value; }
		}

		public bool HasCustomDebugInformations {
			get { return !custom_infos.IsNullOrEmpty (); }
		}

		public Collection<CustomDebugInformation> CustomDebugInformations {
			get { return custom_infos ?? (custom_infos = new Collection<CustomDebugInformation> ()); }
		}

		internal DebugInformation ()
		{
		}
	}

	public sealed class VariableDebugInformation : DebugInformation {

		string name;
		ushort attributes;
		internal VariableIndex index;

		public int Index {
			get { return index.Index; }
		}

		public string Name {
			get { return name; }
			set { name = value; }
		}

		public VariableAttributes Attributes {
			get { return (VariableAttributes) attributes; }
			set { attributes = (ushort) value; }
		}

		public bool IsDebuggerHidden {
			get { return attributes.GetAttributes ((ushort) VariableAttributes.DebuggerHidden); }
			set { attributes = attributes.SetAttributes ((ushort) VariableAttributes.DebuggerHidden, value); }
		}

		internal VariableDebugInformation (int index, string name)
		{
			if (name == null)
				throw new ArgumentNullException ("name");

			this.index = new VariableIndex (index);
			this.name = name;
		}

		public VariableDebugInformation (VariableDefinition variable, string name)
		{
			if (variable == null)
				throw new ArgumentNullException ("variable");
			if (name == null)
				throw new ArgumentNullException ("name");

			this.index = new VariableIndex (variable);
			this.name = name;
			this.token = new MetadataToken (TokenType.LocalVariable);
		}
	}

	public sealed class ConstantDebugInformation : DebugInformation {

		string name;
		TypeReference constant_type;
		object value;

		public string Name {
			get { return name; }
			set { name = value; }
		}

		public TypeReference ConstantType {
			get { return constant_type; }
			set { constant_type = value; }
		}

		public object Value {
			get { return value; }
			set { this.value = value; }
		}

		public ConstantDebugInformation (string name, TypeReference constant_type, object value)
		{
			if (name == null)
				throw new ArgumentNullException ("name");

			this.name = name;
			this.constant_type = constant_type;
			this.value = value;
			this.token = new MetadataToken (TokenType.LocalConstant);
		}
	}

	public enum ImportTargetKind : byte {
		ImportNamespace = 1,
		ImportNamespaceInAssembly = 2,
		ImportType = 3,
		ImportXmlNamespaceWithAlias = 4,
		ImportAlias = 5,
		DefineAssemblyAlias = 6,
		DefineNamespaceAlias = 7,
		DefineNamespaceInAssemblyAlias = 8,
		DefineTypeAlias = 9,
	}

	public sealed class ImportTarget {

		internal ImportTargetKind kind;

		internal string @namespace;
		internal TypeReference type;
		internal AssemblyNameReference reference;
		internal string alias;

		public string Namespace {
			get { return @namespace; }
			set { @namespace = value; }
		}

		public TypeReference Type {
			get { return type; }
			set { type = value; }
		}

		public AssemblyNameReference AssemblyReference {
			get { return reference; }
			set { reference = value; }
		}

		public string Alias {
			get { return alias; }
			set { alias = value; }
		}

		public ImportTargetKind Kind {
			get { return kind; }
			set { kind = value; }
		}

		public ImportTarget (ImportTargetKind kind)
		{
			this.kind = kind;
		}
	}

	public sealed class ImportDebugInformation : DebugInformation {

		internal ImportDebugInformation parent;
		internal Collection<ImportTarget> targets;

		public bool HasTargets {
			get { return !targets.IsNullOrEmpty (); }
		}

		public Collection<ImportTarget> Targets {
			get { return targets ?? (targets = new Collection<ImportTarget> ()); }
		}

		public ImportDebugInformation Parent {
			get { return parent; }
			set { parent = value; }
		}

		public ImportDebugInformation ()
		{
			this.token = new MetadataToken (TokenType.ImportScope);
		}
	}

	interface ICustomDebugInformationProvider : IMetadataTokenProvider {
		bool HasCustomDebugInformations { get; }
		Collection<CustomDebugInformation> CustomDebugInformations { get; }
	}

	public enum CustomDebugInformationKind {
		Binary,
		StateMachineScope,
		DynamicVariable,
		DefaultNamespace,
		AsyncMethodBody,
	}

	public abstract class CustomDebugInformation : DebugInformation {

		Guid identifier;

		public Guid Identifier { get { return identifier; } }
		
		public abstract CustomDebugInformationKind Kind { get; }

		internal CustomDebugInformation (Guid identifier)
		{
			this.identifier = identifier;
			this.token = new MetadataToken (TokenType.CustomDebugInformation);
		}
	}

	public sealed class BinaryCustomDebugInformation : CustomDebugInformation {

		byte [] data;

		public byte [] Data {
			get { return data; }
			set { data = value; }
		}

		public override CustomDebugInformationKind Kind {
			get { return CustomDebugInformationKind.Binary; }
		}

		public BinaryCustomDebugInformation (Guid identifier, byte [] data)
			: base (identifier)
		{
			this.data = data;
		}
	}

	public sealed class AsyncMethodBodyDebugInformation : CustomDebugInformation {

		internal InstructionOffset catch_handler;
		internal Collection<InstructionOffset> yields;
		internal Collection<InstructionOffset> resumes;
		internal MethodDefinition move_next;

		public InstructionOffset CatchHandler {
			get { return catch_handler; }
			set { catch_handler = value; }
		}

		public Collection<InstructionOffset> Yields {
			get { return yields ?? (yields = new Collection<InstructionOffset> ()); }
		}

		public Collection<InstructionOffset> Resumes {
			get { return resumes ?? (resumes = new Collection<InstructionOffset> ()); }
		}

		public MethodDefinition MoveNextMethod {
			get { return move_next; }
			set { move_next = value; }
		}

		public override CustomDebugInformationKind Kind {
			get { return CustomDebugInformationKind.AsyncMethodBody; }
		}

		public static Guid KindIdentifier = new Guid ("{54FD2AC5-E925-401A-9C2A-F94F171072F8}");

		internal AsyncMethodBodyDebugInformation (int catchHandler)
			: base (KindIdentifier)
		{
			this.catch_handler = new InstructionOffset (catchHandler);
		}

		public AsyncMethodBodyDebugInformation (Instruction catchHandler)
			: base (KindIdentifier)
		{
			this.catch_handler = new InstructionOffset (catchHandler);
		}
	}

	public sealed class StateMachineScopeDebugInformation : CustomDebugInformation {

		internal InstructionOffset start;
		internal InstructionOffset end;

		public InstructionOffset Start {
			get { return start; }
			set { start = value; }
		}

		public InstructionOffset End {
			get { return end; }
			set { end = value; }
		}

		public override CustomDebugInformationKind Kind {
			get { return CustomDebugInformationKind.StateMachineScope; }
		}

		public static Guid KindIdentifier = new Guid ("{6DA9A61E-F8C7-4874-BE62-68BC5630DF71}");

		internal StateMachineScopeDebugInformation (int start, int end)
			: base (KindIdentifier)
		{
			this.start = new InstructionOffset (start);
			this.end = new InstructionOffset (end);
		}

		public StateMachineScopeDebugInformation (Instruction start, Instruction end)
			: base (KindIdentifier)
		{
			this.start = new InstructionOffset (start);
			this.end = new InstructionOffset (end);
		}
	}

	public sealed class MethodDebugInformation : DebugInformation {

		internal MethodDefinition method;
		internal Collection<SequencePoint> sequence_points;
		internal ScopeDebugInformation scope;
		internal MethodDefinition kickoff_method;
		internal int code_size;
		internal MetadataToken local_var_token;

		public MethodDefinition Method {
			get { return method; }
		}

		public bool HasSequencePoints {
			get { return !sequence_points.IsNullOrEmpty (); }
		}

		public Collection<SequencePoint> SequencePoints {
			get { return sequence_points ?? (sequence_points = new Collection<SequencePoint> ()); }
		}

		public ScopeDebugInformation Scope {
			get { return scope; }
			set { scope = value; }
		}

		public MethodDefinition StateMachineKickOffMethod {
			get { return kickoff_method; }
			set { kickoff_method = value; }
		}

		internal MethodDebugInformation (MethodDefinition method)
		{
			if (method == null)
				throw new ArgumentNullException ("method");

			this.method = method;
			this.token = new MetadataToken (TokenType.MethodDebugInformation, method.MetadataToken.RID);
		}

		public SequencePoint GetSequencePoint (Instruction instruction)
		{
			if (!HasSequencePoints)
				return null;

			for (int i = 0; i < sequence_points.Count; i++)
				if (sequence_points [i].Offset == instruction.Offset)
					return sequence_points [i];

			return null;
		}

		public IDictionary<Instruction, SequencePoint> GetSequencePointMapping ()
		{
			var instruction_mapping = new Dictionary<Instruction, SequencePoint> ();
			if (!HasSequencePoints || !method.HasBody)
				return instruction_mapping;

			var offset_mapping = new Dictionary<int, SequencePoint> (sequence_points.Count);

			for (int i = 0; i < sequence_points.Count; i++)
				offset_mapping.Add (sequence_points [i].Offset, sequence_points [i]);

			var instructions = method.Body.Instructions;

			for (int i = 0; i < instructions.Count; i++) {
				SequencePoint sequence_point;
				if (offset_mapping.TryGetValue (instructions [i].Offset, out sequence_point))
					instruction_mapping.Add (instructions [i], sequence_point);
			}

			return instruction_mapping;
		}

		public IEnumerable<ScopeDebugInformation> GetScopes ()
		{
			if (scope == null)
				return Empty<ScopeDebugInformation>.Array;

			return GetScopes (new[] { scope });
		}

		static IEnumerable<ScopeDebugInformation> GetScopes (IList<ScopeDebugInformation> scopes)
		{
			for (int i = 0; i < scopes.Count; i++) {
				var scope = scopes [i];

				yield return scope;

				if (!scope.HasScopes)
					continue;

				foreach (var sub_scope in GetScopes (scope.Scopes))
					yield return sub_scope;
			}
		}

		public bool TryGetName (VariableDefinition variable, out string name)
		{
			name = null;

			var has_name = false;
			var unique_name = "";

			foreach (var scope in GetScopes ()) {
				string slot_name;
				if (!scope.TryGetName (variable, out slot_name))
					continue;

				if (!has_name) {
					has_name = true;
					unique_name = slot_name;
					continue;
				}

				if (unique_name != slot_name)
					return false;
			}

			name = unique_name;
			return has_name;
		}
	}

	public interface ISymbolReader : IDisposable {

		bool ProcessDebugHeader (ImageDebugDirectory directory, byte [] header);
		MethodDebugInformation Read (MethodDefinition method);
	}

	public interface ISymbolReaderProvider {
#if !PCL
		ISymbolReader GetSymbolReader (ModuleDefinition module, string fileName);
#endif
		ISymbolReader GetSymbolReader (ModuleDefinition module, Stream symbolStream);
	}

#if !PCL
	static class SymbolProvider {

		static readonly string symbol_kind = Type.GetType ("Mono.Runtime") != null ? "Mdb" : "Pdb";

		static SR.AssemblyName GetPlatformSymbolAssemblyName ()
		{
			var cecil_name = typeof (SymbolProvider).GetAssembly ().GetName ();

			var name = new SR.AssemblyName {
				Name = "Mono.Cecil." + symbol_kind,
				Version = cecil_name.Version,
			};

			name.SetPublicKeyToken (cecil_name.GetPublicKeyToken ());

			return name;
		}

		static Type GetPlatformType (string fullname)
		{
			var type = Type.GetType (fullname);
			if (type != null)
				return type;

			var assembly_name = GetPlatformSymbolAssemblyName ();

			type = Type.GetType (fullname + ", " + assembly_name.FullName);
			if (type != null)
				return type;

			try {
				var assembly = SR.Assembly.Load (assembly_name);
				if (assembly != null)
					return assembly.GetType (fullname);
			} catch (FileNotFoundException) {
			} catch (FileLoadException) {
			}

			return null;
		}

		static ISymbolReaderProvider reader_provider;

		public static ISymbolReaderProvider GetPlatformReaderProvider ()
		{
			if (reader_provider != null)
				return reader_provider;

			var type = GetPlatformType (GetProviderTypeName ("ReaderProvider"));
			if (type == null)
				return null;

			return reader_provider = (ISymbolReaderProvider) Activator.CreateInstance (type);
		}

		static string GetProviderTypeName (string name)
		{
			return "Mono.Cecil." + symbol_kind + "." + symbol_kind + name;
		}

#if !READ_ONLY

		static ISymbolWriterProvider writer_provider;

		public static ISymbolWriterProvider GetPlatformWriterProvider ()
		{
			if (writer_provider != null)
				return writer_provider;

			var type = GetPlatformType (GetProviderTypeName ("WriterProvider"));
			if (type == null)
				return null;

			return writer_provider = (ISymbolWriterProvider) Activator.CreateInstance (type);
		}

#endif
	}
#endif

#if !READ_ONLY

	public interface ISymbolWriter : IDisposable {

		bool GetDebugHeader (out ImageDebugDirectory directory, out byte [] header);
		void Write (MethodDebugInformation info);
	}

	public interface ISymbolWriterProvider {

#if !PCL
		ISymbolWriter GetSymbolWriter (ModuleDefinition module, string fileName);
#endif
		ISymbolWriter GetSymbolWriter (ModuleDefinition module, Stream symbolStream);
	}

#endif
}

#if !PCL

namespace Mono.Cecil {

	static partial class Mixin {

		public static string GetPdbFileName (string assemblyFileName)
		{
			return Path.ChangeExtension (assemblyFileName, ".pdb");
		}

		public static string GetMdbFileName (string assemblyFileName)
		{
			return assemblyFileName + ".mdb";
		}
	}
}

#endif