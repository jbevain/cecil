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
using System.Threading;
using SR = System.Reflection;

using Mono.Cecil.Cil;
using Mono.Cecil.Metadata;
using Mono.Cecil.PE;
using Mono.Collections.Generic;

namespace Mono.Cecil {

	public enum ReadingMode {
		Immediate = 1,
		Deferred = 2,
	}

	public sealed class ReaderParameters {

		ReadingMode reading_mode;
		internal IAssemblyResolver assembly_resolver;
		internal IMetadataResolver metadata_resolver;
#if !READ_ONLY
		internal IMetadataImporterProvider metadata_importer_provider;
#if !PCL
		internal IReflectionImporterProvider reflection_importer_provider;
#endif
#endif
		Stream symbol_stream;
		ISymbolReaderProvider symbol_reader_provider;
		bool read_symbols;

		public ReadingMode ReadingMode {
			get { return reading_mode; }
			set { reading_mode = value; }
		}

		public IAssemblyResolver AssemblyResolver {
			get { return assembly_resolver; }
			set { assembly_resolver = value; }
		}

		public IMetadataResolver MetadataResolver {
			get { return metadata_resolver; }
			set { metadata_resolver = value; }
		}

#if !READ_ONLY
		public IMetadataImporterProvider MetadataImporterProvider {
			get { return metadata_importer_provider; }
			set { metadata_importer_provider = value; }
		}

#if !PCL
		public IReflectionImporterProvider ReflectionImporterProvider {
			get { return reflection_importer_provider; }
			set { reflection_importer_provider = value; }
		}
#endif
#endif

		public Stream SymbolStream {
			get { return symbol_stream; }
			set { symbol_stream = value; }
		}

		public ISymbolReaderProvider SymbolReaderProvider {
			get { return symbol_reader_provider; }
			set { symbol_reader_provider = value; }
		}

		public bool ReadSymbols {
			get { return read_symbols; }
			set { read_symbols = value; }
		}

		public ReaderParameters ()
			: this (ReadingMode.Deferred)
		{
		}

		public ReaderParameters (ReadingMode readingMode)
		{
			this.reading_mode = readingMode;
		}
	}

#if !READ_ONLY

	public sealed class ModuleParameters {

		ModuleKind kind;
		TargetRuntime runtime;
		TargetArchitecture architecture;
		IAssemblyResolver assembly_resolver;
		IMetadataResolver metadata_resolver;
#if !READ_ONLY
		IMetadataImporterProvider metadata_importer_provider;
#if !PCL
		IReflectionImporterProvider reflection_importer_provider;
#endif
#endif

		public ModuleKind Kind {
			get { return kind; }
			set { kind = value; }
		}

		public TargetRuntime Runtime {
			get { return runtime; }
			set { runtime = value; }
		}

		public TargetArchitecture Architecture {
			get { return architecture; }
			set { architecture = value; }
		}

		public IAssemblyResolver AssemblyResolver {
			get { return assembly_resolver; }
			set { assembly_resolver = value; }
		}

		public IMetadataResolver MetadataResolver {
			get { return metadata_resolver; }
			set { metadata_resolver = value; }
		}

#if !READ_ONLY
		public IMetadataImporterProvider MetadataImporterProvider {
			get { return metadata_importer_provider; }
			set { metadata_importer_provider = value; }
		}

#if !PCL
		public IReflectionImporterProvider ReflectionImporterProvider {
			get { return reflection_importer_provider; }
			set { reflection_importer_provider = value; }
		}
#endif
#endif

		public ModuleParameters ()
		{
			this.kind = ModuleKind.Dll;
			this.Runtime = GetCurrentRuntime ();
			this.architecture = TargetArchitecture.I386;
		}

		static TargetRuntime GetCurrentRuntime ()
		{
#if !PCL
			return typeof (object).Assembly.ImageRuntimeVersion.ParseRuntime ();
#else
			var corlib_name = AssemblyNameReference.Parse (typeof (object).Assembly.FullName);
			var corlib_version = corlib_name.Version;
			switch (corlib_version.Major) {
			case 1:
				return corlib_version.Minor == 0
					? TargetRuntime.Net_1_0
					: TargetRuntime.Net_1_1;
			case 2:
				return TargetRuntime.Net_2_0;
			case 4:
				return TargetRuntime.Net_4_0;
			default:
				throw new NotSupportedException ();
			}
#endif
		}
	}

	public sealed class WriterParameters {

		Stream symbol_stream;
		ISymbolWriterProvider symbol_writer_provider;
		bool write_symbols;
#if !PCL
		SR.StrongNameKeyPair key_pair;
#endif
		public Stream SymbolStream {
			get { return symbol_stream; }
			set { symbol_stream = value; }
		}

		public ISymbolWriterProvider SymbolWriterProvider {
			get { return symbol_writer_provider; }
			set { symbol_writer_provider = value; }
		}

		public bool WriteSymbols {
			get { return write_symbols; }
			set { write_symbols = value; }
		}
#if !PCL
		public SR.StrongNameKeyPair StrongNameKeyPair {
			get { return key_pair; }
			set { key_pair = value; }
		}
#endif
	}

#endif

	public sealed class ModuleDefinition : ModuleReference, ICustomAttributeProvider {

		internal Image Image;
		internal MetadataSystem MetadataSystem;
		internal ReadingMode ReadingMode;
		internal ISymbolReaderProvider SymbolReaderProvider;

		internal ISymbolReader symbol_reader;
		internal IAssemblyResolver assembly_resolver;
		internal IMetadataResolver metadata_resolver;
		internal TypeSystem type_system;

		readonly MetadataReader reader;
		readonly string fq_name;

		internal string runtime_version;
		internal ModuleKind kind;
		TargetRuntime runtime;
		TargetArchitecture architecture;
		ModuleAttributes attributes;
		ModuleCharacteristics characteristics;
		Guid mvid;

		internal AssemblyDefinition assembly;
		MethodDefinition entry_point;

#if !READ_ONLY
#if !PCL
		internal IReflectionImporter reflection_importer;
#endif
		internal IMetadataImporter metadata_importer;
#endif
		Collection<CustomAttribute> custom_attributes;
		Collection<AssemblyNameReference> references;
		Collection<ModuleReference> modules;
		Collection<Resource> resources;
		Collection<ExportedType> exported_types;
		TypeDefinitionCollection types;

		public bool IsMain {
			get { return kind != ModuleKind.NetModule; }
		}

		public ModuleKind Kind {
			get { return kind; }
			set { kind = value; }
		}

		public TargetRuntime Runtime {
			get { return runtime; }
			set {
				runtime = value;
				runtime_version = runtime.RuntimeVersionString ();
			}
		}

		public string RuntimeVersion {
			get { return runtime_version; }
			set {
				runtime_version = value;
				runtime = runtime_version.ParseRuntime ();
			}
		}

		public TargetArchitecture Architecture {
			get { return architecture; }
			set { architecture = value; }
		}

		public ModuleAttributes Attributes {
			get { return attributes; }
			set { attributes = value; }
		}

		public ModuleCharacteristics Characteristics {
			get { return characteristics; }
			set { characteristics = value; }
		}

		public string FullyQualifiedName {
			get { return fq_name; }
		}

		public Guid Mvid {
			get { return mvid; }
			set { mvid = value; }
		}

		internal bool HasImage {
			get { return Image != null; }
		}

		public bool HasSymbols {
			get { return symbol_reader != null; }
		}

		public ISymbolReader SymbolReader {
			get { return symbol_reader; }
		}

		public override MetadataScopeType MetadataScopeType {
			get { return MetadataScopeType.ModuleDefinition; }
		}

		public AssemblyDefinition Assembly {
			get { return assembly; }
		}

#if !READ_ONLY
#if !PCL
		internal IReflectionImporter ReflectionImporter {
			get {
				if (reflection_importer == null)
					Interlocked.CompareExchange (ref reflection_importer, new ReflectionImporter (this), null);

				return reflection_importer;
			}
		}
#endif
		internal IMetadataImporter MetadataImporter {
			get {
				if (metadata_importer == null)
					Interlocked.CompareExchange (ref metadata_importer, new MetadataImporter (this), null);

				return metadata_importer;
			}
		}
#endif

		public IAssemblyResolver AssemblyResolver {
			get {
#if !PCL
				if (assembly_resolver == null)
					Interlocked.CompareExchange (ref assembly_resolver, new DefaultAssemblyResolver (), null);
#endif

				return assembly_resolver;
			}
		}

		public IMetadataResolver MetadataResolver {
			get {
				if (metadata_resolver == null)
					Interlocked.CompareExchange (ref metadata_resolver, new MetadataResolver (this.AssemblyResolver), null);

				return metadata_resolver;
			}
		}

		public TypeSystem TypeSystem {
			get {
				if (type_system == null)
					Interlocked.CompareExchange (ref type_system, TypeSystem.CreateTypeSystem (this), null);

				return type_system;
			}
		}

		public bool HasAssemblyReferences {
			get {
				if (references != null)
					return references.Count > 0;

				return HasImage && Image.HasTable (Table.AssemblyRef);
			}
		}

		public Collection<AssemblyNameReference> AssemblyReferences {
			get {
				if (references != null)
					return references;

				if (HasImage)
					return Read (ref references, this, (_, reader) => reader.ReadAssemblyReferences ());

				return references = new Collection<AssemblyNameReference> ();
			}
		}

		public bool HasModuleReferences {
			get {
				if (modules != null)
					return modules.Count > 0;

				return HasImage && Image.HasTable (Table.ModuleRef);
			}
		}

		public Collection<ModuleReference> ModuleReferences {
			get {
				if (modules != null)
					return modules;

				if (HasImage)
					return Read (ref modules, this, (_, reader) => reader.ReadModuleReferences ());

				return modules = new Collection<ModuleReference> ();
			}
		}

		public bool HasResources {
			get {
				if (resources != null)
					return resources.Count > 0;

				if (HasImage)
					return Image.HasTable (Table.ManifestResource) || Read (this, (_, reader) => reader.HasFileResource ());

				return false;
			}
		}

		public Collection<Resource> Resources {
			get {
				if (resources != null)
					return resources;

				if (HasImage)
					return Read (ref resources, this, (_, reader) => reader.ReadResources ());

				return resources = new Collection<Resource> ();
			}
		}

		public bool HasCustomAttributes {
			get {
				if (custom_attributes != null)
					return custom_attributes.Count > 0;

				return this.GetHasCustomAttributes (this);
			}
		}

		public Collection<CustomAttribute> CustomAttributes {
			get { return custom_attributes ?? (this.GetCustomAttributes (ref custom_attributes, this)); }
		}

		public bool HasTypes {
			get {
				if (types != null)
					return types.Count > 0;

				return HasImage && Image.HasTable (Table.TypeDef);
			}
		}

		public Collection<TypeDefinition> Types {
			get {
				if (types != null)
					return types;

				if (HasImage)
					return Read (ref types, this, (_, reader) => reader.ReadTypes ());

				return types = new TypeDefinitionCollection (this);
			}
		}

		public bool HasExportedTypes {
			get {
				if (exported_types != null)
					return exported_types.Count > 0;

				return HasImage && Image.HasTable (Table.ExportedType);
			}
		}

		public Collection<ExportedType> ExportedTypes {
			get {
				if (exported_types != null)
					return exported_types;

				if (HasImage)
					return Read (ref exported_types, this, (_, reader) => reader.ReadExportedTypes ());

				return exported_types = new Collection<ExportedType> ();
			}
		}

		public MethodDefinition EntryPoint {
			get {
				if (entry_point != null)
					return entry_point;

				if (HasImage)
					return Read (ref entry_point, this, (_, reader) => reader.ReadEntryPoint ());

				return entry_point = null;
			}
			set { entry_point = value; }
		}

		internal ModuleDefinition ()
		{
			this.MetadataSystem = new MetadataSystem ();
			this.token = new MetadataToken (TokenType.Module, 1);
		}

		internal ModuleDefinition (Image image)
			: this ()
		{
			this.Image = image;
			this.kind = image.Kind;
			this.RuntimeVersion = image.RuntimeVersion;
			this.architecture = image.Architecture;
			this.attributes = image.Attributes;
			this.characteristics = image.Characteristics;
			this.fq_name = image.FileName;

			this.reader = new MetadataReader (this);
		}

			public bool HasTypeReference (string fullName)
		{
			return HasTypeReference (string.Empty, fullName);
		}

		public bool HasTypeReference (string scope, string fullName)
		{
			CheckFullName (fullName);

			if (!HasImage)
				return false;

			return GetTypeReference (scope, fullName) != null;
		}

		public bool TryGetTypeReference (string fullName, out TypeReference type)
		{
			return TryGetTypeReference (string.Empty, fullName, out type);
		}

		public bool TryGetTypeReference (string scope, string fullName, out TypeReference type)
		{
			CheckFullName (fullName);

			if (!HasImage) {
				type = null;
				return false;
			}

			return (type = GetTypeReference (scope, fullName)) != null;
		}

		TypeReference GetTypeReference (string scope, string fullname)
		{
			return Read (new Row<string, string> (scope, fullname), (row, reader) => reader.GetTypeReference (row.Col1, row.Col2));
		}

		public IEnumerable<TypeReference> GetTypeReferences ()
		{
			if (!HasImage)
				return Empty<TypeReference>.Array;

			return Read (this, (_, reader) => reader.GetTypeReferences ());
		}

		public IEnumerable<MemberReference> GetMemberReferences ()
		{
			if (!HasImage)
				return Empty<MemberReference>.Array;

			return Read (this, (_, reader) => reader.GetMemberReferences ());
		}

		public TypeReference GetType (string fullName, bool runtimeName)
		{
			return runtimeName
				? TypeParser.ParseType (this, fullName)
				: GetType (fullName);
		}

		public TypeDefinition GetType (string fullName)
		{
			CheckFullName (fullName);

			var position = fullName.IndexOf ('/');
			if (position > 0)
				return GetNestedType (fullName);

			return ((TypeDefinitionCollection) this.Types).GetType (fullName);
		}

		public TypeDefinition GetType (string @namespace, string name)
		{
			Mixin.CheckName (name);

			return ((TypeDefinitionCollection) this.Types).GetType (@namespace ?? string.Empty, name);
		}

		public IEnumerable<TypeDefinition> GetTypes ()
		{
			return GetTypes (Types);
		}

		static IEnumerable<TypeDefinition> GetTypes (Collection<TypeDefinition> types)
		{
			for (int i = 0; i < types.Count; i++) {
				var type = types [i];

				yield return type;

				if (!type.HasNestedTypes)
					continue;

				foreach (var nested in GetTypes (type.NestedTypes))
					yield return nested;
			}
		}

		static void CheckFullName (string fullName)
		{
			if (fullName == null)
				throw new ArgumentNullException ("fullName");
			if (fullName.Length == 0)
				throw new ArgumentException ();
		}

		TypeDefinition GetNestedType (string fullname)
		{
			var names = fullname.Split ('/');
			var type = GetType (names [0]);

			if (type == null)
				return null;

			for (int i = 1; i < names.Length; i++) {
				var nested_type = type.GetNestedType (names [i]);
				if (nested_type == null)
					return null;

				type = nested_type;
			}

			return type;
		}

		internal FieldDefinition Resolve (FieldReference field)
		{
#if PCL
			if (MetadataResolver == null)
				throw new NotSupportedException ();
#endif
			return MetadataResolver.Resolve (field);
		}

		internal MethodDefinition Resolve (MethodReference method)
		{
#if PCL
			if (MetadataResolver == null)
				throw new NotSupportedException ();
#endif
			return MetadataResolver.Resolve (method);
		}

		internal TypeDefinition Resolve (TypeReference type)
		{
#if PCL
			if (MetadataResolver == null)
				throw new NotSupportedException ();
#endif
			return MetadataResolver.Resolve (type);
		}

#if !READ_ONLY

		static void CheckContext (IGenericParameterProvider context, ModuleDefinition module)
		{
			if (context == null)
				return;

			if (context.Module != module)
				throw new ArgumentException ();
		}

#if !PCL

		[Obsolete ("Use ImportReference", error: false)]
		public TypeReference Import (Type type)
		{
			return ImportReference (type, null);
		}

		public TypeReference ImportReference (Type type)
		{
			return ImportReference (type, null);
		}

		[Obsolete ("Use ImportReference", error: false)]
		public TypeReference Import (Type type, IGenericParameterProvider context)
		{
			return ImportReference (type, context);
		}

		public TypeReference ImportReference (Type type, IGenericParameterProvider context)
		{
			Mixin.CheckType (type);
			CheckContext (context, this);

			return ReflectionImporter.ImportReference (type, context);
		}

		[Obsolete ("Use ImportReference", error: false)]
		public FieldReference Import (SR.FieldInfo field)
		{
			return ImportReference (field, null);
		}

		[Obsolete ("Use ImportReference", error: false)]
		public FieldReference Import (SR.FieldInfo field, IGenericParameterProvider context)
		{
			return ImportReference (field, context);
		}

		public FieldReference ImportReference (SR.FieldInfo field)
		{
			return ImportReference (field, null);
		}

		public FieldReference ImportReference (SR.FieldInfo field, IGenericParameterProvider context)
		{
			Mixin.CheckField (field);
			CheckContext (context, this);

			return ReflectionImporter.ImportReference (field, context);
		}

		[Obsolete ("Use ImportReference", error: false)]
		public MethodReference Import (SR.MethodBase method)
		{
			return ImportReference (method, null);
		}

		[Obsolete ("Use ImportReference", error: false)]
		public MethodReference Import (SR.MethodBase method, IGenericParameterProvider context)
		{
			return ImportReference (method, context);
		}

		public MethodReference ImportReference (SR.MethodBase method)
		{
			return ImportReference (method, null);
		}

		public MethodReference ImportReference (SR.MethodBase method, IGenericParameterProvider context)
		{
			Mixin.CheckMethod (method);
			CheckContext (context, this);

			return ReflectionImporter.ImportReference (method, context);
		}
#endif

		[Obsolete ("Use ImportReference", error: false)]
		public TypeReference Import (TypeReference type)
		{
			return ImportReference (type, null);
		}

		[Obsolete ("Use ImportReference", error: false)]
		public TypeReference Import (TypeReference type, IGenericParameterProvider context)
		{
			return ImportReference (type, context);
		}

		public TypeReference ImportReference (TypeReference type)
		{
			return ImportReference (type, null);
		}

		public TypeReference ImportReference (TypeReference type, IGenericParameterProvider context)
		{
			Mixin.CheckType (type);

			if (type.Module == this)
				return type;

			CheckContext (context, this);

			return MetadataImporter.ImportReference (type, context);
		}

		[Obsolete ("Use ImportReference", error: false)]
		public FieldReference Import (FieldReference field)
		{
			return ImportReference (field, null);
		}

		[Obsolete ("Use ImportReference", error: false)]
		public FieldReference Import (FieldReference field, IGenericParameterProvider context)
		{
			return ImportReference (field, context);
		}

		public FieldReference ImportReference (FieldReference field)
		{
			return ImportReference (field, null);
		}

		public FieldReference ImportReference (FieldReference field, IGenericParameterProvider context)
		{
			Mixin.CheckField (field);

			if (field.Module == this)
				return field;

			CheckContext (context, this);

			return MetadataImporter.ImportReference (field, context);
		}

		[Obsolete ("Use ImportReference", error: false)]
		public MethodReference Import (MethodReference method)
		{
			return ImportReference (method, null);
		}

		[Obsolete ("Use ImportReference", error: false)]
		public MethodReference Import (MethodReference method, IGenericParameterProvider context)
		{
			return ImportReference (method, context);
		}

		public MethodReference ImportReference (MethodReference method)
		{
			return ImportReference (method, null);
		}

		public MethodReference ImportReference (MethodReference method, IGenericParameterProvider context)
		{
			Mixin.CheckMethod (method);

			if (method.Module == this)
				return method;

			CheckContext (context, this);

			return MetadataImporter.ImportReference (method, context);
		}

#endif

		public IMetadataTokenProvider LookupToken (int token)
		{
			return LookupToken (new MetadataToken ((uint) token));
		}

		public IMetadataTokenProvider LookupToken (MetadataToken token)
		{
			return Read (token, (t, reader) => reader.LookupToken (t));
		}

		readonly object module_lock = new object();

		internal object SyncRoot {
			get { return module_lock; }
		}

		internal TRet Read<TItem, TRet> (TItem item, Func<TItem, MetadataReader, TRet> read)
		{
			lock (module_lock) {
				var position = reader.position;
				var context = reader.context;

				var ret = read (item, reader);

				reader.position = position;
				reader.context = context;

				return ret;
			}
		}

		internal TRet Read<TItem, TRet> (ref TRet variable, TItem item, Func<TItem, MetadataReader, TRet> read) where TRet : class
		{
			lock (module_lock) {
				if (variable != null)
					return variable;

				var position = reader.position;
				var context = reader.context;

				var ret = read (item, reader);

				reader.position = position;
				reader.context = context;

				return variable = ret;
			}
		}

		public bool HasDebugHeader {
			get { return Image != null && !Image.Debug.IsZero; }
		}

		public ImageDebugDirectory GetDebugHeader (out byte [] header)
		{
			if (!HasDebugHeader)
				throw new InvalidOperationException ();

			return Image.GetDebugHeader (out header);
		}

		void ProcessDebugHeader ()
		{
			if (!HasDebugHeader)
				return;

			byte [] header;
			var directory = GetDebugHeader (out header);

			if (!symbol_reader.ProcessDebugHeader (directory, header))
				throw new InvalidOperationException ();
		}

#if !READ_ONLY

		public static ModuleDefinition CreateModule (string name, ModuleKind kind)
		{
			return CreateModule (name, new ModuleParameters { Kind = kind });
		}

		public static ModuleDefinition CreateModule (string name, ModuleParameters parameters)
		{
			Mixin.CheckName (name);
			Mixin.CheckParameters (parameters);

			var module = new ModuleDefinition {
				Name = name,
				kind = parameters.Kind,
				Runtime = parameters.Runtime,
				architecture = parameters.Architecture,
				mvid = Guid.NewGuid (),
				Attributes = ModuleAttributes.ILOnly,
				Characteristics = (ModuleCharacteristics) 0x8540,
			};

			if (parameters.AssemblyResolver != null)
				module.assembly_resolver = parameters.AssemblyResolver;

			if (parameters.MetadataResolver != null)
				module.metadata_resolver = parameters.MetadataResolver;

#if !READ_ONLY
			if (parameters.MetadataImporterProvider != null)
				module.metadata_importer = parameters.MetadataImporterProvider.GetMetadataImporter (module);
#if !PCL
			if (parameters.ReflectionImporterProvider != null)
				module.reflection_importer = parameters.ReflectionImporterProvider.GetReflectionImporter (module);
#endif
#endif

			if (parameters.Kind != ModuleKind.NetModule) {
				var assembly = new AssemblyDefinition ();
				module.assembly = assembly;
				module.assembly.Name = CreateAssemblyName (name);
				assembly.main_module = module;
			}

			module.Types.Add (new TypeDefinition (string.Empty, "<Module>", TypeAttributes.NotPublic));

			return module;
		}

		static AssemblyNameDefinition CreateAssemblyName (string name)
		{
			if (name.EndsWith (".dll") || name.EndsWith (".exe"))
				name = name.Substring (0, name.Length - 4);

			return new AssemblyNameDefinition (name, Mixin.ZeroVersion);
		}

#endif

#if !PCL
		public void ReadSymbols ()
		{
			if (string.IsNullOrEmpty (fq_name))
				throw new InvalidOperationException ();

			var provider = SymbolProvider.GetPlatformReaderProvider ();
			if (provider == null)
				throw new InvalidOperationException ();

			ReadSymbols (provider.GetSymbolReader (this, fq_name));
		}
#endif

		public void ReadSymbols (ISymbolReader reader)
		{
			if (reader == null)
				throw new ArgumentNullException ("reader");

			symbol_reader = reader;

			ProcessDebugHeader ();
		}

#if !PCL
		public static ModuleDefinition ReadModule (string fileName)
		{
			return ReadModule (fileName, new ReaderParameters (ReadingMode.Deferred));
		}

		public static ModuleDefinition ReadModule (string fileName, ReaderParameters parameters)
		{
			using (var stream = GetFileStream (fileName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
				return ReadModule (stream, parameters);
			}
		}

		static Stream GetFileStream (string fileName, FileMode mode, FileAccess access, FileShare share)
		{
			if (fileName == null)
				throw new ArgumentNullException ("fileName");
			if (fileName.Length == 0)
				throw new ArgumentException ();

			return new FileStream (fileName, mode, access, share);
		}
#endif

		public static ModuleDefinition ReadModule (Stream stream)
		{
			return ReadModule (stream, new ReaderParameters (ReadingMode.Deferred));
		}

		static void CheckStream (object stream)
		{
			if (stream == null)
				throw new ArgumentNullException ("stream");
		}

		public static ModuleDefinition ReadModule (Stream stream, ReaderParameters parameters)
		{
			Mixin.CheckStream (stream);
			if (!stream.CanRead || !stream.CanSeek)
				throw new ArgumentException ();
			Mixin.CheckParameters (parameters);

			return ModuleReader.CreateModuleFrom (
				ImageReader.ReadImageFrom (stream),
				parameters);
		}

#if !READ_ONLY

#if !PCL
		public void Write (string fileName)
		{
			Write (fileName, new WriterParameters ());
		}

		public void Write (string fileName, WriterParameters parameters)
		{
			using (var stream = GetFileStream (fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.None)) {
				Write (stream, parameters);
			}
		}
#endif

		public void Write (Stream stream)
		{
			Write (stream, new WriterParameters ());
		}

		public void Write (Stream stream, WriterParameters parameters)
		{
			Mixin.CheckStream (stream);
			if (!stream.CanWrite || !stream.CanSeek)
				throw new ArgumentException ();
			Mixin.CheckParameters (parameters);

			ModuleWriter.WriteModuleTo (this, stream, parameters);
		}

#endif

	}

	static partial class Mixin {

		public static void CheckStream (object stream)
		{
			if (stream == null)
				throw new ArgumentNullException ("stream");
		}

#if !READ_ONLY

		public static void CheckType (object type)
		{
			if (type == null)
				throw new ArgumentNullException ("type");
		}

		public static void CheckField (object field)
		{
			if (field == null)
				throw new ArgumentNullException ("field");
		}

		public static void CheckMethod (object method)
		{
			if (method == null)
				throw new ArgumentNullException ("method");
		}

#endif

		public static void CheckParameters (object parameters)
		{
			if (parameters == null)
				throw new ArgumentNullException ("parameters");
		}

		public static bool HasImage (this ModuleDefinition self)
		{
			return self != null && self.HasImage;
		}

		public static bool IsCoreLibrary (this ModuleDefinition module)
		{
			if (module.Assembly == null)
				return false;

			var assembly_name = module.Assembly.Name.Name;

			if (assembly_name != "mscorlib" && assembly_name != "System.Runtime")
				return false;

			if (module.HasImage && !module.Read (module, (m, reader) => reader.image.GetTableLength (Table.TypeDef) > 1))
				return false;

			return true;
		}

		public static string GetFullyQualifiedName (this Stream self)
		{
#if !PCL
			var file_stream = self as FileStream;
			if (file_stream == null)
				return string.Empty;

			return Path.GetFullPath (file_stream.Name);
#else
			return string.Empty;
#endif
		}

		public static TargetRuntime ParseRuntime (this string self)
		{
			switch (self [1]) {
			case '1':
				return self [3] == '0'
					? TargetRuntime.Net_1_0
					: TargetRuntime.Net_1_1;
			case '2':
				return TargetRuntime.Net_2_0;
			case '4':
			default:
				return TargetRuntime.Net_4_0;
			}
		}

		public static string RuntimeVersionString (this TargetRuntime runtime)
		{
			switch (runtime) {
			case TargetRuntime.Net_1_0:
				return "v1.0.3705";
			case TargetRuntime.Net_1_1:
				return "v1.1.4322";
			case TargetRuntime.Net_2_0:
				return "v2.0.50727";
			case TargetRuntime.Net_4_0:
			default:
				return "v4.0.30319";
			}
		}
	}
}
