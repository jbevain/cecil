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

	public class InstructionRange {

		Instruction start;
		Instruction end;

		public Instruction Start {
			get { return start; }
			set { start = value; }
		}

		public Instruction End {
			get { return end; }
			set { end = value; }
		}
	}

	public sealed class Scope : InstructionRange, IVariableDefinitionProvider {

		Collection<Scope> scopes;
		Collection<VariableDefinition> variables;

		public bool HasScopes {
			get { return !scopes.IsNullOrEmpty (); }
		}

		public Collection<Scope> Scopes {
			get {
				if (scopes == null)
					scopes = new Collection<Scope> ();

				return scopes;
			}
		}

		public bool HasVariables {
			get { return !variables.IsNullOrEmpty (); }
		}

		public Collection<VariableDefinition> Variables {
			get {
				if (variables == null)
					variables = new Collection<VariableDefinition> ();

				return variables;
			}
		}
	}

	public class RangeSymbol {

		internal int start;
		internal int end;

		public int Start {
			get { return start; }
		}

		public int End {
			get { return end; }
		}
	}

	public sealed class ScopeSymbol : RangeSymbol, IVariableDefinitionProvider {

		Collection<ScopeSymbol> scopes;
		Collection<VariableDefinition> variables;

		public bool HasScopes {
			get { return !scopes.IsNullOrEmpty (); }
		}

		public Collection<ScopeSymbol> Scopes {
			get {
				if (scopes == null)
					scopes = new Collection<ScopeSymbol> ();

				return scopes;
			}
		}

		public bool HasVariables {
			get { return !variables.IsNullOrEmpty (); }
		}

		public Collection<VariableDefinition> Variables {
			get {
				if (variables == null)
					variables = new Collection<VariableDefinition> ();

				return variables;
			}
		}
	}

	public struct InstructionSymbol {

		public readonly int Offset;
		public readonly SequencePoint SequencePoint;

		public InstructionSymbol (int offset, SequencePoint sequencePoint)
		{
			this.Offset = offset;
			this.SequencePoint = sequencePoint;
		}
	}

	public class MethodSymbols
	{
		internal MethodBody method_body;
		internal int code_size;
		internal MetadataToken original_method_token;
		internal ScopeSymbol scope;
		internal MetadataToken local_var_token;
		internal Collection<VariableDefinition> variables;
		internal Collection<InstructionSymbol> instructions;

		public MethodBody Body {
			get { return method_body; }
			set { method_body = value; }
		}

	    public bool HasVariables {
			get { return !variables.IsNullOrEmpty (); }
		}

		public Collection<VariableDefinition> Variables {
			get {
				if (variables == null)
					variables = new Collection<VariableDefinition> ();

				return variables;
			}
		}

		public Collection<InstructionSymbol> Instructions {
			get {
				if (instructions == null)
					instructions = new Collection<InstructionSymbol> ();

				return instructions;
			}
		}

		public ScopeSymbol Scope {
			get { return scope; }
		}

		public int CodeSize {
			get { return code_size; }
			set { code_size = value; }
		}

		public string MethodName {
			get { return method_body.Method.Name; }
		}

		public MetadataToken OriginalMethodToken {
			get { return original_method_token; }
			set { original_method_token = value; }
		}

		public MetadataToken LocalVarToken {
			get { return local_var_token; }
			set { local_var_token = value; }
		}

	    public virtual MethodSymbols Clone ()
	    {
	        var result = new MethodSymbols (method_body);
	        CloneTo (result);
	        return result;
	    }

	    protected void CloneTo (MethodSymbols symbols)
	    {
	        symbols.method_body = method_body;
	        symbols.code_size = code_size;
	        symbols.original_method_token = original_method_token;
	        symbols.local_var_token = local_var_token;

	        if (variables != null) {
	            symbols.variables = new Collection<VariableDefinition> (variables);
	        }

	        if (instructions != null) {
	            symbols.instructions = new Collection<InstructionSymbol> ();
	            foreach (var instruction in instructions) {
	                symbols.instructions.Add (new InstructionSymbol (instruction.Offset, instruction.SequencePoint));
	            }
	        }
	    }

		public MethodSymbols (MethodBody methodBody)
		{
			this.method_body = methodBody;
		}
	}

	public delegate Instruction InstructionMapper (int offset);

	public interface ISymbolReaderResolver {
		MethodReference LookupMethod(MetadataToken old_token);
	}

	public interface ISymbolReader : IDisposable {

		MethodSymbols Create (MethodBody methodBody);
		bool ProcessDebugHeader (ImageDebugDirectory directory, byte [] header);
		void Read (MethodBody body, InstructionMapper mapper, ISymbolReaderResolver symbolReaderResolver);
		void Read (MethodSymbols symbols, ISymbolReaderResolver symbolReaderResolver);
	}

	public interface ISymbolReaderProvider {

		ISymbolReader GetSymbolReader (ModuleDefinition module, string fileName);
		ISymbolReader GetSymbolReader (ModuleDefinition module, Stream symbolStream);
	}

#if !PCL
	static class SymbolProvider {

		static readonly string symbol_kind = Type.GetType ("Mono.Runtime") != null ? "Mdb" : "Pdb";

		static SR.AssemblyName GetPlatformSymbolAssemblyName ()
		{
			var cecil_name = typeof (SymbolProvider).Assembly.GetName ();

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
		void Write (MethodBody body);
		void Write (MethodSymbols symbols);
	}

	public interface ISymbolWriterProvider {

		ISymbolWriter GetSymbolWriter (ModuleDefinition module, string fileName);
		ISymbolWriter GetSymbolWriter (ModuleDefinition module, Stream symbolStream);
	}

#endif
}
