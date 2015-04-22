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
using Mono.Cecil.PE;

namespace Mono.Cecil.Metadata {

	enum Table : byte {
		Module = 0x00,
		TypeRef = 0x01,
		TypeDef = 0x02,
		FieldPtr = 0x03,
		Field = 0x04,
		MethodPtr = 0x05,
		Method = 0x06,
		ParamPtr = 0x07,
		Param = 0x08,
		InterfaceImpl = 0x09,
		MemberRef = 0x0a,
		Constant = 0x0b,
		CustomAttribute = 0x0c,
		FieldMarshal = 0x0d,
		DeclSecurity = 0x0e,
		ClassLayout = 0x0f,
		FieldLayout = 0x10,
		StandAloneSig = 0x11,
		EventMap = 0x12,
		EventPtr = 0x13,
		Event = 0x14,
		PropertyMap = 0x15,
		PropertyPtr = 0x16,
		Property = 0x17,
		MethodSemantics = 0x18,
		MethodImpl = 0x19,
		ModuleRef = 0x1a,
		TypeSpec = 0x1b,
		ImplMap = 0x1c,
		FieldRVA = 0x1d,
		EncLog = 0x1e,
		EncMap = 0x1f,
		Assembly = 0x20,
		AssemblyProcessor = 0x21,
		AssemblyOS = 0x22,
		AssemblyRef = 0x23,
		AssemblyRefProcessor = 0x24,
		AssemblyRefOS = 0x25,
		File = 0x26,
		ExportedType = 0x27,
		ManifestResource = 0x28,
		NestedClass = 0x29,
		GenericParam = 0x2a,
		MethodSpec = 0x2b,
		GenericParamConstraint = 0x2c,
	}

	struct TableInformation {
		public uint Offset;
		public uint Length;
		public uint RowSize;
	}

	sealed class TableHeap : Heap {

		public long Valid;
		public long Sorted;

		public const int TableCount = 45;

		public readonly TableInformation [] Tables = new TableInformation [TableCount];

		public TableInformation this [Table table] {
			get { return Tables [(int) table]; }
		}

		public TableHeap (Section section, uint start, uint size)
			: base (section, start, size)
		{
		}

		public bool HasTable (Table table)
		{
			return (Valid & (1L << (int) table)) != 0;
		}
	}
}
