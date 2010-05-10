//
// TypeSystem.cs
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

using Mono.Cecil.Metadata;

namespace Mono.Cecil {

	abstract class TypeSystem {

		sealed class CorlibTypeSystem : TypeSystem {

			public CorlibTypeSystem (ModuleDefinition module)
				: base (module)
			{
			}

			public override TypeReference LookupType (string @namespace, string name)
			{
				var types = module.MetadataSystem.Types;

				for (int i = 0; i < types.Length; i++) {
					var type = types [i];
					if (type == null)
						continue;

					if (type.Name == name && type.Namespace == @namespace)
						return type;
				}

				return null;
			}
		}

		sealed class CommonTypeSystem : TypeSystem {

			AssemblyNameReference corlib;

			public CommonTypeSystem (ModuleDefinition module)
				: base (module)
			{
			}

			public override TypeReference LookupType (string @namespace, string name)
			{
				return CreateTypeReference (@namespace, name);
			}

			public AssemblyNameReference GetCorlibReference ()
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
				var type = new TypeReference (@namespace, name, GetCorlibReference ());
				type.module = module;
				return type;
			}
		}

		readonly ModuleDefinition module;

		TypeReference type_object;
		TypeReference type_void;
		TypeReference type_bool;
		TypeReference type_char;
		TypeReference type_sbyte;
		TypeReference type_byte;
		TypeReference type_int16;
		TypeReference type_uint16;
		TypeReference type_int32;
		TypeReference type_uint32;
		TypeReference type_int64;
		TypeReference type_uint64;
		TypeReference type_single;
		TypeReference type_double;
		TypeReference type_intptr;
		TypeReference type_uintptr;
		TypeReference type_string;
		TypeReference type_typedref;

		protected TypeSystem (ModuleDefinition module)
		{
			this.module = module;
		}

		public static TypeSystem CreateTypeSystem (ModuleDefinition module)
		{
			if (IsCorlib (module))
				return new CorlibTypeSystem (module);

			return new CommonTypeSystem (module);
		}

		static bool IsCorlib (ModuleDefinition module)
		{
			if (module.Assembly == null)
				return false;

			return module.Assembly.Name.Name == "mscorlib";
		}

		public abstract TypeReference LookupType (string @namespace, string name);

		TypeReference LookupSystemType (string name, ElementType element_type)
		{
			var type = LookupType ("System", name);
			type.etype = element_type;
			return type;
		}

		TypeReference LookupSystemValueType (string name, ElementType element_type)
		{
			var type = LookupSystemType (name, element_type);
			type.IsValueType = true;
			return type;
		}

		public IMetadataScope Corlib {
			get {
				var common = this as CommonTypeSystem;
				if (common == null)
					return module;

				return common.GetCorlibReference ();
			}
		}

		public TypeReference Object {
			get {
				if (type_object == null)
					type_object = LookupSystemType ("Object", ElementType.Object);

				return type_object;
			}
		}

		public TypeReference Void {
			get {
				if (type_void == null)
					type_void = LookupSystemType ("Void", ElementType.Void);

				return type_void;
			}
		}

		public TypeReference Boolean {
			get {
				if (type_bool == null)
					type_bool = LookupSystemValueType ("Boolean", ElementType.Boolean);

				return type_bool;
			}
		}

		public TypeReference Char {
			get {
				if (type_char == null)
					type_char = LookupSystemValueType ("Char", ElementType.Char);

				return type_char;
			}
		}

		public TypeReference SByte {
			get {
				if (type_sbyte == null)
					type_sbyte = LookupSystemValueType ("SByte", ElementType.I1);

				return type_sbyte;
			}
		}

		public TypeReference Byte {
			get {
				if (type_byte == null)
					type_byte = LookupSystemValueType ("Byte", ElementType.U1);

				return type_byte;
			}
		}

		public TypeReference Int16 {
			get {
				if (type_int16 == null)
					type_int16 = LookupSystemValueType ("Int16", ElementType.I2);

				return type_int16;
			}
		}

		public TypeReference UInt16 {
			get {
				if (type_uint16 == null)
					type_uint16 = LookupSystemValueType ("UInt16", ElementType.U2);

				return type_uint16;
			}
		}

		public TypeReference Int32 {
			get {
				if (type_int32 == null)
					type_int32 = LookupSystemValueType ("Int32", ElementType.I4);

				return type_int32;
			}
		}

		public TypeReference UInt32 {
			get {
				if (type_uint32 == null)
					type_uint32 = LookupSystemValueType ("UInt32", ElementType.U4);

				return type_uint32;
			}
		}

		public TypeReference Int64 {
			get {
				if (type_int64 == null)
					type_int64 = LookupSystemValueType ("Int64", ElementType.I8);

				return type_int64;
			}
		}

		public TypeReference UInt64 {
			get {
				if (type_uint64 == null)
					type_uint64 = LookupSystemValueType ("UInt64", ElementType.U8);

				return type_uint64;
			}
		}

		public TypeReference Single {
			get {
				if (type_single == null)
					type_single = LookupSystemValueType ("Single", ElementType.R4);

				return type_single;
			}
		}

		public TypeReference Double {
			get {
				if (type_double == null)
					type_double = LookupSystemValueType ("Double", ElementType.R8);

				return type_double;
			}
		}

		public TypeReference IntPtr {
			get {
				if (type_intptr == null)
					type_intptr = LookupSystemValueType ("IntPtr", ElementType.I);

				return type_intptr;
			}
		}

		public TypeReference UIntPtr {
			get {
				if (type_uintptr == null)
					type_uintptr = LookupSystemValueType ("UIntPtr", ElementType.U);

				return type_uintptr;
			}
		}

		public TypeReference String {
			get {
				if (type_string == null)
					type_string = LookupSystemType ("String", ElementType.String);

				return type_string;
			}
		}

		public TypeReference TypedReference {
			get {
				if (type_typedref == null)
					type_typedref = LookupSystemValueType ("TypedReference", ElementType.TypedByRef);

				return type_typedref;
			}
		}
	}
}
