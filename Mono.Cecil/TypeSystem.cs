//
// TypeSystem.cs
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

using Mono.Cecil.Metadata;

namespace Mono.Cecil {
    public interface ITypeSystem {
        IMetadataScope Corlib { get; }
        ITypeReference Object { get; }
        ITypeReference Void { get; }
        ITypeReference Boolean { get; }
        ITypeReference Char { get; }
        ITypeReference SByte { get; }
        ITypeReference Byte { get; }
        ITypeReference Int16 { get; }
        ITypeReference UInt16 { get; }
        ITypeReference Int32 { get; }
        ITypeReference UInt32 { get; }
        ITypeReference Int64 { get; }
        ITypeReference UInt64 { get; }
        ITypeReference Single { get; }
        ITypeReference Double { get; }
        ITypeReference IntPtr { get; }
        ITypeReference UIntPtr { get; }
        ITypeReference String { get; }
        ITypeReference TypedReference { get; }
        ITypeReference LookupType (string @namespace, string name);
    }

    public abstract class TypeSystem : ITypeSystem {

		sealed class CoreTypeSystem : TypeSystem {

			public CoreTypeSystem (IModuleDefinition module)
				: base (module)
			{
			}

		    public override ITypeReference LookupType (string @namespace, string name)
			{
				var type = LookupTypeDefinition (@namespace, name) ?? LookupTypeForwarded (@namespace, name);
				if (type != null)
					return type;

				throw new NotSupportedException ();
			}

			ITypeReference LookupTypeDefinition (string @namespace, string name)
			{
				var metadata = module.MetadataSystem;
				if (metadata.Types == null)
					Initialize (module.Types);

				return module.Read (new Row<string, string> (@namespace, name), (row, reader) => {
					var types = reader.metadata.Types;

					for (int i = 0; i < types.Length; i++) {
						if (types [i] == null)
							types [i] = reader.GetTypeDefinition ((uint) i + 1);

						var type = types [i];

						if (type.Name == row.Col2 && type.Namespace == row.Col1)
							return type;
					}

					return null;
				});
			}

			ITypeReference LookupTypeForwarded (string @namespace, string name)
			{
				if (!module.HasExportedTypes)
					return null;

				var exported_types = module.ExportedTypes;
				for (int i = 0; i < exported_types.Count; i++) {
					var exported_type = exported_types [i];

					if (exported_type.Name == name && exported_type.Namespace == @namespace)
						return exported_type.CreateReference ();
				}

				return null;
			}

			static void Initialize (object obj)
			{
			}
		}

		sealed class CommonTypeSystem : TypeSystem {

			IAssemblyNameReference corlib;

			public CommonTypeSystem (IModuleDefinition module)
				: base (module)
			{
			}

		    public override ITypeReference LookupType (string @namespace, string name)
			{
				return CreateTypeReference (@namespace, name);
			}

			public IAssemblyNameReference GetCorlibReference ()
			{
				if (corlib != null)
					return corlib;

				const string mscorlib = "mscorlib";

				var references = module.AssemblyReferences;

				for (int i = 0; i < references.Count; i++) {
					var reference = references [i];
					if (reference.Name == mscorlib)
						return corlib = reference;
				}

				corlib = new AssemblyNameReference {
					Name = mscorlib,
					Version = GetCorlibVersion (),
					PublicKeyToken = new byte [] { 0xb7, 0x7a, 0x5c, 0x56, 0x19, 0x34, 0xe0, 0x89 },
				};

				references.Add (corlib);

				return corlib;
			}

			Version GetCorlibVersion ()
			{
				switch (module.Runtime) {
				case TargetRuntime.Net_1_0:
				case TargetRuntime.Net_1_1:
					return new Version (1, 0, 0, 0);
				case TargetRuntime.Net_2_0:
					return new Version (2, 0, 0, 0);
				case TargetRuntime.Net_4_0:
					return new Version (4, 0, 0, 0);
				default:
					throw new NotSupportedException ();
				}
			}

			TypeReference CreateTypeReference (string @namespace, string name)
			{
				return new TypeReference (@namespace, name, module, GetCorlibReference ());
			}
		}

		readonly IModuleDefinition module;

		ITypeReference type_object;
		ITypeReference type_void;
		ITypeReference type_bool;
		ITypeReference type_char;
		ITypeReference type_sbyte;
		ITypeReference type_byte;
		ITypeReference type_int16;
		ITypeReference type_uint16;
		ITypeReference type_int32;
		ITypeReference type_uint32;
		ITypeReference type_int64;
		ITypeReference type_uint64;
		ITypeReference type_single;
		ITypeReference type_double;
		ITypeReference type_intptr;
		ITypeReference type_uintptr;
		ITypeReference type_string;
		ITypeReference type_typedref;

		TypeSystem (IModuleDefinition module)
		{
			this.module = module;
		}

		internal static TypeSystem CreateTypeSystem (IModuleDefinition module)
		{
			if (module.IsCorlib ())
				return new CoreTypeSystem (module);

			return new CommonTypeSystem (module);
		}

		public abstract ITypeReference LookupType (string @namespace, string name);

		ITypeReference LookupSystemType (ref ITypeReference reference, string name, ElementType element_type)
		{
			lock (module.SyncRoot) {
				if (reference != null)
					return reference;
				var type = LookupType ("System", name);
				type.EType = element_type;
				return reference = type;
			}
		}

		ITypeReference LookupSystemValueType (ref ITypeReference typeRef, string name, ElementType element_type)
		{
			lock (module.SyncRoot) {
				if (typeRef != null)
					return typeRef;
				var type = LookupType ("System", name);
				type.EType = element_type;
				type.IsValueType = true;
				return typeRef = type;
			}
		}

		public IMetadataScope Corlib {
			get {
				var common = this as CommonTypeSystem;
				if (common == null)
					return module;

				return common.GetCorlibReference ();
			}
		}

		public ITypeReference Object {
			get { return type_object ?? (LookupSystemType (ref type_object, "Object", ElementType.Object)); }
		}

		public ITypeReference Void {
			get { return type_void ?? (LookupSystemType (ref type_void, "Void", ElementType.Void)); }
		}

		public ITypeReference Boolean {
			get { return type_bool ?? (LookupSystemValueType (ref type_bool, "Boolean", ElementType.Boolean)); }
		}

		public ITypeReference Char {
			get { return type_char ?? (LookupSystemValueType (ref type_char, "Char", ElementType.Char)); }
		}

		public ITypeReference SByte {
			get { return type_sbyte ?? (LookupSystemValueType (ref type_sbyte, "SByte", ElementType.I1)); }
		}

		public ITypeReference Byte {
			get { return type_byte ?? (LookupSystemValueType (ref type_byte, "Byte", ElementType.U1)); }
		}

		public ITypeReference Int16 {
			get { return type_int16 ?? (LookupSystemValueType (ref type_int16, "Int16", ElementType.I2)); }
		}

		public ITypeReference UInt16 {
			get { return type_uint16 ?? (LookupSystemValueType (ref type_uint16, "UInt16", ElementType.U2)); }
		}

		public ITypeReference Int32 {
			get { return type_int32 ?? (LookupSystemValueType (ref type_int32, "Int32", ElementType.I4)); }
		}

		public ITypeReference UInt32 {
			get { return type_uint32 ?? (LookupSystemValueType (ref type_uint32, "UInt32", ElementType.U4)); }
		}

		public ITypeReference Int64 {
			get { return type_int64 ?? (LookupSystemValueType (ref type_int64, "Int64", ElementType.I8)); }
		}

		public ITypeReference UInt64 {
			get { return type_uint64 ?? (LookupSystemValueType (ref type_uint64, "UInt64", ElementType.U8)); }
		}

		public ITypeReference Single {
			get { return type_single ?? (LookupSystemValueType (ref type_single, "Single", ElementType.R4)); }
		}

		public ITypeReference Double {
			get { return type_double ?? (LookupSystemValueType (ref type_double, "Double", ElementType.R8)); }
		}

		public ITypeReference IntPtr {
			get { return type_intptr ?? (LookupSystemValueType (ref type_intptr, "IntPtr", ElementType.I)); }
		}

		public ITypeReference UIntPtr {
			get { return type_uintptr ?? (LookupSystemValueType (ref type_uintptr, "UIntPtr", ElementType.U)); }
		}

		public ITypeReference String {
			get { return type_string ?? (LookupSystemType (ref type_string, "String", ElementType.String)); }
		}

		public ITypeReference TypedReference {
			get { return type_typedref ?? (LookupSystemValueType (ref type_typedref, "TypedReference", ElementType.TypedByRef)); }
		}
	}
}
