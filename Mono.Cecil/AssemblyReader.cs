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
using System.IO.Compression;
using System.Text;

using Mono.Collections.Generic;
using Mono.Cecil.Cil;
using Mono.Cecil.Metadata;
using Mono.Cecil.PE;

using RVA = System.UInt32;

namespace Mono.Cecil {

	abstract class ModuleReader {

		readonly protected ModuleDefinition module;

		protected ModuleReader (Image image, ReadingMode mode)
		{
			this.module = new ModuleDefinition (image);
			this.module.ReadingMode = mode;
		}

		protected abstract void ReadModule ();
		public abstract void ReadSymbols (ModuleDefinition module);

		protected void ReadModuleManifest (MetadataReader reader)
		{
			reader.Populate (module);

			ReadAssembly (reader);
		}

		void ReadAssembly (MetadataReader reader)
		{
			var name = reader.ReadAssemblyNameDefinition ();
			if (name == null) {
				module.kind = ModuleKind.NetModule;
				return;
			}

			var assembly = new AssemblyDefinition ();
			assembly.Name = name;

			module.assembly = assembly;
			assembly.main_module = module;
		}

		public static ModuleDefinition CreateModule (Image image, ReaderParameters parameters)
		{
			var reader = CreateModuleReader (image, parameters.ReadingMode);
			var module = reader.module;

			if (parameters.assembly_resolver != null)
				module.assembly_resolver = Disposable.NotOwned (parameters.assembly_resolver);

			if (parameters.metadata_resolver != null)
				module.metadata_resolver = parameters.metadata_resolver;

			if (parameters.metadata_importer_provider != null)
				module.metadata_importer = parameters.metadata_importer_provider.GetMetadataImporter (module);

			if (parameters.reflection_importer_provider != null)
				module.reflection_importer = parameters.reflection_importer_provider.GetReflectionImporter (module);

			GetMetadataKind (module, parameters);

			reader.ReadModule ();

			ReadSymbols (module, parameters);

			reader.ReadSymbols (module);

			if (parameters.ReadingMode == ReadingMode.Immediate)
				module.MetadataSystem.Clear ();

			return module;
		}

		static void ReadSymbols (ModuleDefinition module, ReaderParameters parameters)
		{
			var symbol_reader_provider = parameters.SymbolReaderProvider;

			if (symbol_reader_provider == null && parameters.ReadSymbols)
				symbol_reader_provider = new DefaultSymbolReaderProvider ();

			if (symbol_reader_provider != null) {
				module.SymbolReaderProvider = symbol_reader_provider;

				var reader = parameters.SymbolStream != null
					? symbol_reader_provider.GetSymbolReader (module, parameters.SymbolStream)
					: symbol_reader_provider.GetSymbolReader (module, module.FileName);

				if (reader != null) {
					try {
						module.ReadSymbols (reader, parameters.ThrowIfSymbolsAreNotMatching);
					} catch (Exception) {
						reader.Dispose ();
						throw;
					}
				}
			}

			if (module.Image.HasDebugTables ())
				module.ReadSymbols (new PortablePdbReader (module.Image, module));
		}

		static void GetMetadataKind (ModuleDefinition module, ReaderParameters parameters)
		{
			if (!parameters.ApplyWindowsRuntimeProjections) {
				module.MetadataKind = MetadataKind.Ecma335;
				return;
			}

			var runtime_version = module.RuntimeVersion;

			if (!runtime_version.Contains ("WindowsRuntime"))
				module.MetadataKind = MetadataKind.Ecma335;
			else if (runtime_version.Contains ("CLR"))
				module.MetadataKind = MetadataKind.ManagedWindowsMetadata;
			else
				module.MetadataKind = MetadataKind.WindowsMetadata;
		}

		static ModuleReader CreateModuleReader (Image image, ReadingMode mode)
		{
			switch (mode) {
			case ReadingMode.Immediate:
				return new ImmediateModuleReader (image);
			case ReadingMode.Deferred:
				return new DeferredModuleReader (image);
			default:
				throw new ArgumentException ();
			}
		}
	}

	sealed class ImmediateModuleReader : ModuleReader {

		bool resolve_attributes;

		public ImmediateModuleReader (Image image)
			: base (image, ReadingMode.Immediate)
		{
		}

		protected override void ReadModule ()
		{
			this.module.Read (this.module, (module, reader) => {
				ReadModuleManifest (reader);
				ReadModule (module, resolve_attributes: true);
			});
		}

		public void ReadModule (ModuleDefinition module, bool resolve_attributes)
		{
			this.resolve_attributes = resolve_attributes;

			if (module.HasAssemblyReferences)
				Mixin.Read (module.AssemblyReferences);
			if (module.HasResources)
				Mixin.Read (module.Resources);
			if (module.HasModuleReferences)
				Mixin.Read (module.ModuleReferences);
			if (module.HasTypes)
				ReadTypes (module.Types);
			if (module.HasExportedTypes)
				Mixin.Read (module.ExportedTypes);

			ReadCustomAttributes (module);

			var assembly = module.Assembly;
			if (assembly == null)
				return;

			ReadCustomAttributes (assembly);
			ReadSecurityDeclarations (assembly);
		}

		void ReadTypes (Collection<TypeDefinition> types)
		{
			for (int i = 0; i < types.Count; i++)
				ReadType (types [i]);
		}

		void ReadType (TypeDefinition type)
		{
			ReadGenericParameters (type);

			if (type.HasInterfaces)
				ReadInterfaces (type);

			if (type.HasNestedTypes)
				ReadTypes (type.NestedTypes);

			if (type.HasLayoutInfo)
				Mixin.Read (type.ClassSize);

			if (type.HasFields)
				ReadFields (type);

			if (type.HasMethods)
				ReadMethods (type);

			if (type.HasProperties)
				ReadProperties (type);

			if (type.HasEvents)
				ReadEvents (type);

			ReadSecurityDeclarations (type);
			ReadCustomAttributes (type);
		}

		void ReadInterfaces (TypeDefinition type)
		{
			var interfaces = type.Interfaces;

			for (int i = 0; i < interfaces.Count; i++)
				ReadCustomAttributes (interfaces [i]);
		}

		void ReadGenericParameters (IGenericParameterProvider provider)
		{
			if (!provider.HasGenericParameters)
				return;

			var parameters = provider.GenericParameters;

			for (int i = 0; i < parameters.Count; i++) {
				var parameter = parameters [i];

				if (parameter.HasConstraints)
					ReadGenericParameterConstraints (parameter);

				ReadCustomAttributes (parameter);
			}
		}

		void ReadGenericParameterConstraints (GenericParameter parameter)
		{
			var constraints = parameter.Constraints;

			for (int i = 0; i < constraints.Count; i++)
				ReadCustomAttributes (constraints [i]);
		}

		void ReadSecurityDeclarations (ISecurityDeclarationProvider provider)
		{
			if (!provider.HasSecurityDeclarations)
				return;

			var security_declarations = provider.SecurityDeclarations;

			if (!resolve_attributes)
				return;

			for (int i = 0; i < security_declarations.Count; i++) {
				var security_declaration = security_declarations [i];

				Mixin.Read (security_declaration.SecurityAttributes);
			}
		}

		void ReadCustomAttributes (ICustomAttributeProvider provider)
		{
			if (!provider.HasCustomAttributes)
				return;

			var custom_attributes = provider.CustomAttributes;

			if (!resolve_attributes)
				return;

			for (int i = 0; i < custom_attributes.Count; i++) {
				var custom_attribute = custom_attributes [i];

				Mixin.Read (custom_attribute.ConstructorArguments);
			}
		}

		void ReadFields (TypeDefinition type)
		{
			var fields = type.Fields;

			for (int i = 0; i < fields.Count; i++) {
				var field = fields [i];

				if (field.HasConstant)
					Mixin.Read (field.Constant);

				if (field.HasLayoutInfo)
					Mixin.Read (field.Offset);

				if (field.RVA > 0)
					Mixin.Read (field.InitialValue);

				if (field.HasMarshalInfo)
					Mixin.Read (field.MarshalInfo);

				ReadCustomAttributes (field);
			}
		}

		void ReadMethods (TypeDefinition type)
		{
			var methods = type.Methods;

			for (int i = 0; i < methods.Count; i++) {
				var method = methods [i];

				ReadGenericParameters (method);

				if (method.HasParameters)
					ReadParameters (method);

				if (method.HasOverrides)
					Mixin.Read (method.Overrides);

				if (method.IsPInvokeImpl)
					Mixin.Read (method.PInvokeInfo);

				ReadSecurityDeclarations (method);
				ReadCustomAttributes (method);

				var return_type = method.MethodReturnType;
				if (return_type.HasConstant)
					Mixin.Read (return_type.Constant);

				if (return_type.HasMarshalInfo)
					Mixin.Read (return_type.MarshalInfo);

				ReadCustomAttributes (return_type);
			}
		}

		void ReadParameters (MethodDefinition method)
		{
			var parameters = method.Parameters;

			for (int i = 0; i < parameters.Count; i++) {
				var parameter = parameters [i];

				if (parameter.HasConstant)
					Mixin.Read (parameter.Constant);

				if (parameter.HasMarshalInfo)
					Mixin.Read (parameter.MarshalInfo);

				ReadCustomAttributes (parameter);
			}
		}

		void ReadProperties (TypeDefinition type)
		{
			var properties = type.Properties;

			for (int i = 0; i < properties.Count; i++) {
				var property = properties [i];

				Mixin.Read (property.GetMethod);

				if (property.HasConstant)
					Mixin.Read (property.Constant);

				ReadCustomAttributes (property);
			}
		}

		void ReadEvents (TypeDefinition type)
		{
			var events = type.Events;

			for (int i = 0; i < events.Count; i++) {
				var @event = events [i];

				Mixin.Read (@event.AddMethod);

				ReadCustomAttributes (@event);
			}
		}

		public override void ReadSymbols (ModuleDefinition module)
		{
			if (module.symbol_reader == null)
				return;

			ReadTypesSymbols (module.Types, module.symbol_reader);
		}

		void ReadTypesSymbols (Collection<TypeDefinition> types, ISymbolReader symbol_reader)
		{
			for (int i = 0; i < types.Count; i++) {
				var type = types [i];

				if (type.HasNestedTypes)
					ReadTypesSymbols (type.NestedTypes, symbol_reader);

				if (type.HasMethods)
					ReadMethodsSymbols (type, symbol_reader);
			}
		}

		void ReadMethodsSymbols (TypeDefinition type, ISymbolReader symbol_reader)
		{
			var methods = type.Methods;
			for (int i = 0; i < methods.Count; i++) {
				var method = methods [i];

				if (method.HasBody && method.token.RID != 0 && method.debug_info == null)
					method.debug_info = symbol_reader.Read (method);
			}
		}
	}

	sealed class DeferredModuleReader : ModuleReader {

		public DeferredModuleReader (Image image)
			: base (image, ReadingMode.Deferred)
		{
		}

		protected override void ReadModule ()
		{
			this.module.Read (this.module, (_, reader) => ReadModuleManifest (reader));
		}

		public override void ReadSymbols (ModuleDefinition module)
		{
		}
	}

	sealed unsafe class MetadataReader {

		readonly internal Image image;
		readonly internal ModuleDefinition module;
		readonly internal MetadataSystem metadata;

		internal CodeReader code;
		internal IGenericContext context;

		readonly MetadataReader metadata_reader;

		public MetadataReader (ModuleDefinition module)
		{
			this.image = module.Image;
			this.module = module;
			this.metadata = module.MetadataSystem;
			this.code = new CodeReader (this);
		}

		public MetadataReader (Image image, ModuleDefinition module, MetadataReader metadata_reader)
		{
			this.image = image;
			this.module = module;
			this.metadata = module.MetadataSystem;
			this.metadata_reader = metadata_reader;
		}

		int GetCodedIndexSize (CodedIndex index)
		{
			return image.GetCodedIndexSize (index);
		}

		uint ReadByIndexSize (int size, ref PByteBuffer buffer)
		{
			if (size == 4)
				return buffer.ReadUInt32 ();
			else
				return buffer.ReadUInt16 ();
		}

		byte [] ReadBlob (ref PByteBuffer buffer)
		{
			var blob_heap = image.BlobHeap;
			if (blob_heap == null) {
				buffer.Advance (2);
				return Empty<byte>.Array;
			}

			return blob_heap.Read (ReadBlobIndex (ref buffer));
		}

		byte [] ReadBlob (uint signature)
		{
			var blob_heap = image.BlobHeap;
			if (blob_heap == null)
				return Empty<byte>.Array;

			return blob_heap.Read (signature);
		}

		uint ReadBlobIndex (ref PByteBuffer buffer)
		{
			var blob_heap = image.BlobHeap;
			return ReadByIndexSize (blob_heap != null ? blob_heap.IndexSize : 2, ref buffer);
		}

		void GetBlobView (uint signature, out byte [] blob, out int index, out int count)
		{
			var blob_heap = image.BlobHeap;
			if (blob_heap == null) {
				blob = null;
				index = count = 0;
				return;
			}

			blob_heap.GetView (signature, out blob, out index, out count);
		}

		string ReadString (ref PByteBuffer buffer)
		{
			return image.StringHeap.Read (ReadByIndexSize (image.StringHeap.IndexSize, ref buffer));
		}

		uint ReadStringIndex (ref PByteBuffer buffer)
		{
			return ReadByIndexSize (image.StringHeap.IndexSize, ref buffer);
		}

		Guid ReadGuid (ref PByteBuffer buffer)
		{
			return image.GuidHeap.Read (ReadByIndexSize (image.GuidHeap.IndexSize, ref buffer));
		}

		uint ReadTableIndex (Table table, ref PByteBuffer buffer)
		{
			return ReadByIndexSize (image.GetTableIndexSize (table), ref buffer);
		}

		MetadataToken ReadMetadataToken (CodedIndex index, ref PByteBuffer buffer)
		{
			return index.GetMetadataToken (ReadByIndexSize (GetCodedIndexSize (index), ref buffer));
		}

		int MoveTo (Table table, out PByteBuffer buffer)
		{
			var info = image.TableHeap [table];
			if (info.Length != 0) {
				buffer = new PByteBuffer(image.TableHeap.data + info.Offset, info.RowSize * info.Length);
				return (int) info.Length;
			}

			buffer = default;
			return 0;
		}

		bool MoveTo (Table table, uint row, out PByteBuffer buffer)
		{
			var info = image.TableHeap [table];
			var length = info.Length;
			if (length == 0 || row > length) {
				buffer = default;
				return false;
			}

			var end = info.Offset + (info.RowSize * info.Length);
			var start = (info.Offset + (info.RowSize * (row - 1)));
			var size = end - start;

			buffer = new PByteBuffer (image.TableHeap.data + start, size);

			return true;
		}

		public AssemblyNameDefinition ReadAssemblyNameDefinition ()
		{
			if (MoveTo (Table.Assembly, out PByteBuffer buffer) == 0)
				return null;

			var name = new AssemblyNameDefinition ();

			name.HashAlgorithm = (AssemblyHashAlgorithm) buffer.ReadUInt32 ();

			PopulateVersionAndFlags (name, ref buffer);

			name.PublicKey = ReadBlob (ref buffer);

			PopulateNameAndCulture (name, ref buffer);

			return name;
		}

		public ModuleDefinition Populate (ModuleDefinition module)
		{
			if (MoveTo (Table.Module, out PByteBuffer buffer) == 0)
				return module;

			buffer.Advance (2); // Generation

			module.Name = ReadString (ref buffer);
			module.Mvid = ReadGuid (ref buffer);

			return module;
		}

		void InitializeAssemblyReferences ()
		{
			if (metadata.AssemblyReferences != null)
				return;

			int length = MoveTo (Table.AssemblyRef, out PByteBuffer buffer);
			var references = metadata.AssemblyReferences = new AssemblyNameReference [length];

			for (uint i = 0; i < length; i++) {
				var reference = new AssemblyNameReference ();
				reference.token = new MetadataToken (TokenType.AssemblyRef, i + 1);

				PopulateVersionAndFlags (reference, ref buffer);

				var key_or_token = ReadBlob (ref buffer);

				if (reference.HasPublicKey)
					reference.PublicKey = key_or_token;
				else
					reference.PublicKeyToken = key_or_token;

				PopulateNameAndCulture (reference, ref buffer);

				reference.Hash = ReadBlob (ref buffer);

				references [i] = reference;
			}
		}

		public Collection<AssemblyNameReference> ReadAssemblyReferences ()
		{
			InitializeAssemblyReferences ();

			var references = new Collection<AssemblyNameReference> (metadata.AssemblyReferences);
			if (module.IsWindowsMetadata ())
				module.Projections.AddVirtualReferences (references);

			return references;
		}

		public MethodDefinition ReadEntryPoint ()
		{
			if (module.Image.EntryPointToken == 0)
				return null;

			var token = new MetadataToken (module.Image.EntryPointToken);
			return GetMethodDefinition (token.RID);
		}

		public Collection<ModuleDefinition> ReadModules ()
		{
			var modules = new Collection<ModuleDefinition> (1);
			modules.Add (this.module);

			int length = MoveTo (Table.File, out PByteBuffer buffer);
			for (uint i = 1; i <= length; i++) {
				var attributes = (FileAttributes) buffer.ReadUInt32 ();
				var name = ReadString (ref buffer);
				ReadBlobIndex (ref buffer);

				if (attributes != FileAttributes.ContainsMetaData)
					continue;

				var parameters = new ReaderParameters {
					ReadingMode = module.ReadingMode,
					SymbolReaderProvider = module.SymbolReaderProvider,
					AssemblyResolver = module.AssemblyResolver
				};

				modules.Add (ModuleDefinition.ReadModule (
					GetModuleFileName (name), parameters));
			}

			return modules;
		}

		string GetModuleFileName (string name)
		{
			if (module.FileName == null)
				throw new NotSupportedException ();

			var path = Path.GetDirectoryName (module.FileName);
			return Path.Combine (path, name);
		}

		void InitializeModuleReferences ()
		{
			if (metadata.ModuleReferences != null)
				return;

			int length = MoveTo (Table.ModuleRef, out PByteBuffer buffer);
			var references = metadata.ModuleReferences = new ModuleReference [length];

			for (uint i = 0; i < length; i++) {
				var reference = new ModuleReference (ReadString (ref buffer));
				reference.token = new MetadataToken (TokenType.ModuleRef, i + 1);

				references [i] = reference;
			}
		}

		public Collection<ModuleReference> ReadModuleReferences ()
		{
			InitializeModuleReferences ();

			return new Collection<ModuleReference> (metadata.ModuleReferences);
		}

		public bool HasFileResource ()
		{
			int length = MoveTo (Table.File, out PByteBuffer buffer);
			if (length == 0)
				return false;

			for (uint i = 1; i <= length; i++)
				if (ReadFileRecord (i).Col1 == FileAttributes.ContainsNoMetaData)
					return true;

			return false;
		}

		public Collection<Resource> ReadResources ()
		{
			int length = MoveTo (Table.ManifestResource, out PByteBuffer buffer);
			var resources = new Collection<Resource> (length);

			for (int i = 1; i <= length; i++) {
				var offset = buffer.ReadUInt32 ();
				var flags = (ManifestResourceAttributes) buffer.ReadUInt32 ();
				var name = ReadString (ref buffer);
				var implementation = ReadMetadataToken (CodedIndex.Implementation, ref buffer);

				Resource resource;

				if (implementation.RID == 0) {
					resource = new EmbeddedResource (name, flags, offset, this);
				} else if (implementation.TokenType == TokenType.AssemblyRef) {
					resource = new AssemblyLinkedResource (name, flags) {
						Assembly = (AssemblyNameReference) GetTypeReferenceScope (implementation),
					};
				} else if (implementation.TokenType == TokenType.File) {
					var file_record = ReadFileRecord (implementation.RID);

					resource = new LinkedResource (name, flags) {
						File = file_record.Col2,
						hash = ReadBlob (file_record.Col3)
					};
				} else
					continue;

				resources.Add (resource);
			}

			return resources;
		}

		Row<FileAttributes, string, uint> ReadFileRecord (uint rid)
		{
			if (!MoveTo (Table.File, rid, out PByteBuffer buffer))
				throw new ArgumentException ();

			var record = new Row<FileAttributes, string, uint> (
				(FileAttributes) buffer.ReadUInt32 (),
				ReadString (ref buffer),
				ReadBlobIndex (ref buffer));

			return record;
		}

		public byte [] GetManagedResource (uint offset)
		{
			return image.GetReaderAt (image.Resources.VirtualAddress, offset, (o, reader) => {
				reader.Advance ((int) o);
				return reader.ReadBytes (reader.ReadInt32 ());
			}) ?? Empty<byte>.Array;
		}

		void PopulateVersionAndFlags (AssemblyNameReference name, ref PByteBuffer buffer)
		{
			name.Version = new Version (
				buffer.ReadUInt16 (),
				buffer.ReadUInt16 (),
				buffer.ReadUInt16 (),
				buffer.ReadUInt16 ());

			name.Attributes = (AssemblyAttributes) buffer.ReadUInt32 ();
		}

		void PopulateNameAndCulture (AssemblyNameReference name, ref PByteBuffer buffer)
		{
			name.Name = ReadString (ref buffer);
			name.Culture = ReadString (ref buffer);
		}

		public TypeDefinitionCollection ReadTypes ()
		{
			InitializeTypeDefinitions ();
			var mtypes = metadata.Types;
			var type_count = mtypes.Length - metadata.NestedTypes.Count;
			var types = new TypeDefinitionCollection (module, type_count);

			for (int i = 0; i < mtypes.Length; i++) {
				var type = mtypes [i];
				if (IsNested (type.Attributes))
					continue;

				types.Add (type);
			}

			if (image.HasTable (Table.MethodPtr) || image.HasTable (Table.FieldPtr))
				CompleteTypes ();

			return types;
		}

		void CompleteTypes ()
		{
			var types = metadata.Types;

			for (int i = 0; i < types.Length; i++) {
				var type = types [i];

				Mixin.Read (type.Fields);
				Mixin.Read (type.Methods);
			}
		}

		void InitializeTypeDefinitions ()
		{
			if (metadata.Types != null)
				return;

			InitializeNestedTypes ();
			InitializeFields ();
			InitializeMethods ();

			int length = image.GetTableLength (Table.TypeDef);
			var types = metadata.Types = new TypeDefinition [length];

			for (uint i = 0; i < length; i++) {
				if (types [i] != null)
					continue;

				types [i] = ReadType (i + 1);
			}

			if (module.IsWindowsMetadata ()) {
				for (uint i = 0; i < length; i++) {
					WindowsRuntimeProjections.Project (types [i]);
				}
			}
		}

		static bool IsNested (TypeAttributes attributes)
		{
			switch (attributes & TypeAttributes.VisibilityMask) {
			case TypeAttributes.NestedAssembly:
			case TypeAttributes.NestedFamANDAssem:
			case TypeAttributes.NestedFamily:
			case TypeAttributes.NestedFamORAssem:
			case TypeAttributes.NestedPrivate:
			case TypeAttributes.NestedPublic:
				return true;
			default:
				return false;
			}
		}

		public bool HasNestedTypes (TypeDefinition type)
		{
			Collection<uint> mapping;
			InitializeNestedTypes ();

			if (!metadata.TryGetNestedTypeMapping (type, out mapping))
				return false;

			return mapping.Count > 0;
		}

		public Collection<TypeDefinition> ReadNestedTypes (TypeDefinition type)
		{
			InitializeNestedTypes ();
			Collection<uint> mapping;
			if (!metadata.TryGetNestedTypeMapping (type, out mapping))
				return new MemberDefinitionCollection<TypeDefinition> (type);

			var nested_types = new MemberDefinitionCollection<TypeDefinition> (type, mapping.Count);

			for (int i = 0; i < mapping.Count; i++) {
				var nested_type = GetTypeDefinition (mapping [i]);

				if (nested_type != null)
					nested_types.Add (nested_type);
			}

			metadata.RemoveNestedTypeMapping (type);

			return nested_types;
		}

		void InitializeNestedTypes ()
		{
			if (metadata.NestedTypes != null)
				return;

			var length = MoveTo (Table.NestedClass, out PByteBuffer buffer);

			metadata.NestedTypes = new Dictionary<uint, Collection<uint>> (length);
			metadata.ReverseNestedTypes = new Dictionary<uint, uint> (length);

			if (length == 0)
				return;

			for (int i = 1; i <= length; i++) {
				var nested = ReadTableIndex (Table.TypeDef, ref buffer);
				var declaring = ReadTableIndex (Table.TypeDef, ref buffer);

				AddNestedMapping (declaring, nested);
			}
		}

		void AddNestedMapping (uint declaring, uint nested)
		{
			metadata.SetNestedTypeMapping (declaring, AddMapping (metadata.NestedTypes, declaring, nested));
			metadata.SetReverseNestedTypeMapping (nested, declaring);
		}

		static Collection<TValue> AddMapping<TKey, TValue> (Dictionary<TKey, Collection<TValue>> cache, TKey key, TValue value)
		{
			Collection<TValue> mapped;
			if (!cache.TryGetValue (key, out mapped)) {
				mapped = new Collection<TValue> ();
			}
			mapped.Add (value);
			return mapped;
		}

		TypeDefinition ReadType (uint rid)
		{
			if (!MoveTo (Table.TypeDef, rid, out PByteBuffer buffer))
				return null;

			var attributes = (TypeAttributes) buffer.ReadUInt32 ();
			var name = ReadString (ref buffer);
			var @namespace = ReadString (ref buffer);
			var type = new TypeDefinition (@namespace, name, attributes);
			type.token = new MetadataToken (TokenType.TypeDef, rid);
			type.scope = module;
			type.module = module;

			metadata.AddTypeDefinition (type);

			this.context = type;

			type.BaseType = GetTypeDefOrRef (ReadMetadataToken (CodedIndex.TypeDefOrRef, ref buffer));

			type.fields_range = ReadListRange (rid, Table.TypeDef, Table.Field, ref buffer);
			type.methods_range = ReadListRange (rid, Table.TypeDef, Table.Method, ref buffer);

			if (IsNested (attributes))
				type.DeclaringType = GetNestedTypeDeclaringType (type);

			return type;
		}

		TypeDefinition GetNestedTypeDeclaringType (TypeDefinition type)
		{
			uint declaring_rid;
			if (!metadata.TryGetReverseNestedTypeMapping (type, out declaring_rid))
				return null;

			metadata.RemoveReverseNestedTypeMapping (type);
			return GetTypeDefinition (declaring_rid);
		}

		Range ReadListRange (uint current_index, Table current, Table target, ref PByteBuffer buffer)
		{
			var list = new Range ();

			var start = ReadTableIndex (target, ref buffer);
			if (start == 0)
				return list;

			uint next_index;
			var current_table = image.TableHeap [current];

			if (current_index == current_table.Length)
				next_index = image.TableHeap [target].Length + 1;
			else {
				var nextRowBuffer = buffer;
				nextRowBuffer.Advance ( (int) (current_table.RowSize - image.GetTableIndexSize (target)));
				next_index = ReadTableIndex (target, ref nextRowBuffer);
			}

			list.Start = start;
			list.Length = next_index - start;

			return list;
		}

		public Row<short, int> ReadTypeLayout (TypeDefinition type)
		{
			InitializeTypeLayouts ();
			Row<ushort, uint> class_layout;
			var rid = type.token.RID;
			if (!metadata.ClassLayouts.TryGetValue (rid, out class_layout))
				return new Row<short, int> (Mixin.NoDataMarker, Mixin.NoDataMarker);

			type.PackingSize = (short) class_layout.Col1;
			type.ClassSize = (int) class_layout.Col2;

			metadata.ClassLayouts.Remove (rid);

			return new Row<short, int> ((short) class_layout.Col1, (int) class_layout.Col2);
		}

		void InitializeTypeLayouts ()
		{
			if (metadata.ClassLayouts != null)
				return;

			int length = MoveTo (Table.ClassLayout, out PByteBuffer buffer);

			var class_layouts = metadata.ClassLayouts = new Dictionary<uint, Row<ushort, uint>> (length);

			for (uint i = 0; i < length; i++) {
				var packing_size = buffer.ReadUInt16 ();
				var class_size = buffer.ReadUInt32 ();

				var parent = ReadTableIndex (Table.TypeDef, ref buffer);

				class_layouts.Add (parent, new Row<ushort, uint> (packing_size, class_size));
			}
		}

		public TypeReference GetTypeDefOrRef (MetadataToken token)
		{
			return (TypeReference) LookupToken (token);
		}

		public TypeDefinition GetTypeDefinition (uint rid)
		{
			InitializeTypeDefinitions ();

			var type = metadata.GetTypeDefinition (rid);
			if (type != null)
				return type;

			type = ReadTypeDefinition (rid);

			if (module.IsWindowsMetadata ())
				WindowsRuntimeProjections.Project (type);

			return type;
		}

		TypeDefinition ReadTypeDefinition (uint rid)
		{
			return ReadType (rid);
		}

		void InitializeTypeReferences ()
		{
			if (metadata.TypeReferences != null)
				return;

			metadata.TypeReferences = new TypeReference [image.GetTableLength (Table.TypeRef)];
		}

		public TypeReference GetTypeReference (string scope, string full_name)
		{
			InitializeTypeReferences ();

			var length = metadata.TypeReferences.Length;

			for (uint i = 1; i <= length; i++) {
				var type = GetTypeReference (i);

				if (type.FullName != full_name)
					continue;

				if (string.IsNullOrEmpty (scope))
					return type;

				if (type.Scope.Name == scope)
					return type;
			}

			return null;
		}

		TypeReference GetTypeReference (uint rid)
		{
			InitializeTypeReferences ();

			var type = metadata.GetTypeReference (rid);
			if (type != null)
				return type;

			return ReadTypeReference (rid);
		}

		TypeReference ReadTypeReference (uint rid)
		{
			if (!MoveTo (Table.TypeRef, rid, out PByteBuffer buffer))
				return null;

			TypeReference declaring_type = null;
			IMetadataScope scope;

			var scope_token = ReadMetadataToken (CodedIndex.ResolutionScope, ref buffer);

			var name = ReadString (ref buffer);
			var @namespace = ReadString (ref buffer);

			var type = new TypeReference (
				@namespace,
				name,
				module,
				null);

			type.token = new MetadataToken (TokenType.TypeRef, rid);

			metadata.AddTypeReference (type);

			if (scope_token.TokenType == TokenType.TypeRef) {
				if (scope_token.RID != rid) {
					declaring_type = GetTypeDefOrRef (scope_token);

					scope = declaring_type != null
						? declaring_type.Scope
						: module;
				} else // obfuscated typeref row pointing to self
					scope = module;
			} else
				scope = GetTypeReferenceScope (scope_token);

			type.scope = scope;
			type.DeclaringType = declaring_type;

			MetadataSystem.TryProcessPrimitiveTypeReference (type);

			if (type.Module.IsWindowsMetadata ())
				WindowsRuntimeProjections.Project (type);

			return type;
		}

		IMetadataScope GetTypeReferenceScope (MetadataToken scope)
		{
			if (scope.TokenType == TokenType.Module)
				return module;

			IMetadataScope[] scopes;

			switch (scope.TokenType) {
			case TokenType.AssemblyRef:
				InitializeAssemblyReferences ();
				scopes = metadata.AssemblyReferences;
				break;
			case TokenType.ModuleRef:
				InitializeModuleReferences ();
				scopes = metadata.ModuleReferences;
				break;
			default:
				throw new NotSupportedException ();
			}

			var index = scope.RID - 1;
			if (index < 0 || index >= scopes.Length)
				return null;

			return scopes [index];
		}

		public IEnumerable<TypeReference> GetTypeReferences ()
		{
			InitializeTypeReferences ();

			var length = image.GetTableLength (Table.TypeRef);

			var type_references = new TypeReference [length];

			for (uint i = 1; i <= length; i++)
				type_references [i - 1] = GetTypeReference (i);

			return type_references;
		}

		TypeReference GetTypeSpecification (uint rid)
		{
			if (!MoveTo (Table.TypeSpec, rid, out PByteBuffer buffer))
				return null;

			var reader = ReadSignature (ReadBlobIndex (ref buffer), out PByteBuffer sig);
			var type = reader.ReadTypeSignature (ref sig);
			if (type.token.RID == 0)
				type.token = new MetadataToken (TokenType.TypeSpec, rid);

			return type;
		}

		SignatureReader ReadSignature (uint signature, out PByteBuffer buffer)
		{
			var heapSpan = new ByteSpan (image.BlobHeap.data, image.BlobHeap.size);
			var heapBuffer = new PByteBuffer (heapSpan.pointer, heapSpan.length);
			heapBuffer.Advance ((int) signature);

			var length = heapBuffer.ReadCompressedUInt32 ();

			buffer = new PByteBuffer (new ByteSpan (heapBuffer.p, length));

			return new SignatureReader (this);
		}

		public bool HasInterfaces (TypeDefinition type)
		{
			InitializeInterfaces ();
			Collection<Row<uint, MetadataToken>> mapping;

			return metadata.TryGetInterfaceMapping (type, out mapping);
		}

		public InterfaceImplementationCollection ReadInterfaces (TypeDefinition type)
		{
			InitializeInterfaces ();
			Collection<Row<uint, MetadataToken>> mapping;

			if (!metadata.TryGetInterfaceMapping (type, out mapping))
				return new InterfaceImplementationCollection (type);

			var interfaces = new InterfaceImplementationCollection (type, mapping.Count);

			this.context = type;

			for (int i = 0; i < mapping.Count; i++) {
				interfaces.Add (
					new InterfaceImplementation (
						GetTypeDefOrRef (mapping [i].Col2),
						new MetadataToken(TokenType.InterfaceImpl, mapping [i].Col1)));
			}

			metadata.RemoveInterfaceMapping (type);

			return interfaces;
		}

		void InitializeInterfaces ()
		{
			if (metadata.Interfaces != null)
				return;

			int length = MoveTo (Table.InterfaceImpl, out PByteBuffer buffer);

			metadata.Interfaces = new Dictionary<uint, Collection<Row<uint, MetadataToken>>> (length);

			for (uint i = 1; i <= length; i++) {
				var type = ReadTableIndex (Table.TypeDef, ref buffer);
				var @interface = ReadMetadataToken (CodedIndex.TypeDefOrRef, ref buffer);

				AddInterfaceMapping (type, new Row<uint, MetadataToken> (i, @interface));
			}
		}

		void AddInterfaceMapping (uint type, Row<uint, MetadataToken> @interface)
		{
			metadata.SetInterfaceMapping (type, AddMapping (metadata.Interfaces, type, @interface));
		}

		public Collection<FieldDefinition> ReadFields (TypeDefinition type)
		{
			var fields_range = type.fields_range;
			if (fields_range.Length == 0)
				return new MemberDefinitionCollection<FieldDefinition> (type);

			var fields = new MemberDefinitionCollection<FieldDefinition> (type, (int) fields_range.Length);
			this.context = type;

			if (!MoveTo (Table.FieldPtr, fields_range.Start, out PByteBuffer buffer)) {
				if (!MoveTo (Table.Field, fields_range.Start, out buffer))
					return fields;

				for (uint i = 0; i < fields_range.Length; i++)
					ReadField (fields_range.Start + i, fields, ref buffer);
			} else
				ReadPointers (Table.FieldPtr, Table.Field, fields_range, fields, ReadField);

			return fields;
		}

		void ReadField (uint field_rid, Collection<FieldDefinition> fields, ref PByteBuffer buffer)
		{
			var attributes = (FieldAttributes) buffer.ReadUInt16 ();
			var name = ReadString (ref buffer);
			var signature = ReadBlobIndex (ref buffer);

			var field = new FieldDefinition (name, attributes, ReadFieldType (signature));
			field.token = new MetadataToken (TokenType.Field, field_rid);
			metadata.AddFieldDefinition (field);

			if (IsDeleted (field))
				return;

			fields.Add (field);

			if (module.IsWindowsMetadata ())
				WindowsRuntimeProjections.Project (field);
		}

		void InitializeFields ()
		{
			if (metadata.Fields != null)
				return;

			metadata.Fields = new FieldDefinition [image.GetTableLength (Table.Field)];
		}

		TypeReference ReadFieldType (uint signature)
		{
			var reader = ReadSignature (signature, out PByteBuffer sig);

			const byte field_sig = 0x6;

			if (sig.ReadByte () != field_sig)
				throw new NotSupportedException ();

			return reader.ReadTypeSignature (ref sig);
		}

		public int ReadFieldRVA (FieldDefinition field)
		{
			InitializeFieldRVAs ();
			var rid = field.token.RID;

			RVA rva;
			if (!metadata.FieldRVAs.TryGetValue (rid, out rva))
				return 0;

			var size = GetFieldTypeSize (field.FieldType);

			if (size == 0 || rva == 0)
				return 0;

			metadata.FieldRVAs.Remove (rid);

			field.InitialValue = GetFieldInitializeValue (size, rva);

			return (int) rva;
		}

		byte [] GetFieldInitializeValue (int size, RVA rva)
		{
			return image.GetReaderAt (rva, size, (s, reader) => reader.ReadBytes (s)) ?? Empty<byte>.Array;
		}

		static int GetFieldTypeSize (TypeReference type)
		{
			int size = 0;

			switch (type.etype) {
			case ElementType.Boolean:
			case ElementType.U1:
			case ElementType.I1:
				size = 1;
				break;
			case ElementType.U2:
			case ElementType.I2:
			case ElementType.Char:
				size = 2;
				break;
			case ElementType.U4:
			case ElementType.I4:
			case ElementType.R4:
				size = 4;
				break;
			case ElementType.U8:
			case ElementType.I8:
			case ElementType.R8:
				size = 8;
				break;
			case ElementType.Ptr:
			case ElementType.FnPtr:
				size = IntPtr.Size;
				break;
			case ElementType.CModOpt:
			case ElementType.CModReqD:
				return GetFieldTypeSize (((IModifierType) type).ElementType);
			default:
				var field_type = type.Resolve ();
				if (field_type != null && field_type.HasLayoutInfo)
					size = field_type.ClassSize;

				break;
			}

			return size;
		}

		void InitializeFieldRVAs ()
		{
			if (metadata.FieldRVAs != null)
				return;

			int length = MoveTo (Table.FieldRVA, out PByteBuffer buffer);

			var field_rvas = metadata.FieldRVAs = new Dictionary<uint, uint> (length);

			for (int i = 0; i < length; i++) {
				var rva = buffer.ReadUInt32 ();
				var field = ReadTableIndex (Table.Field, ref buffer);

				field_rvas.Add (field, rva);
			}
		}

		public int ReadFieldLayout (FieldDefinition field)
		{
			InitializeFieldLayouts ();
			var rid = field.token.RID;
			uint offset;
			if (!metadata.FieldLayouts.TryGetValue (rid, out offset))
				return Mixin.NoDataMarker;

			metadata.FieldLayouts.Remove (rid);

			return (int) offset;
		}

		void InitializeFieldLayouts ()
		{
			if (metadata.FieldLayouts != null)
				return;

			int length = MoveTo (Table.FieldLayout, out PByteBuffer buffer);

			var field_layouts = metadata.FieldLayouts = new Dictionary<uint, uint> (length);

			for (int i = 0; i < length; i++) {
				var offset = buffer.ReadUInt32 ();
				var field = ReadTableIndex (Table.Field, ref buffer);

				field_layouts.Add (field, offset);
			}
		}

		public bool HasEvents (TypeDefinition type)
		{
			InitializeEvents ();

			Range range;
			if (!metadata.TryGetEventsRange (type, out range))
				return false;

			return range.Length > 0;
		}

		public Collection<EventDefinition> ReadEvents (TypeDefinition type)
		{
			InitializeEvents ();
			Range range;

			if (!metadata.TryGetEventsRange (type, out range))
				return new MemberDefinitionCollection<EventDefinition> (type);

			var events = new MemberDefinitionCollection<EventDefinition> (type, (int) range.Length);

			metadata.RemoveEventsRange (type);

			if (range.Length == 0)
				return events;

			this.context = type;

			if (!MoveTo (Table.EventPtr, range.Start, out PByteBuffer buffer)) {
				if (!MoveTo (Table.Event, range.Start, out buffer))
					return events;

				for (uint i = 0; i < range.Length; i++)
					ReadEvent (range.Start + i, events, ref buffer);
			} else
				ReadPointers (Table.EventPtr, Table.Event, range, events, ReadEvent);

			return events;
		}

		void ReadEvent (uint event_rid, Collection<EventDefinition> events, ref PByteBuffer buffer)
		{
			var attributes = (EventAttributes) buffer.ReadUInt16 ();
			var name = ReadString (ref buffer);
			var event_type = GetTypeDefOrRef (ReadMetadataToken (CodedIndex.TypeDefOrRef, ref buffer));

			var @event = new EventDefinition (name, attributes, event_type);
			@event.token = new MetadataToken (TokenType.Event, event_rid);

			if (IsDeleted (@event))
				return;

			events.Add (@event);
		}

		void InitializeEvents ()
		{
			if (metadata.Events != null)
				return;

			int length = MoveTo (Table.EventMap, out PByteBuffer buffer);

			metadata.Events = new Dictionary<uint, Range> (length);

			for (uint i = 1; i <= length; i++) {
				var type_rid = ReadTableIndex (Table.TypeDef, ref buffer);
				Range events_range = ReadListRange (i, Table.EventMap, Table.Event, ref buffer);
				metadata.AddEventsRange (type_rid, events_range);
			}
		}

		public bool HasProperties (TypeDefinition type)
		{
			InitializeProperties ();

			Range range;
			if (!metadata.TryGetPropertiesRange (type, out range))
				return false;

			return range.Length > 0;
		}

		public Collection<PropertyDefinition> ReadProperties (TypeDefinition type)
		{
			InitializeProperties ();

			Range range;

			if (!metadata.TryGetPropertiesRange (type, out range))
				return new MemberDefinitionCollection<PropertyDefinition> (type);

			metadata.RemovePropertiesRange (type);

			var properties = new MemberDefinitionCollection<PropertyDefinition> (type, (int) range.Length);

			if (range.Length == 0)
				return properties;

			this.context = type;

			if (!MoveTo (Table.PropertyPtr, range.Start, out PByteBuffer buffer)) {
				if (!MoveTo (Table.Property, range.Start, out buffer))
					return properties;
				for (uint i = 0; i < range.Length; i++)
					ReadProperty (range.Start + i, properties, ref buffer);
			} else
				ReadPointers (Table.PropertyPtr, Table.Property, range, properties, ReadProperty);

			return properties;
		}

		void ReadProperty (uint property_rid, Collection<PropertyDefinition> properties, ref PByteBuffer buffer)
		{
			var attributes = (PropertyAttributes) buffer.ReadUInt16 ();
			var name = ReadString (ref buffer);
			var signature = ReadBlobIndex (ref buffer);

			var reader = ReadSignature (signature, out PByteBuffer sig);

			const byte property_signature = 0x8;

			var calling_convention = sig.ReadByte ();

			if ((calling_convention & property_signature) == 0)
				throw new NotSupportedException ();

			var has_this = (calling_convention & 0x20) != 0;

			sig.ReadCompressedUInt32 (); // count

			var property = new PropertyDefinition (name, attributes, reader.ReadTypeSignature (ref sig));
			property.HasThis = has_this;
			property.token = new MetadataToken (TokenType.Property, property_rid);

			if (IsDeleted (property))
				return;

			properties.Add (property);
		}

		void InitializeProperties ()
		{
			if (metadata.Properties != null)
				return;

			int length = MoveTo (Table.PropertyMap, out PByteBuffer buffer);

			metadata.Properties = new Dictionary<uint, Range> (length);

			for (uint i = 1; i <= length; i++) {
				var type_rid = ReadTableIndex (Table.TypeDef, ref buffer);
				var properties_range = ReadListRange (i, Table.PropertyMap, Table.Property, ref buffer);
				metadata.AddPropertiesRange (type_rid, properties_range);
			}
		}

		MethodSemanticsAttributes ReadMethodSemantics (MethodDefinition method)
		{
			InitializeMethodSemantics ();
			Row<MethodSemanticsAttributes, MetadataToken> row;
			if (!metadata.Semantics.TryGetValue (method.token.RID, out row))
				return MethodSemanticsAttributes.None;

			var type = method.DeclaringType;

			switch (row.Col1) {
			case MethodSemanticsAttributes.AddOn:
				GetEvent (type, row.Col2).add_method = method;
				break;
			case MethodSemanticsAttributes.Fire:
				GetEvent (type, row.Col2).invoke_method = method;
				break;
			case MethodSemanticsAttributes.RemoveOn:
				GetEvent (type, row.Col2).remove_method = method;
				break;
			case MethodSemanticsAttributes.Getter:
				GetProperty (type, row.Col2).get_method = method;
				break;
			case MethodSemanticsAttributes.Setter:
				GetProperty (type, row.Col2).set_method = method;
				break;
			case MethodSemanticsAttributes.Other:
				switch (row.Col2.TokenType) {
				case TokenType.Event: {
					var @event = GetEvent (type, row.Col2);
					if (@event.other_methods == null)
						@event.other_methods = new Collection<MethodDefinition> ();

					@event.other_methods.Add (method);
					break;
				}
				case TokenType.Property: {
					var property = GetProperty (type, row.Col2);
					if (property.other_methods == null)
						property.other_methods = new Collection<MethodDefinition> ();

					property.other_methods.Add (method);

					break;
				}
				default:
					throw new NotSupportedException ();
				}
				break;
			default:
				throw new NotSupportedException ();
			}

			metadata.Semantics.Remove (method.token.RID);

			return row.Col1;
		}

		static EventDefinition GetEvent (TypeDefinition type, MetadataToken token)
		{
			if (token.TokenType != TokenType.Event)
				throw new ArgumentException ();

			return GetMember (type.Events, token);
		}

		static PropertyDefinition GetProperty (TypeDefinition type, MetadataToken token)
		{
			if (token.TokenType != TokenType.Property)
				throw new ArgumentException ();

			return GetMember (type.Properties, token);
		}

		static TMember GetMember<TMember> (Collection<TMember> members, MetadataToken token) where TMember : IMemberDefinition
		{
			for (int i = 0; i < members.Count; i++) {
				var member = members [i];
				if (member.MetadataToken == token)
					return member;
			}

			throw new ArgumentException ();
		}

		void InitializeMethodSemantics ()
		{
			if (metadata.Semantics != null)
				return;

			int length = MoveTo (Table.MethodSemantics, out PByteBuffer buffer);

			var semantics = metadata.Semantics = new Dictionary<uint, Row<MethodSemanticsAttributes, MetadataToken>> (0);

			for (uint i = 0; i < length; i++) {
				var attributes = (MethodSemanticsAttributes) buffer.ReadUInt16 ();
				var method_rid = ReadTableIndex (Table.Method, ref buffer);
				var association = ReadMetadataToken (CodedIndex.HasSemantics, ref buffer);

				semantics [method_rid] = new Row<MethodSemanticsAttributes, MetadataToken> (attributes, association);
			}
		}

		public void ReadMethods (PropertyDefinition property)
		{
			ReadAllSemantics (property.DeclaringType);
		}

		public void ReadMethods (EventDefinition @event)
		{
			ReadAllSemantics (@event.DeclaringType);
		}

		public void ReadAllSemantics (MethodDefinition method)
		{
			ReadAllSemantics (method.DeclaringType);
		}

		void ReadAllSemantics (TypeDefinition type)
		{
			var methods = type.Methods;
			for (int i = 0; i < methods.Count; i++) {
				var method = methods [i];
				if (method.sem_attrs_ready)
					continue;

				method.sem_attrs = ReadMethodSemantics (method);
				method.sem_attrs_ready = true;
			}
		}

		public Collection<MethodDefinition> ReadMethods (TypeDefinition type)
		{
			var methods_range = type.methods_range;
			if (methods_range.Length == 0)
				return new MemberDefinitionCollection<MethodDefinition> (type);

			var methods = new MemberDefinitionCollection<MethodDefinition> (type, (int) methods_range.Length);
			if (!MoveTo (Table.MethodPtr, methods_range.Start, out PByteBuffer buffer)) {
				if (!MoveTo (Table.Method, methods_range.Start, out buffer))
					return methods;

				for (uint i = 0; i < methods_range.Length; i++)
					ReadMethod (methods_range.Start + i, methods, ref buffer);
			} else
				ReadPointers (Table.MethodPtr, Table.Method, methods_range, methods, ReadMethod);

			return methods;
		}

		delegate void PointerReader<TMember> (uint rid, Collection<TMember> members, ref PByteBuffer buffer) where TMember : IMemberDefinition;

		void ReadPointers<TMember> (Table ptr, Table table, Range range, Collection<TMember> members, PointerReader<TMember> reader)
			where TMember : IMemberDefinition
		{
			for (uint i = 0; i < range.Length; i++) {
				MoveTo (ptr, range.Start + i, out PByteBuffer buffer);

				var rid = ReadTableIndex (table, ref buffer);
				MoveTo (table, rid, out buffer);

				reader (rid, members, ref buffer);
			}
		}

		static bool IsDeleted (IMemberDefinition member)
		{
			return member.IsSpecialName && member.Name == "_Deleted";
		}

		void InitializeMethods ()
		{
			if (metadata.Methods != null)
				return;

			metadata.Methods = new MethodDefinition [image.GetTableLength (Table.Method)];
		}

		void ReadMethod (uint method_rid, Collection<MethodDefinition> methods, ref PByteBuffer buffer)
		{
			var method = new MethodDefinition ();
			method.rva = buffer.ReadUInt32 ();
			method.ImplAttributes = (MethodImplAttributes) buffer.ReadUInt16 ();
			method.Attributes = (MethodAttributes) buffer.ReadUInt16 ();
			method.Name = ReadString (ref buffer);
			method.token = new MetadataToken (TokenType.Method, method_rid);

			if (IsDeleted (method))
				return;

			methods.Add (method); // attach method

			var signature = ReadBlobIndex (ref buffer);
			var param_range = ReadListRange (method_rid, Table.Method, Table.Param, ref buffer);

			this.context = method;

			ReadMethodSignature (signature, method);
			metadata.AddMethodDefinition (method);

			if (param_range.Length != 0) {
				ReadParameters (method, param_range);
			}

			if (module.IsWindowsMetadata ())
				WindowsRuntimeProjections.Project (method);
		}

		void ReadParameters (MethodDefinition method, Range param_range)
		{
			if (!MoveTo (Table.ParamPtr, param_range.Start, out PByteBuffer buffer)) {
				if (!MoveTo (Table.Param, param_range.Start, out buffer))
					return;

				for (uint i = 0; i < param_range.Length; i++)
					ReadParameter (param_range.Start + i, method, ref buffer);
			} else
				ReadParameterPointers (method, param_range);
		}

		void ReadParameterPointers (MethodDefinition method, Range range)
		{
			for (uint i = 0; i < range.Length; i++) {
				MoveTo (Table.ParamPtr, range.Start + i, out PByteBuffer buffer);

				var rid = ReadTableIndex (Table.Param, ref buffer);

				MoveTo (Table.Param, rid, out buffer);

				ReadParameter (rid, method, ref buffer);
			}
		}

		void ReadParameter (uint param_rid, MethodDefinition method, ref PByteBuffer buffer)
		{
			var attributes = (ParameterAttributes) buffer.ReadUInt16 ();
			var sequence = buffer.ReadUInt16 ();
			var name = ReadString (ref buffer);

			var parameter = sequence == 0
				? method.MethodReturnType.Parameter
				: method.Parameters [sequence - 1];

			parameter.token = new MetadataToken (TokenType.Param, param_rid);
			parameter.Name = name;
			parameter.Attributes = attributes;
		}

		void ReadMethodSignature (uint signature, IMethodSignature method)
		{
			var reader = ReadSignature (signature, out PByteBuffer sig);
			reader.ReadMethodSignature (method, ref sig);
		}

		public PInvokeInfo ReadPInvokeInfo (MethodDefinition method)
		{
			InitializePInvokes ();
			Row<PInvokeAttributes, uint, uint> row;

			var rid = method.token.RID;

			if (!metadata.PInvokes.TryGetValue (rid, out row))
				return null;

			metadata.PInvokes.Remove (rid);

			return new PInvokeInfo (
				row.Col1,
				image.StringHeap.Read (row.Col2),
				module.ModuleReferences [(int) row.Col3 - 1]);
		}

		void InitializePInvokes ()
		{
			if (metadata.PInvokes != null)
				return;

			int length = MoveTo (Table.ImplMap, out PByteBuffer buffer);

			var pinvokes = metadata.PInvokes = new Dictionary<uint, Row<PInvokeAttributes, uint, uint>> (length);

			for (int i = 1; i <= length; i++) {
				var attributes = (PInvokeAttributes) buffer.ReadUInt16 ();
				var method = ReadMetadataToken (CodedIndex.MemberForwarded, ref buffer);
				var name = ReadStringIndex (ref buffer);
				var scope = ReadTableIndex (Table.File, ref buffer);

				if (method.TokenType != TokenType.Method)
					continue;

				pinvokes.Add (method.RID, new Row<PInvokeAttributes, uint, uint> (attributes, name, scope));
			}
		}

		public bool HasGenericParameters (IGenericParameterProvider provider)
		{
			InitializeGenericParameters ();

			Range [] ranges;
			if (!metadata.TryGetGenericParameterRanges (provider, out ranges))
				return false;

			return RangesSize (ranges) > 0;
		}

		public Collection<GenericParameter> ReadGenericParameters (IGenericParameterProvider provider)
		{
			InitializeGenericParameters ();

			Range [] ranges;
			if (!metadata.TryGetGenericParameterRanges (provider, out ranges))
				return new GenericParameterCollection (provider);

			metadata.RemoveGenericParameterRange (provider);

			var generic_parameters = new GenericParameterCollection (provider, RangesSize (ranges));

			for (int i = 0; i < ranges.Length; i++)
				ReadGenericParametersRange (ranges [i], provider, generic_parameters);

			return generic_parameters;
		}

		void ReadGenericParametersRange (Range range, IGenericParameterProvider provider, GenericParameterCollection generic_parameters)
		{
			if (!MoveTo (Table.GenericParam, range.Start, out PByteBuffer buffer))
				return;

			for (uint i = 0; i < range.Length; i++) {
				buffer.ReadUInt16 (); // index
				var flags = (GenericParameterAttributes) buffer.ReadUInt16 ();
				ReadMetadataToken (CodedIndex.TypeOrMethodDef, ref buffer);
				var name = ReadString (ref buffer);

				var parameter = new GenericParameter (name, provider);
				parameter.token = new MetadataToken (TokenType.GenericParam, range.Start + i);
				parameter.Attributes = flags;

				generic_parameters.Add (parameter);
			}
		}

		void InitializeGenericParameters ()
		{
			if (metadata.GenericParameters != null)
				return;

			metadata.GenericParameters = InitializeRanges (
				Table.GenericParam, (ref PByteBuffer buffer) => {
					buffer.Advance (4);
					var next = ReadMetadataToken (CodedIndex.TypeOrMethodDef, ref buffer);
					ReadStringIndex (ref buffer);
					return next;
			});
		}

		delegate MetadataToken NextInRangeReader (ref PByteBuffer buffer);

		Dictionary<MetadataToken, Range []> InitializeRanges (Table table, NextInRangeReader get_next)
		{
			int length = MoveTo (table, out PByteBuffer buffer);
			var ranges = new Dictionary<MetadataToken, Range []> (length);

			if (length == 0)
				return ranges;

			MetadataToken owner = MetadataToken.Zero;
			Range range = new Range (1, 0);

			for (uint i = 1; i <= length; i++) {
				var next = get_next (ref buffer);

				if (i == 1) {
					owner = next;
					range.Length++;
				} else if (next != owner) {
					AddRange (ranges, owner, range);
					range = new Range (i, 1);
					owner = next;
				} else
					range.Length++;
			}

			AddRange (ranges, owner, range);

			return ranges;
		}

		static void AddRange (Dictionary<MetadataToken, Range []> ranges, MetadataToken owner, Range range)
		{
			if (owner.RID == 0)
				return;

			Range [] slots;
			if (!ranges.TryGetValue (owner, out slots)) {
				ranges.Add (owner, new [] { range });
				return;
			}

			ranges [owner] = slots.Add(range);
		}

		public bool HasGenericConstraints (GenericParameter generic_parameter)
		{
			InitializeGenericConstraints ();

			Collection<Row<uint, MetadataToken>> mapping;
			if (!metadata.TryGetGenericConstraintMapping (generic_parameter, out mapping))
				return false;

			return mapping.Count > 0;
		}

		public GenericParameterConstraintCollection ReadGenericConstraints (GenericParameter generic_parameter)
		{
			InitializeGenericConstraints ();

			Collection<Row<uint, MetadataToken>> mapping;
			if (!metadata.TryGetGenericConstraintMapping (generic_parameter, out mapping))
				return new GenericParameterConstraintCollection (generic_parameter);

			var constraints = new GenericParameterConstraintCollection (generic_parameter, mapping.Count);

			this.context = (IGenericContext) generic_parameter.Owner;

			for (int i = 0; i < mapping.Count; i++) {
				constraints.Add (
					new GenericParameterConstraint (
						GetTypeDefOrRef (mapping [i].Col2),
						new MetadataToken (TokenType.GenericParamConstraint, mapping [i].Col1)));
			}

			metadata.RemoveGenericConstraintMapping (generic_parameter);

			return constraints;
		}

		void InitializeGenericConstraints ()
		{
			if (metadata.GenericConstraints != null)
				return;

			var length = MoveTo (Table.GenericParamConstraint, out PByteBuffer buffer);

			metadata.GenericConstraints = new Dictionary<uint, Collection<Row<uint, MetadataToken>>> (length);

			for (uint i = 1; i <= length; i++) {
				AddGenericConstraintMapping (
					ReadTableIndex (Table.GenericParam, ref buffer),
					new Row<uint, MetadataToken> (i, ReadMetadataToken (CodedIndex.TypeDefOrRef, ref buffer)));
			}
		}

		void AddGenericConstraintMapping (uint generic_parameter, Row<uint, MetadataToken> constraint)
		{
			metadata.SetGenericConstraintMapping (
				generic_parameter,
				AddMapping (metadata.GenericConstraints, generic_parameter, constraint));
		}

		public bool HasOverrides (MethodDefinition method)
		{
			InitializeOverrides ();
			Collection<MetadataToken> mapping;

			if (!metadata.TryGetOverrideMapping (method, out mapping))
				return false;

			return mapping.Count > 0;
		}

		public Collection<MethodReference> ReadOverrides (MethodDefinition method)
		{
			InitializeOverrides ();

			Collection<MetadataToken> mapping;
			if (!metadata.TryGetOverrideMapping (method, out mapping))
				return new Collection<MethodReference> ();

			var overrides = new Collection<MethodReference> (mapping.Count);

			this.context = method;

			for (int i = 0; i < mapping.Count; i++)
				overrides.Add ((MethodReference) LookupToken (mapping [i]));

			metadata.RemoveOverrideMapping (method);

			return overrides;
		}

		void InitializeOverrides ()
		{
			if (metadata.Overrides != null)
				return;

			var length = MoveTo (Table.MethodImpl, out PByteBuffer buffer);

			metadata.Overrides = new Dictionary<uint, Collection<MetadataToken>> (length);

			for (int i = 1; i <= length; i++) {
				ReadTableIndex (Table.TypeDef, ref buffer);

				var method = ReadMetadataToken (CodedIndex.MethodDefOrRef, ref buffer);
				if (method.TokenType != TokenType.Method)
					throw new NotSupportedException ();

				var @override = ReadMetadataToken (CodedIndex.MethodDefOrRef, ref buffer);

				AddOverrideMapping (method.RID, @override);
			}
		}

		void AddOverrideMapping (uint method_rid, MetadataToken @override)
		{
			metadata.SetOverrideMapping (
				method_rid,
				AddMapping (metadata.Overrides, method_rid, @override));
		}

		public MethodBody ReadMethodBody (MethodDefinition method)
		{
			return code.ReadMethodBody (method);
		}

		public int ReadCodeSize (MethodDefinition method)
		{
			return code.ReadCodeSize (method);
		}

		public CallSite ReadCallSite (MetadataToken token)
		{
			if (!MoveTo (Table.StandAloneSig, token.RID, out PByteBuffer buffer))
				return null;

			var signature = ReadBlobIndex (ref buffer);

			var call_site = new CallSite ();

			ReadMethodSignature (signature, call_site);

			call_site.MetadataToken = token;

			return call_site;
		}

		public VariableDefinitionCollection ReadVariables (MetadataToken local_var_token)
		{
			if (!MoveTo (Table.StandAloneSig, local_var_token.RID, out PByteBuffer buffer))
				return null;

			var reader = ReadSignature (ReadBlobIndex (ref buffer), out PByteBuffer sig);

			const byte local_sig = 0x7;

			if (sig.ReadByte () != local_sig)
				throw new NotSupportedException ();

			var count = sig.ReadCompressedUInt32 ();
			if (count == 0)
				return null;

			var variables = new VariableDefinitionCollection ((int) count);

			for (int i = 0; i < count; i++)
				variables.Add (new VariableDefinition (reader.ReadTypeSignature (ref sig)));

			return variables;
		}

		public IMetadataTokenProvider LookupToken (MetadataToken token)
		{
			var rid = token.RID;

			if (rid == 0)
				return null;

			if (metadata_reader != null)
				return metadata_reader.LookupToken (token);

			IMetadataTokenProvider element;
			var context = this.context;

			switch (token.TokenType) {
			case TokenType.TypeDef:
				element = GetTypeDefinition (rid);
				break;
			case TokenType.TypeRef:
				element = GetTypeReference (rid);
				break;
			case TokenType.TypeSpec:
				element = GetTypeSpecification (rid);
				break;
			case TokenType.Field:
				element = GetFieldDefinition (rid);
				break;
			case TokenType.Method:
				element = GetMethodDefinition (rid);
				break;
			case TokenType.MemberRef:
				element = GetMemberReference (rid);
				break;
			case TokenType.MethodSpec:
				element = GetMethodSpecification (rid);
				break;
			default:
				return null;
			}

			this.context = context;

			return element;
		}

		public FieldDefinition GetFieldDefinition (uint rid)
		{
			InitializeTypeDefinitions ();

			var field = metadata.GetFieldDefinition (rid);
			if (field != null)
				return field;

			return LookupField (rid);
		}

		FieldDefinition LookupField (uint rid)
		{
			var type = metadata.GetFieldDeclaringType (rid);
			if (type == null)
				return null;

			Mixin.Read (type.Fields);

			return metadata.GetFieldDefinition (rid);
		}

		public MethodDefinition GetMethodDefinition (uint rid)
		{
			InitializeTypeDefinitions ();

			var method = metadata.GetMethodDefinition (rid);
			if (method != null)
				return method;

			return LookupMethod (rid);
		}

		MethodDefinition LookupMethod (uint rid)
		{
			var type = metadata.GetMethodDeclaringType (rid);
			if (type == null)
				return null;

			Mixin.Read (type.Methods);

			return metadata.GetMethodDefinition (rid);
		}

		MethodSpecification GetMethodSpecification (uint rid)
		{
			if (!MoveTo (Table.MethodSpec, rid, out PByteBuffer buffer))
				return null;

			var element_method = (MethodReference) LookupToken (
				ReadMetadataToken (CodedIndex.MethodDefOrRef, ref buffer));
			var signature = ReadBlobIndex (ref buffer);

			var method_spec = ReadMethodSpecSignature (signature, element_method);
			method_spec.token = new MetadataToken (TokenType.MethodSpec, rid);
			return method_spec;
		}

		MethodSpecification ReadMethodSpecSignature (uint signature, MethodReference method)
		{
			var reader = ReadSignature (signature, out PByteBuffer sig);
			const byte methodspec_sig = 0x0a;

			var call_conv = sig.ReadByte ();

			if (call_conv != methodspec_sig)
				throw new NotSupportedException ();

			var arity = sig.ReadCompressedUInt32 ();

			var instance = new GenericInstanceMethod (method, (int) arity);

			reader.ReadGenericInstanceSignature (method, instance, arity, ref sig);

			return instance;
		}

		MemberReference GetMemberReference (uint rid)
		{
			InitializeMemberReferences ();

			var member = metadata.GetMemberReference (rid);
			if (member != null)
				return member;

			member = ReadMemberReference (rid);
			if (member != null && !member.ContainsGenericParameter)
				metadata.AddMemberReference (member);
			return member;
		}

		MemberReference ReadMemberReference (uint rid)
		{
			if (!MoveTo (Table.MemberRef, rid, out PByteBuffer buffer))
				return null;

			var token = ReadMetadataToken (CodedIndex.MemberRefParent, ref buffer);
			var name = ReadString (ref buffer);
			var signature = ReadBlobIndex (ref buffer);

			MemberReference member;

			switch (token.TokenType) {
			case TokenType.TypeDef:
			case TokenType.TypeRef:
			case TokenType.TypeSpec:
				member = ReadTypeMemberReference (token, name, signature);
				break;
			case TokenType.Method:
				member = ReadMethodMemberReference (token, name, signature);
				break;
			default:
				throw new NotSupportedException ();
			}

			member.token = new MetadataToken (TokenType.MemberRef, rid);

			if (module.IsWindowsMetadata ())
				WindowsRuntimeProjections.Project (member);

			return member;
		}

		MemberReference ReadTypeMemberReference (MetadataToken type, string name, uint signature)
		{
			var declaring_type = GetTypeDefOrRef (type);

			if (!declaring_type.IsArray)
				this.context = declaring_type;

			var member = ReadMemberReferenceSignature (signature, declaring_type);
			member.Name = name;

			return member;
		}

		MemberReference ReadMemberReferenceSignature (uint signature, TypeReference declaring_type)
		{
			var reader = ReadSignature (signature, out PByteBuffer sig);

			const byte field_sig = 0x6;

			if (sig.PeekByte () == field_sig) {
				sig.Advance (1);
				var field = new FieldReference ();
				field.DeclaringType = declaring_type;
				field.FieldType = reader.ReadTypeSignature (ref sig);
				return field;
			} else {
				var method = new MethodReference ();
				method.DeclaringType = declaring_type;
				reader.ReadMethodSignature (method, ref sig);
				return method;
			}
		}

		MemberReference ReadMethodMemberReference (MetadataToken token, string name, uint signature)
		{
			var method = GetMethodDefinition (token.RID);

			this.context = method;

			var member = ReadMemberReferenceSignature (signature, method.DeclaringType);
			member.Name = name;

			return member;
		}

		void InitializeMemberReferences ()
		{
			if (metadata.MemberReferences != null)
				return;

			metadata.MemberReferences = new MemberReference [image.GetTableLength (Table.MemberRef)];
		}

		public IEnumerable<MemberReference> GetMemberReferences ()
		{
			InitializeMemberReferences ();

			var length = image.GetTableLength (Table.MemberRef);

			var type_system = module.TypeSystem;

			var context = new MethodDefinition (string.Empty, MethodAttributes.Static, type_system.Void);
			context.DeclaringType = new TypeDefinition (string.Empty, string.Empty, TypeAttributes.Public);

			var member_references = new MemberReference [length];

			for (uint i = 1; i <= length; i++) {
				this.context = context;
				member_references [i - 1] = GetMemberReference (i);
			}

			return member_references;
		}

		void InitializeConstants ()
		{
			if (metadata.Constants != null)
				return;

			var length = MoveTo (Table.Constant, out PByteBuffer buffer);

			var constants = metadata.Constants = new Dictionary<MetadataToken, Row<ElementType, uint>> (length);

			for (uint i = 1; i <= length; i++) {
				var type = (ElementType) buffer.ReadUInt16 ();
				var owner = ReadMetadataToken (CodedIndex.HasConstant, ref buffer);
				var signature = ReadBlobIndex (ref buffer);

				constants.Add (owner, new Row<ElementType, uint> (type, signature));
			}
		}

		public TypeReference ReadConstantSignature (MetadataToken token)
		{
			if (token.TokenType != TokenType.Signature)
				throw new NotSupportedException ();

			if (token.RID == 0)
				return null;

			if (!MoveTo (Table.StandAloneSig, token.RID, out PByteBuffer buffer))
				return null;

			return ReadFieldType (ReadBlobIndex (ref buffer));
		}

		public object ReadConstant (IConstantProvider owner)
		{
			InitializeConstants ();

			Row<ElementType, uint> row;
			if (!metadata.Constants.TryGetValue (owner.MetadataToken, out row))
				return Mixin.NoValue;

			metadata.Constants.Remove (owner.MetadataToken);

			return ReadConstantValue (row.Col1, row.Col2);
		}

		object ReadConstantValue (ElementType etype, uint signature)
		{
			switch (etype) {
			case ElementType.Class:
			case ElementType.Object:
				return null;
			case ElementType.String:
				return ReadConstantString (signature);
			default:
				return ReadConstantPrimitive (etype, signature);
			}
		}

		string ReadConstantString (uint signature)
		{
			byte [] blob;
			int index, count;

			GetBlobView (signature, out blob, out index, out count);
			if (count == 0)
				return string.Empty;

			if ((count & 1) == 1)
				count--;

			return Encoding.Unicode.GetString (blob, index, count);
		}

		object ReadConstantPrimitive (ElementType type, uint signature)
		{
			var reader = ReadSignature (signature, out PByteBuffer sig);
			return reader.ReadConstantSignature (type, ref sig);
		}

		internal void InitializeCustomAttributes ()
		{
			if (metadata.CustomAttributes != null)
				return;

			metadata.CustomAttributes = InitializeRanges (
				Table.CustomAttribute, (ref PByteBuffer buffer) => {
					var next = ReadMetadataToken (CodedIndex.HasCustomAttribute, ref buffer);
					ReadMetadataToken (CodedIndex.CustomAttributeType, ref buffer);
					ReadBlobIndex (ref buffer);
					return next;
			});
		}

		public bool HasCustomAttributes (ICustomAttributeProvider owner)
		{
			InitializeCustomAttributes ();

			Range [] ranges;
			if (!metadata.TryGetCustomAttributeRanges (owner, out ranges))
				return false;

			return RangesSize (ranges) > 0;
		}

		public Collection<CustomAttribute> ReadCustomAttributes (ICustomAttributeProvider owner)
		{
			InitializeCustomAttributes ();

			Range [] ranges;
			if (!metadata.TryGetCustomAttributeRanges (owner, out ranges))
				return new Collection<CustomAttribute> ();

			var custom_attributes = new Collection<CustomAttribute> (RangesSize (ranges));

			for (int i = 0; i < ranges.Length; i++)
				ReadCustomAttributeRange (ranges [i], custom_attributes);

			metadata.RemoveCustomAttributeRange (owner);

			if (module.IsWindowsMetadata ())
				foreach (var custom_attribute in custom_attributes)
					WindowsRuntimeProjections.Project (owner, custom_attribute);

			return custom_attributes;
		}

		void ReadCustomAttributeRange (Range range, Collection<CustomAttribute> custom_attributes)
		{
			if (!MoveTo (Table.CustomAttribute, range.Start, out PByteBuffer buffer))
				return;

			for (var i = 0; i < range.Length; i++) {
				ReadMetadataToken (CodedIndex.HasCustomAttribute, ref buffer);

				var constructor = (MethodReference) LookupToken (
					ReadMetadataToken (CodedIndex.CustomAttributeType, ref buffer));

				var signature = ReadBlobIndex (ref buffer);

				custom_attributes.Add (new CustomAttribute (signature, constructor));
			}
		}

		static int RangesSize (Range [] ranges)
		{
			uint size = 0;
			for (int i = 0; i < ranges.Length; i++)
				size += ranges [i].Length;

			return (int) size;
		}

		public IEnumerable<CustomAttribute> GetCustomAttributes ()
		{
			InitializeTypeDefinitions ();

			var length = image.TableHeap [Table.CustomAttribute].Length;
			var custom_attributes = new Collection<CustomAttribute> ((int) length);
			ReadCustomAttributeRange (new Range (1, length), custom_attributes);

			return custom_attributes;
		}

		public byte [] ReadCustomAttributeBlob (uint signature)
		{
			return ReadBlob (signature);
		}

		public void ReadCustomAttributeSignature (CustomAttribute attribute)
		{
			var reader = ReadSignature (attribute.signature, out PByteBuffer sig);

			if (!sig.CanReadMore ())
				return;

			if (sig.ReadUInt16 () != 0x0001)
				throw new InvalidOperationException ();

			var constructor = attribute.Constructor;
			if (constructor.HasParameters)
				reader.ReadCustomAttributeConstructorArguments (attribute, constructor.Parameters, ref sig);

			if (!sig.CanReadMore ())
				return;

			var named = sig.ReadUInt16 ();

			if (named == 0)
				return;

			reader.ReadCustomAttributeNamedArguments (named, ref attribute.fields, ref attribute.properties, ref sig);
		}

		void InitializeMarshalInfos ()
		{
			if (metadata.FieldMarshals != null)
				return;

			var length = MoveTo (Table.FieldMarshal, out PByteBuffer buffer);

			var marshals = metadata.FieldMarshals = new Dictionary<MetadataToken, uint> (length);

			for (int i = 0; i < length; i++) {
				var token = ReadMetadataToken (CodedIndex.HasFieldMarshal, ref buffer);
				var signature = ReadBlobIndex (ref buffer);
				if (token.RID == 0)
					continue;

				marshals.Add (token, signature);
			}
		}

		public bool HasMarshalInfo (IMarshalInfoProvider owner)
		{
			InitializeMarshalInfos ();

			return metadata.FieldMarshals.ContainsKey (owner.MetadataToken);
		}

		public MarshalInfo ReadMarshalInfo (IMarshalInfoProvider owner)
		{
			InitializeMarshalInfos ();

			uint signature;
			if (!metadata.FieldMarshals.TryGetValue (owner.MetadataToken, out signature))
				return null;

			var reader = ReadSignature (signature, out PByteBuffer sig);

			metadata.FieldMarshals.Remove (owner.MetadataToken);

			return reader.ReadMarshalInfo (ref sig);
		}

		void InitializeSecurityDeclarations ()
		{
			if (metadata.SecurityDeclarations != null)
				return;

			metadata.SecurityDeclarations = InitializeRanges (
				Table.DeclSecurity, (ref PByteBuffer buffer) => {
					buffer.ReadUInt16 ();
					var next = ReadMetadataToken (CodedIndex.HasDeclSecurity, ref buffer);
					ReadBlobIndex (ref buffer);
					return next;
			});
		}

		public bool HasSecurityDeclarations (ISecurityDeclarationProvider owner)
		{
			InitializeSecurityDeclarations ();

			Range [] ranges;
			if (!metadata.TryGetSecurityDeclarationRanges (owner, out ranges))
				return false;

			return RangesSize (ranges) > 0;
		}

		public Collection<SecurityDeclaration> ReadSecurityDeclarations (ISecurityDeclarationProvider owner)
		{
			InitializeSecurityDeclarations ();

			Range [] ranges;
			if (!metadata.TryGetSecurityDeclarationRanges (owner, out ranges))
				return new Collection<SecurityDeclaration> ();

			var security_declarations = new Collection<SecurityDeclaration> (RangesSize (ranges));

			for (int i = 0; i < ranges.Length; i++)
				ReadSecurityDeclarationRange (ranges [i], security_declarations);

			metadata.RemoveSecurityDeclarationRange (owner);

			return security_declarations;
		}

		void ReadSecurityDeclarationRange (Range range, Collection<SecurityDeclaration> security_declarations)
		{
			if (!MoveTo (Table.DeclSecurity, range.Start, out PByteBuffer buffer))
				return;

			for (int i = 0; i < range.Length; i++) {
				var action = (SecurityAction) buffer.ReadUInt16 ();
				ReadMetadataToken (CodedIndex.HasDeclSecurity, ref buffer);
				var signature = ReadBlobIndex (ref buffer);

				security_declarations.Add (new SecurityDeclaration (action, signature, module));
			}
		}

		public byte [] ReadSecurityDeclarationBlob (uint signature)
		{
			return ReadBlob (signature);
		}

		public void ReadSecurityDeclarationSignature (SecurityDeclaration declaration)
		{
			var signature = declaration.signature;
			var reader = ReadSignature (signature, out PByteBuffer sig);

			if (sig.PeekByte () != '.') {
				ReadXmlSecurityDeclaration (signature, declaration);
				return;
			}

			sig.Advance (1);
			var count = sig.ReadCompressedUInt32 ();
			var attributes = new Collection<SecurityAttribute> ((int) count);

			for (int i = 0; i < count; i++)
				attributes.Add (reader.ReadSecurityAttribute (ref sig));

			declaration.security_attributes = attributes;
		}

		void ReadXmlSecurityDeclaration (uint signature, SecurityDeclaration declaration)
		{
			var attributes = new Collection<SecurityAttribute> (1);

			var attribute = new SecurityAttribute (
				module.TypeSystem.LookupType ("System.Security.Permissions", "PermissionSetAttribute"));

			attribute.properties = new Collection<CustomAttributeNamedArgument> (1);
			attribute.properties.Add (
				new CustomAttributeNamedArgument (
					"XML",
					new CustomAttributeArgument (
						module.TypeSystem.String,
						ReadUnicodeStringBlob (signature))));

			attributes.Add (attribute);

			declaration.security_attributes = attributes;
		}

		public Collection<ExportedType> ReadExportedTypes ()
		{
			var length = MoveTo (Table.ExportedType, out PByteBuffer buffer);
			if (length == 0)
				return new Collection<ExportedType> ();

			var exported_types = new Collection<ExportedType> (length);

			for (int i = 1; i <= length; i++) {
				var attributes = (TypeAttributes) buffer.ReadUInt32 ();
				var identifier = buffer.ReadUInt32 ();
				var name = ReadString (ref buffer);
				var @namespace = ReadString (ref buffer);
				var implementation = ReadMetadataToken (CodedIndex.Implementation, ref buffer);

				ExportedType declaring_type = null;
				IMetadataScope scope = null;

				switch (implementation.TokenType) {
				case TokenType.AssemblyRef:
				case TokenType.File:
					scope = GetExportedTypeScope (implementation);
					break;
				case TokenType.ExportedType:
					// FIXME: if the table is not properly sorted
					declaring_type = exported_types [(int) implementation.RID - 1];
					break;
				}

				var exported_type = new ExportedType (@namespace, name, module, scope) {
					Attributes = attributes,
					Identifier = (int) identifier,
					DeclaringType = declaring_type,
				};
				exported_type.token = new MetadataToken (TokenType.ExportedType, i);

				exported_types.Add (exported_type);
			}

			return exported_types;
		}

		IMetadataScope GetExportedTypeScope (MetadataToken token)
		{
			IMetadataScope scope;

			switch (token.TokenType) {
			case TokenType.AssemblyRef:
				InitializeAssemblyReferences ();
				scope = metadata.GetAssemblyNameReference (token.RID);
				break;
			case TokenType.File:
				InitializeModuleReferences ();
				scope = GetModuleReferenceFromFile (token);
				break;
			default:
				throw new NotSupportedException ();
			}

			return scope;
		}

		ModuleReference GetModuleReferenceFromFile (MetadataToken token)
		{
			if (!MoveTo (Table.File, token.RID, out PByteBuffer buffer))
				return null;

			buffer.Advance (4);
			var file_name = ReadString (ref buffer);
			var modules = module.ModuleReferences;

			ModuleReference reference;
			for (int i = 0; i < modules.Count; i++) {
				reference = modules [i];
				if (reference.Name == file_name)
					return reference;
			}

			reference = new ModuleReference (file_name);
			modules.Add (reference);
			return reference;
		}

		void InitializeDocuments ()
		{
			if (metadata.Documents != null)
				return;

			int length = MoveTo (Table.Document, out PByteBuffer buffer);

			var documents = metadata.Documents = new Document [length];

			for (uint i = 1; i <= length; i++) {
				var name_index = ReadBlobIndex (ref buffer);
				var hash_algorithm = ReadGuid (ref buffer);
				var hash = ReadBlob (ref buffer);
				var language = ReadGuid (ref buffer);

				var signature = ReadSignature (name_index, out PByteBuffer sig);
				var name = signature.ReadDocumentName (ref sig);

				documents [i - 1] = new Document (name) {
					HashAlgorithmGuid = hash_algorithm,
					Hash = hash,
					LanguageGuid = language,
					token = new MetadataToken (TokenType.Document, i),
				};
			}
		}

		public Collection<SequencePoint> ReadSequencePoints (MethodDefinition method)
		{
			InitializeDocuments ();

			if (!MoveTo (Table.MethodDebugInformation, method.MetadataToken.RID, out PByteBuffer buffer))
				return new Collection<SequencePoint> (0);

			var document_index = ReadTableIndex (Table.Document, ref buffer);
			var signature = ReadBlobIndex (ref buffer);
			if (signature == 0)
				return new Collection<SequencePoint> (0);

			var document = GetDocument (document_index);
			var reader = ReadSignature (signature, out PByteBuffer sig);

			return reader.ReadSequencePoints (document, ref sig);
		}

		public Document GetDocument (uint rid)
		{
			var document = metadata.GetDocument (rid);
			if (document == null)
				return null;

			document.custom_infos = GetCustomDebugInformation (document);
			return document;
		}

		void InitializeLocalScopes ()
		{
			if (metadata.LocalScopes != null)
				return;

			InitializeMethods ();

			int length = MoveTo (Table.LocalScope, out PByteBuffer buffer);

			metadata.LocalScopes = new Dictionary<uint, Collection<Row<uint, Range, Range, uint, uint, uint>>> ();

			for (uint i = 1; i <= length; i++) {
				var method = ReadTableIndex (Table.Method, ref buffer);
				var import = ReadTableIndex (Table.ImportScope, ref buffer);
				var variables = ReadListRange (i, Table.LocalScope, Table.LocalVariable, ref buffer);
				var constants = ReadListRange (i, Table.LocalScope, Table.LocalConstant, ref buffer);
				var scope_start = buffer.ReadUInt32 ();
				var scope_length = buffer.ReadUInt32 ();

				metadata.SetLocalScopes (method, AddMapping (metadata.LocalScopes, method, new Row<uint, Range, Range, uint, uint, uint> (import, variables, constants, scope_start, scope_length, i)));
			}
		}

		public ScopeDebugInformation ReadScope (MethodDefinition method)
		{
			InitializeLocalScopes ();
			InitializeImportScopes ();

			Collection<Row<uint, Range, Range, uint, uint, uint>> records;
			if (!metadata.TryGetLocalScopes (method, out records))
				return null;

			var method_scope = null as ScopeDebugInformation;

			for (int i = 0; i < records.Count; i++) {
				var scope = ReadLocalScope (records [i]);

				if (i == 0) {
					method_scope = scope;
					continue;
				}

				if (!AddScope (method_scope.scopes, scope))
					method_scope.Scopes.Add (scope);
			}

			return method_scope;
		}

		static bool AddScope (Collection<ScopeDebugInformation> scopes, ScopeDebugInformation scope)
		{
			if (scopes.IsNullOrEmpty ())
				return false;

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

		ScopeDebugInformation ReadLocalScope (Row<uint, Range, Range, uint, uint, uint> record)
		{
			var scope = new ScopeDebugInformation
			{
				start = new InstructionOffset ((int) record.Col4),
				end = new InstructionOffset ((int) (record.Col4 + record.Col5)),
				token = new MetadataToken (TokenType.LocalScope, record.Col6),
			};

			if (record.Col1 > 0)
				scope.import = metadata.GetImportScope (record.Col1);

			if (record.Col2.Length > 0) {
				scope.variables = new Collection<VariableDebugInformation> ((int) record.Col2.Length);
				for (uint i = 0; i < record.Col2.Length; i++) {
					var variable = ReadLocalVariable (record.Col2.Start + i);
					if (variable != null)
						scope.variables.Add (variable);
				}
			}

			if (record.Col3.Length > 0) {
				scope.constants = new Collection<ConstantDebugInformation> ((int) record.Col3.Length);
				for (uint i = 0; i < record.Col3.Length; i++) {
					var constant = ReadLocalConstant (record.Col3.Start + i);
					if (constant != null)
						scope.constants.Add (constant);
				}
			}

			return scope;
		}

		VariableDebugInformation ReadLocalVariable (uint rid)
		{
			if (!MoveTo (Table.LocalVariable, rid, out PByteBuffer buffer))
				return null;

			var attributes = (VariableAttributes) buffer.ReadUInt16 ();
			var index = buffer.ReadUInt16 ();
			var name = ReadString (ref buffer);

			var variable = new VariableDebugInformation (index, name) { Attributes = attributes, token = new MetadataToken (TokenType.LocalVariable, rid) };
			variable.custom_infos = GetCustomDebugInformation (variable);
			return variable;
		}

		ConstantDebugInformation ReadLocalConstant (uint rid)
		{
			if (!MoveTo (Table.LocalConstant, rid, out PByteBuffer buffer))
				return null;

			var name = ReadString (ref buffer);
			var signature = ReadSignature (ReadBlobIndex (ref buffer), out PByteBuffer sig);
			var type = signature.ReadTypeSignature (ref sig);

			object value;
			if (type.etype == ElementType.String) {
				if (sig.PeekByte () != 0xff) {
					var bytes = sig.ReadBytes ((int) (sig.e - sig.p));
					value = Encoding.Unicode.GetString (bytes, 0, bytes.Length);
				} else
					value = null;
			} else if (type.IsTypeOf ("System", "Decimal")) {
				var bb = sig.ReadByte ();
				value = new decimal (sig.ReadInt32 (), sig.ReadInt32 (), sig.ReadInt32 (), (bb & 0x80) != 0, (byte) (bb & 0x7f));
			} else if (type.IsTypeOf ("System", "DateTime")) {
				value = new DateTime (sig.ReadInt64());
			} else if (type.etype == ElementType.Object || type.etype == ElementType.None || type.etype == ElementType.Class) {
				value = null;
			} else
				value = signature.ReadConstantSignature (type.etype, ref sig);

			var constant = new ConstantDebugInformation (name, type, value) { token = new MetadataToken (TokenType.LocalConstant, rid) };
			constant.custom_infos = GetCustomDebugInformation (constant);
			return constant;
		}

		void InitializeImportScopes ()
		{
			if (metadata.ImportScopes != null)
				return;

			var length = MoveTo (Table.ImportScope, out PByteBuffer buffer);

			metadata.ImportScopes = new ImportDebugInformation [length];

			for (int i = 1; i <= length; i++) {
				ReadTableIndex (Table.ImportScope, ref buffer);

				var import = new ImportDebugInformation ();
				import.token = new MetadataToken (TokenType.ImportScope, i);

				var reader = ReadSignature (ReadBlobIndex (ref buffer), out PByteBuffer sig);
				while (sig.CanReadMore ())
					import.Targets.Add (ReadImportTarget (reader, ref sig));

				metadata.ImportScopes [i - 1] = import;
			}

			MoveTo (Table.ImportScope, out buffer);

			for (int i = 0; i < length; i++) {
				var parent = ReadTableIndex (Table.ImportScope, ref buffer);

				ReadBlobIndex (ref buffer);

				if (parent != 0)
					metadata.ImportScopes [i].Parent = metadata.GetImportScope (parent);
			}
		}

		public string ReadUTF8StringBlob (uint signature)
		{
			return ReadStringBlob (signature, Encoding.UTF8);
		}

		string ReadUnicodeStringBlob (uint signature)
		{
			return ReadStringBlob (signature, Encoding.Unicode);
		}

		string ReadStringBlob (uint signature, Encoding encoding)
		{
			byte [] blob;
			int index, count;

			GetBlobView (signature, out blob, out index, out count);
			if (count == 0)
				return string.Empty;

			return encoding.GetString (blob, index, count);
		}

		ImportTarget ReadImportTarget (SignatureReader signature, ref PByteBuffer sig)
		{
			AssemblyNameReference reference = null;
			string @namespace = null;
			string alias = null;
			TypeReference type = null;

			var kind = (ImportTargetKind) sig.ReadCompressedUInt32 ();
			switch (kind) {
			case ImportTargetKind.ImportNamespace:
				@namespace = ReadUTF8StringBlob (sig.ReadCompressedUInt32 ());
				break;
			case ImportTargetKind.ImportNamespaceInAssembly:
				reference = metadata.GetAssemblyNameReference (sig.ReadCompressedUInt32 ());
				@namespace = ReadUTF8StringBlob (sig.ReadCompressedUInt32 ());
				break;
			case ImportTargetKind.ImportType:
				type = signature.ReadTypeToken (ref sig);
				break;
			case ImportTargetKind.ImportXmlNamespaceWithAlias:
				alias = ReadUTF8StringBlob (sig.ReadCompressedUInt32 ());
				@namespace = ReadUTF8StringBlob (sig.ReadCompressedUInt32 ());
				break;
			case ImportTargetKind.ImportAlias:
				alias = ReadUTF8StringBlob (sig.ReadCompressedUInt32 ());
				break;
			case ImportTargetKind.DefineAssemblyAlias:
				alias = ReadUTF8StringBlob (sig.ReadCompressedUInt32 ());
				reference = metadata.GetAssemblyNameReference (sig.ReadCompressedUInt32 ());
				break;
			case ImportTargetKind.DefineNamespaceAlias:
				alias = ReadUTF8StringBlob (sig.ReadCompressedUInt32 ());
				@namespace = ReadUTF8StringBlob (sig.ReadCompressedUInt32 ());
				break;
			case ImportTargetKind.DefineNamespaceInAssemblyAlias:
				alias = ReadUTF8StringBlob (sig.ReadCompressedUInt32 ());
				reference = metadata.GetAssemblyNameReference (sig.ReadCompressedUInt32 ());
				@namespace = ReadUTF8StringBlob (sig.ReadCompressedUInt32 ());
				break;
			case ImportTargetKind.DefineTypeAlias:
				alias = ReadUTF8StringBlob (sig.ReadCompressedUInt32 ());
				type = signature.ReadTypeToken (ref sig);
				break;
			}

			return new ImportTarget (kind) {
				alias = alias,
				type = type,
				@namespace = @namespace,
				reference = reference,
			};
		}

		void InitializeStateMachineMethods ()
		{
			if (metadata.StateMachineMethods != null)
				return;

			var length = MoveTo (Table.StateMachineMethod, out PByteBuffer buffer);

			metadata.StateMachineMethods = new Dictionary<uint, uint> (length);

			for (int i = 0; i < length; i++)
				metadata.StateMachineMethods.Add (ReadTableIndex (Table.Method, ref buffer), ReadTableIndex (Table.Method, ref buffer));
		}

		public MethodDefinition ReadStateMachineKickoffMethod (MethodDefinition method)
		{
			InitializeStateMachineMethods ();

			uint rid;
			if (!metadata.TryGetStateMachineKickOffMethod (method, out rid))
				return null;

			return GetMethodDefinition (rid);
		}

		void InitializeCustomDebugInformations ()
		{
			if (metadata.CustomDebugInformations != null)
				return;

			var length = MoveTo (Table.CustomDebugInformation, out PByteBuffer buffer);

			metadata.CustomDebugInformations = new Dictionary<MetadataToken, Row<Guid, uint, uint> []> ();

			for (uint i = 1; i <= length; i++) {
				var token = ReadMetadataToken (CodedIndex.HasCustomDebugInformation, ref buffer);
				var info = new Row<Guid, uint, uint> (ReadGuid (ref buffer), ReadBlobIndex (ref buffer), i);

				Row<Guid, uint, uint> [] infos;
				metadata.CustomDebugInformations.TryGetValue (token, out infos);
				metadata.CustomDebugInformations [token] = infos.Add (info);
			}
		}

		public Collection<CustomDebugInformation> GetCustomDebugInformation (ICustomDebugInformationProvider provider)
		{
			InitializeCustomDebugInformations ();

			Row<Guid, uint, uint> [] rows;
			if (!metadata.CustomDebugInformations.TryGetValue (provider.MetadataToken, out rows))
				return null;

			var infos = new Collection<CustomDebugInformation> (rows.Length);

			for (int i = 0; i < rows.Length; i++) {
				if (rows [i].Col1 == StateMachineScopeDebugInformation.KindIdentifier) {
					ReadSignature (rows [i].Col2, out PByteBuffer sig);

					var scopes = new Collection<StateMachineScope> ();

					while (sig.CanReadMore ()) {
						var start = sig.ReadInt32 ();
						var end = start + sig.ReadInt32 ();
						scopes.Add (new StateMachineScope (start, end));
					}

					var state_machine = new StateMachineScopeDebugInformation ();
					state_machine.scopes = scopes;

					infos.Add (state_machine);
				} else if (rows [i].Col1 == AsyncMethodBodyDebugInformation.KindIdentifier) {
					ReadSignature (rows [i].Col2, out PByteBuffer sig);

					var catch_offset = sig.ReadInt32 () - 1;
					var yields = new Collection<InstructionOffset> ();
					var resumes = new Collection<InstructionOffset> ();
					var resume_methods = new Collection<MethodDefinition> ();

					while (sig.CanReadMore ()) {
						yields.Add (new InstructionOffset (sig.ReadInt32 ()));
						resumes.Add (new InstructionOffset (sig.ReadInt32 ()));
						resume_methods.Add (GetMethodDefinition (sig.ReadCompressedUInt32 ()));
					}

					var async_body = new AsyncMethodBodyDebugInformation (catch_offset);
					async_body.yields = yields;
					async_body.resumes = resumes;
					async_body.resume_methods = resume_methods;

					infos.Add (async_body);
				} else if (rows [i].Col1 == EmbeddedSourceDebugInformation.KindIdentifier) {
					ReadSignature (rows [i].Col2, out PByteBuffer sig);

					var format = sig.ReadInt32 ();
					var length = sig.span.length - 4;

					var info = null as CustomDebugInformation;

					if (format == 0) {
						info = new EmbeddedSourceDebugInformation (sig.ReadBytes ((int) length), compress: false);
					} else if (format > 0) {
						var compressed_stream = new MemoryStream (sig.ReadBytes ((int) length));
						var decompressed_document = new byte [format]; // if positive, format is the decompressed length of the document
						var decompressed_stream = new MemoryStream (decompressed_document);

						using (var deflate_stream = new DeflateStream (compressed_stream, CompressionMode.Decompress, leaveOpen: true))
							deflate_stream.CopyTo (decompressed_stream);

						info = new EmbeddedSourceDebugInformation (decompressed_document, compress: true);
					} else if (format < 0) {
						info = new BinaryCustomDebugInformation (rows [i].Col1, ReadBlob (rows [i].Col2));
					}

					infos.Add (info);
				} else if (rows [i].Col1 == SourceLinkDebugInformation.KindIdentifier) {
					infos.Add (new SourceLinkDebugInformation (Encoding.UTF8.GetString (ReadBlob (rows [i].Col2))));
				} else {
					infos.Add (new BinaryCustomDebugInformation (rows [i].Col1, ReadBlob (rows [i].Col2)));
				}

				infos [i].token = new MetadataToken (TokenType.CustomDebugInformation, rows [i].Col3);
			}

			return infos;
		}
	}

	ref struct SignatureReader {

		readonly MetadataReader reader;

		TypeSystem TypeSystem {
			get { return reader.module.TypeSystem; }
		}

		public SignatureReader (MetadataReader reader)
		{
			this.reader = reader;
		}

		MetadataToken ReadTypeTokenSignature (ref PByteBuffer buffer)
		{
			return CodedIndex.TypeDefOrRef.GetMetadataToken (buffer.ReadCompressedUInt32 ());
		}

		GenericParameter GetGenericParameter (GenericParameterType type, uint var)
		{
			var context = reader.context;
			int index = (int) var;

			if (context == null)
				return GetUnboundGenericParameter (type, index);

			IGenericParameterProvider provider;

			switch (type) {
			case GenericParameterType.Type:
				provider = context.Type;
				break;
			case GenericParameterType.Method:
				provider = context.Method;
				break;
			default:
				throw new NotSupportedException ();
			}

			if (!context.IsDefinition)
				CheckGenericContext (provider, index);

			if (index >= provider.GenericParameters.Count)
				return GetUnboundGenericParameter (type, index);

			return provider.GenericParameters [index];
		}

		GenericParameter GetUnboundGenericParameter (GenericParameterType type, int index)
		{
			return new GenericParameter (index, type, reader.module);
		}

		static void CheckGenericContext (IGenericParameterProvider owner, int index)
		{
			var owner_parameters = owner.GenericParameters;

			for (int i = owner_parameters.Count; i <= index; i++)
				owner_parameters.Add (new GenericParameter (owner));
		}

		public void ReadGenericInstanceSignature (IGenericParameterProvider provider, IGenericInstance instance, uint arity, ref PByteBuffer buffer)
		{
			if (!provider.IsDefinition)
				CheckGenericContext (provider, (int) arity - 1);

			var instance_arguments = instance.GenericArguments;

			for (int i = 0; i < arity; i++)
				instance_arguments.Add (ReadTypeSignature (ref buffer));
		}

		ArrayType ReadArrayTypeSignature (ref PByteBuffer buffer)
		{
			var array = new ArrayType (ReadTypeSignature (ref buffer));

			var rank = buffer.ReadCompressedUInt32 ();

			var sizes = new uint [buffer.ReadCompressedUInt32 ()];
			for (int i = 0; i < sizes.Length; i++)
				sizes [i] = buffer.ReadCompressedUInt32 ();

			var low_bounds = new int [buffer.ReadCompressedUInt32 ()];
			for (int i = 0; i < low_bounds.Length; i++)
				low_bounds [i] = buffer.ReadCompressedInt32 ();

			array.Dimensions.Clear ();

			for (int i = 0; i < rank; i++) {
				int? lower = null, upper = null;

				if (i < low_bounds.Length)
					lower = low_bounds [i];

				if (i < sizes.Length)
					upper = lower + (int) sizes [i] - 1;

				array.Dimensions.Add (new ArrayDimension (lower, upper));
			}

			return array;
		}

		TypeReference GetTypeDefOrRef (MetadataToken token)
		{
			return reader.GetTypeDefOrRef (token);
		}

		public TypeReference ReadTypeSignature (ref PByteBuffer buffer)
		{
			return ReadTypeSignature ((ElementType) buffer.ReadByte (), ref buffer);
		}

		public TypeReference ReadTypeToken (ref PByteBuffer buffer)
		{
			return GetTypeDefOrRef (ReadTypeTokenSignature (ref buffer));
		}

		TypeReference ReadTypeSignature (ElementType etype, ref PByteBuffer buffer)
		{
			switch (etype) {
			case ElementType.ValueType: {
				var value_type = GetTypeDefOrRef (ReadTypeTokenSignature (ref buffer));
				value_type.KnownValueType ();
				return value_type;
			}
			case ElementType.Class:
				return GetTypeDefOrRef (ReadTypeTokenSignature (ref buffer));
			case ElementType.Ptr:
				return new PointerType (ReadTypeSignature (ref buffer));
			case ElementType.FnPtr: {
				var fptr = new FunctionPointerType ();
				ReadMethodSignature (fptr, ref buffer);
				return fptr;
			}
			case ElementType.ByRef:
				return new ByReferenceType (ReadTypeSignature (ref buffer));
			case ElementType.Pinned:
				return new PinnedType (ReadTypeSignature (ref buffer));
			case ElementType.SzArray:
				return new ArrayType (ReadTypeSignature (ref buffer));
			case ElementType.Array:
				return ReadArrayTypeSignature (ref buffer);
			case ElementType.CModOpt:
				return new OptionalModifierType (
					GetTypeDefOrRef (ReadTypeTokenSignature (ref buffer)), ReadTypeSignature (ref buffer));
			case ElementType.CModReqD:
				return new RequiredModifierType (
					GetTypeDefOrRef (ReadTypeTokenSignature (ref buffer)), ReadTypeSignature (ref buffer));
			case ElementType.Sentinel:
				return new SentinelType (ReadTypeSignature (ref buffer));
			case ElementType.Var:
				return GetGenericParameter (GenericParameterType.Type, buffer.ReadCompressedUInt32 ());
			case ElementType.MVar:
				return GetGenericParameter (GenericParameterType.Method, buffer.ReadCompressedUInt32 ());
			case ElementType.GenericInst: {
				var is_value_type = buffer.ReadByte () == (byte) ElementType.ValueType;
				var element_type = GetTypeDefOrRef (ReadTypeTokenSignature (ref buffer));

				var arity = buffer.ReadCompressedUInt32 ();
				var generic_instance = new GenericInstanceType (element_type);

				ReadGenericInstanceSignature (element_type, generic_instance, arity, ref buffer);

				if (is_value_type) {
					generic_instance.KnownValueType ();
					element_type.GetElementType ().KnownValueType ();
				}

				return generic_instance;
			}
			case ElementType.Object: return TypeSystem.Object;
			case ElementType.Void: return TypeSystem.Void;
			case ElementType.TypedByRef: return TypeSystem.TypedReference;
			case ElementType.I: return TypeSystem.IntPtr;
			case ElementType.U: return TypeSystem.UIntPtr;
			default: return GetPrimitiveType (etype);
			}
		}

		public void ReadMethodSignature (IMethodSignature method, ref PByteBuffer buffer)
		{
			var calling_convention = buffer.ReadByte ();

			const byte has_this = 0x20;
			const byte explicit_this = 0x40;

			if ((calling_convention & has_this) != 0) {
				method.HasThis = true;
				calling_convention = (byte) (calling_convention & ~has_this);
			}

			if ((calling_convention & explicit_this) != 0) {
				method.ExplicitThis = true;
				calling_convention = (byte) (calling_convention & ~explicit_this);
			}

			method.CallingConvention = (MethodCallingConvention) calling_convention;

			var generic_context = method as MethodReference;
			if (generic_context != null && !generic_context.DeclaringType.IsArray)
				reader.context = generic_context;

			if ((calling_convention & 0x10) != 0) {
				var arity = buffer.ReadCompressedUInt32 ();

				if (generic_context != null && !generic_context.IsDefinition)
					CheckGenericContext (generic_context, (int) arity -1 );
			}

			var param_count = buffer.ReadCompressedUInt32 ();

			method.MethodReturnType.ReturnType = ReadTypeSignature (ref buffer);

			if (param_count == 0)
				return;

			Collection<ParameterDefinition> parameters;

			var method_ref = method as MethodReference;
			if (method_ref != null)
				parameters = method_ref.parameters = new ParameterDefinitionCollection (method, (int) param_count);
			else
				parameters = method.Parameters;

			for (int i = 0; i < param_count; i++)
				parameters.Add (new ParameterDefinition (ReadTypeSignature (ref buffer)));
		}

		public object ReadConstantSignature (ElementType type, ref PByteBuffer buffer)
		{
			return ReadPrimitiveValue (type, ref buffer);
		}

		public void ReadCustomAttributeConstructorArguments (CustomAttribute attribute, Collection<ParameterDefinition> parameters, ref PByteBuffer buffer)
		{
			var count = parameters.Count;
			if (count == 0)
				return;

			attribute.arguments = new Collection<CustomAttributeArgument> (count);

			for (int i = 0; i < count; i++)
				attribute.arguments.Add (
					ReadCustomAttributeFixedArgument (parameters [i].ParameterType, ref buffer));
		}

		CustomAttributeArgument ReadCustomAttributeFixedArgument (TypeReference type, ref PByteBuffer buffer)
		{
			if (type.IsArray)
				return ReadCustomAttributeFixedArrayArgument ((ArrayType) type, ref buffer);

			return ReadCustomAttributeElement (type, ref buffer);
		}

		public void ReadCustomAttributeNamedArguments (ushort count, ref Collection<CustomAttributeNamedArgument> fields, ref Collection<CustomAttributeNamedArgument> properties, ref PByteBuffer buffer)
		{
			for (int i = 0; i < count; i++) {
				if (!buffer.CanReadMore ())
					return;
				ReadCustomAttributeNamedArgument (ref fields, ref properties, ref buffer);
			}
		}

		void ReadCustomAttributeNamedArgument (ref Collection<CustomAttributeNamedArgument> fields, ref Collection<CustomAttributeNamedArgument> properties, ref PByteBuffer buffer)
		{
			var kind = buffer.ReadByte ();
			var type = ReadCustomAttributeFieldOrPropType (ref buffer);
			var name = ReadUTF8String (ref buffer);

			Collection<CustomAttributeNamedArgument> container;
			switch (kind) {
			case 0x53:
				container = GetCustomAttributeNamedArgumentCollection (ref fields);
				break;
			case 0x54:
				container = GetCustomAttributeNamedArgumentCollection (ref properties);
				break;
			default:
				throw new NotSupportedException ();
			}

			container.Add (new CustomAttributeNamedArgument (name, ReadCustomAttributeFixedArgument (type, ref buffer)));
		}

		static Collection<CustomAttributeNamedArgument> GetCustomAttributeNamedArgumentCollection (ref Collection<CustomAttributeNamedArgument> collection)
		{
			if (collection != null)
				return collection;

			return collection = new Collection<CustomAttributeNamedArgument> ();
		}

		CustomAttributeArgument ReadCustomAttributeFixedArrayArgument (ArrayType type, ref PByteBuffer buffer)
		{
			var length = buffer.ReadUInt32 ();

			if (length == 0xffffffff)
				return new CustomAttributeArgument (type, null);

			if (length == 0)
				return new CustomAttributeArgument (type, Empty<CustomAttributeArgument>.Array);

			var arguments = new CustomAttributeArgument [length];
			var element_type = type.ElementType;

			for (int i = 0; i < length; i++)
				arguments [i] = ReadCustomAttributeElement (element_type, ref buffer);

			return new CustomAttributeArgument (type, arguments);
		}

		CustomAttributeArgument ReadCustomAttributeElement (TypeReference type, ref PByteBuffer buffer)
		{
			if (type.IsArray)
				return ReadCustomAttributeFixedArrayArgument ((ArrayType) type, ref buffer);

			return new CustomAttributeArgument (
				type,
				type.etype == ElementType.Object
					? ReadCustomAttributeElement (ReadCustomAttributeFieldOrPropType (ref buffer), ref buffer)
					: ReadCustomAttributeElementValue (type, ref buffer));
		}

		object ReadCustomAttributeElementValue (TypeReference type, ref PByteBuffer buffer)
		{
			var etype = type.etype;

			switch (etype) {
			case ElementType.String:
				return ReadUTF8String (ref buffer);
			case ElementType.None:
				if (type.IsTypeOf ("System", "Type"))
					return ReadTypeReference (ref buffer);

				return ReadCustomAttributeEnum (type, ref buffer);
			default:
				return ReadPrimitiveValue (etype, ref buffer);
			}
		}

		object ReadPrimitiveValue (ElementType type, ref PByteBuffer buffer)
		{
			switch (type) {
			case ElementType.Boolean:
				return buffer.ReadByte () == 1;
			case ElementType.I1:
				return (sbyte) buffer.ReadByte ();
			case ElementType.U1:
				return buffer.ReadByte ();
			case ElementType.Char:
				return (char) buffer.ReadUInt16 ();
			case ElementType.I2:
				return buffer.ReadInt16 ();
			case ElementType.U2:
				return buffer.ReadUInt16 ();
			case ElementType.I4:
				return buffer.ReadInt32 ();
			case ElementType.U4:
				return buffer.ReadUInt32 ();
			case ElementType.I8:
				return buffer.ReadInt64 ();
			case ElementType.U8:
				return buffer.ReadUInt64 ();
			case ElementType.R4:
				return buffer.ReadSingle ();
			case ElementType.R8:
				return buffer.ReadDouble ();
			default:
				throw new NotImplementedException (type.ToString ());
			}
		}

		TypeReference GetPrimitiveType (ElementType etype)
		{
			switch (etype) {
			case ElementType.Boolean:
				return TypeSystem.Boolean;
			case ElementType.Char:
				return TypeSystem.Char;
			case ElementType.I1:
				return TypeSystem.SByte;
			case ElementType.U1:
				return TypeSystem.Byte;
			case ElementType.I2:
				return TypeSystem.Int16;
			case ElementType.U2:
				return TypeSystem.UInt16;
			case ElementType.I4:
				return TypeSystem.Int32;
			case ElementType.U4:
				return TypeSystem.UInt32;
			case ElementType.I8:
				return TypeSystem.Int64;
			case ElementType.U8:
				return TypeSystem.UInt64;
			case ElementType.R4:
				return TypeSystem.Single;
			case ElementType.R8:
				return TypeSystem.Double;
			case ElementType.String:
				return TypeSystem.String;
			default:
				throw new NotImplementedException (etype.ToString ());
			}
		}

		TypeReference ReadCustomAttributeFieldOrPropType (ref PByteBuffer buffer)
		{
			var etype = (ElementType) buffer.ReadByte ();

			switch (etype) {
			case ElementType.Boxed:
				return TypeSystem.Object;
			case ElementType.SzArray:
				return new ArrayType (ReadCustomAttributeFieldOrPropType (ref buffer));
			case ElementType.Enum:
				return ReadTypeReference (ref buffer);
			case ElementType.Type:
				return TypeSystem.LookupType ("System", "Type");
			default:
				return GetPrimitiveType (etype);
			}
		}

		public TypeReference ReadTypeReference (ref PByteBuffer buffer)
		{
			return TypeParser.ParseType (reader.module, ReadUTF8String (ref buffer));
		}

		object ReadCustomAttributeEnum (TypeReference enum_type, ref PByteBuffer buffer)
		{
			var type = enum_type.CheckedResolve ();
			if (!type.IsEnum)
				throw new ArgumentException ();

			return ReadCustomAttributeElementValue (type.GetEnumUnderlyingType (), ref buffer);
		}

		public SecurityAttribute ReadSecurityAttribute (ref PByteBuffer buffer)
		{
			var attribute = new SecurityAttribute (ReadTypeReference (ref buffer));

			buffer.ReadCompressedUInt32 ();

			ReadCustomAttributeNamedArguments (
				(ushort) buffer.ReadCompressedUInt32 (),
				ref attribute.fields,
				ref attribute.properties,
				ref buffer);

			return attribute;
		}

		public MarshalInfo ReadMarshalInfo (ref PByteBuffer buffer)
		{
			var native = ReadNativeType (ref buffer);
			switch (native) {
			case NativeType.Array: {
				var array = new ArrayMarshalInfo ();
				if (buffer.CanReadMore ())
					array.element_type = ReadNativeType (ref buffer);
				if (buffer.CanReadMore ())
					array.size_parameter_index = (int) buffer.ReadCompressedUInt32 ();
				if (buffer.CanReadMore ())
					array.size = (int) buffer.ReadCompressedUInt32 ();
				if (buffer.CanReadMore ())
					array.size_parameter_multiplier = (int) buffer.ReadCompressedUInt32 ();
				return array;
			}
			case NativeType.SafeArray: {
				var array = new SafeArrayMarshalInfo ();
				if (buffer.CanReadMore ())
					array.element_type = ReadVariantType (ref buffer);
				return array;
			}
			case NativeType.FixedArray: {
				var array = new FixedArrayMarshalInfo ();
				if (buffer.CanReadMore ())
					array.size = (int) buffer.ReadCompressedUInt32 ();
				if (buffer.CanReadMore ())
					array.element_type = ReadNativeType (ref buffer);
				return array;
			}
			case NativeType.FixedSysString: {
				var sys_string = new FixedSysStringMarshalInfo ();
				if (buffer.CanReadMore ())
					sys_string.size = (int) buffer.ReadCompressedUInt32 ();
				return sys_string;
			}
			case NativeType.CustomMarshaler: {
				var marshaler = new CustomMarshalInfo ();
				var guid_value = ReadUTF8String (ref buffer);
				marshaler.guid = !string.IsNullOrEmpty (guid_value) ? new Guid (guid_value) : Guid.Empty;
				marshaler.unmanaged_type = ReadUTF8String (ref buffer);
				marshaler.managed_type = ReadTypeReference (ref buffer);
				marshaler.cookie = ReadUTF8String (ref buffer);
				return marshaler;
			}
			default:
				return new MarshalInfo (native);
			}
		}

		NativeType ReadNativeType (ref PByteBuffer buffer)
		{
			return (NativeType) buffer.ReadByte ();
		}

		VariantType ReadVariantType (ref PByteBuffer buffer)
		{
			return (VariantType) buffer.ReadByte ();
		}

		string ReadUTF8String (ref PByteBuffer buffer)
		{
			if (buffer.PeekByte () == 0xff) {
				buffer.Advance (1);
				return null;
			}

			var length = (int) buffer.ReadCompressedUInt32 ();
			if (length == 0)
				return string.Empty;

			var bytes = buffer.ReadBytes (length);

			return Encoding.UTF8.GetString (bytes);
		}

		public string ReadDocumentName (ref PByteBuffer buffer)
		{
			var separator = (char) buffer.ReadByte ();

			var builder = new StringBuilder ();
			for (int i = 0; buffer.CanReadMore (); i++) {
				if (i > 0 && separator != 0)
					builder.Append (separator);

				uint part = buffer.ReadCompressedUInt32 ();
				if (part != 0)
					builder.Append (reader.ReadUTF8StringBlob (part));
			}

			return builder.ToString ();
		}

		public Collection<SequencePoint> ReadSequencePoints (Document document, ref PByteBuffer buffer)
		{
			buffer.ReadCompressedUInt32 (); // local_sig_token

			if (document == null)
				document = reader.GetDocument (buffer.ReadCompressedUInt32 ());

			var offset = 0;
			var start_line = 0;
			var start_column = 0;
			var first_non_hidden = true;

			//there's about 5 compressed int32's per sequence points.  we don't know exactly how many
			//but let's take a conservative guess so we dont end up reallocating the sequence_points collection
			//as it grows.
			var estimated_sequencepoint_amount = (int) buffer.RemainingBytes () / 5;
			var sequence_points = new Collection<SequencePoint> (estimated_sequencepoint_amount);
			
			for (var i = 0; buffer.CanReadMore (); i++) {
				var delta_il = (int) buffer.ReadCompressedUInt32 ();
				if (i > 0 && delta_il == 0) {
					document = reader.GetDocument (buffer.ReadCompressedUInt32 ());
					continue;
				}

				offset += delta_il;

				var delta_lines = (int) buffer.ReadCompressedUInt32 ();
				var delta_columns = delta_lines == 0
					? (int) buffer.ReadCompressedUInt32 ()
					: buffer.ReadCompressedInt32 ();

				if (delta_lines == 0 && delta_columns == 0) {
					sequence_points.Add (new SequencePoint (offset, document) {
						StartLine = 0xfeefee,
						EndLine = 0xfeefee,
						StartColumn = 0,
						EndColumn = 0,
					});
					continue;
				}

				if (first_non_hidden) {
					start_line = (int) buffer.ReadCompressedUInt32 ();
					start_column = (int) buffer.ReadCompressedUInt32 ();
				} else {
					start_line += buffer.ReadCompressedInt32 ();
					start_column += buffer.ReadCompressedInt32 ();
				}

				sequence_points.Add (new SequencePoint (offset, document) {
					StartLine = start_line,
					StartColumn = start_column,
					EndLine = start_line + delta_lines,
					EndColumn = start_column + delta_columns,
				});
				first_non_hidden = false;
			}

			return sequence_points;
		}
	}
}
