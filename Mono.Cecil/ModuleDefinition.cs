//
// ModuleDefinition.cs
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
		IAssemblyResolver assembly_resolver;
		IMetadataResolver metadata_resolver;
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

		public ModuleParameters ()
		{
			this.kind = ModuleKind.Dll;
			this.Runtime = GetCurrentRuntime ();
			this.architecture = TargetArchitecture.I386;
		}

		static TargetRuntime GetCurrentRuntime ()
		{
#if !CF
			return typeof (object).Assembly.ImageRuntimeVersion.ParseRuntime ();
#else
			var corlib_version = typeof (object).Assembly.GetName ().Version;
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

    public interface IWriterParameters {
        Stream SymbolStream { get; set; }
        ISymbolWriterProvider SymbolWriterProvider { get; set; }
        bool WriteSymbols { get; set; }
        SR.StrongNameKeyPair StrongNameKeyPair { get; set; }
    }

    public sealed class WriterParameters : IWriterParameters {

		Stream symbol_stream;
		ISymbolWriterProvider symbol_writer_provider;
		bool write_symbols;
#if !SILVERLIGHT && !CF
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
#if !SILVERLIGHT && !CF
		public SR.StrongNameKeyPair StrongNameKeyPair {
			get { return key_pair; }
			set { key_pair = value; }
		}
#endif
	}

#endif

    public interface IModuleDefinition : IModuleReference, ICustomAttributeProvider {
        bool IsMain { get; }
        ModuleKind Kind { get; set; }
        TargetRuntime Runtime { get; set; }
        string RuntimeVersion { get; set; }
        TargetArchitecture Architecture { get; set; }
        ModuleAttributes Attributes { get; set; }
        ModuleCharacteristics Characteristics { get; set; }
        string FullyQualifiedName { get; }
        Guid Mvid { get; set; }
        bool HasSymbols { get; }
        ISymbolReader SymbolReader { get; }
        IAssemblyDefinition Assembly { get; set; }
        IAssemblyResolver AssemblyResolver { get; set; }
        IMetadataResolver MetadataResolver { get; set; }
        ITypeSystem TypeSystem { get; }
        bool HasAssemblyReferences { get; }
        IList<IAssemblyNameReference> AssemblyReferences { get; }
        bool HasModuleReferences { get; }
        IList<IModuleReference> ModuleReferences { get; }
        bool HasResources { get; }
        IList<IResource> Resources { get; }
        bool HasTypes { get; }
        IList<ITypeDefinition> Types { get; }
        bool HasExportedTypes { get; }
        IList<ExportedType> ExportedTypes { get; }
        IMethodDefinition EntryPoint { get; set; }
        bool HasDebugHeader { get; }
        bool HasTypeReference (string fullName);
        bool HasTypeReference (string scope, string fullName);
        bool TryGetTypeReference (string fullName, out ITypeReference type);
        bool TryGetTypeReference (string scope, string fullName, out ITypeReference type);
        IEnumerable<ITypeReference> GetTypeReferences ();
        IEnumerable<IMemberReference> GetMemberReferences ();
        ITypeReference GetType (string fullName, bool runtimeName);
        ITypeDefinition GetType (string fullName);
        ITypeDefinition GetType (string @namespace, string name);
        IEnumerable<ITypeDefinition> GetTypes ();
        ITypeReference Import (Type type);
        ITypeReference Import (Type type, IGenericParameterProvider context);
        IFieldReference Import(SR.FieldInfo field);
        IFieldReference Import(SR.FieldInfo field, IGenericParameterProvider context);
        IMethodReference Import (SR.MethodBase method);
        IMethodReference Import (SR.MethodBase method, IGenericParameterProvider context);
        ITypeReference Import (ITypeReference type);
        ITypeReference Import (ITypeReference type, IGenericParameterProvider context);
        IFieldReference Import(IFieldReference field);
        IFieldReference Import(IFieldReference field, IGenericParameterProvider context);
        IMethodReference Import (IMethodReference method);
        IMethodReference Import (IMethodReference method, IGenericParameterProvider context);
        IMetadataTokenProvider LookupToken (int token);
        IMetadataTokenProvider LookupToken (MetadataToken token);
        ImageDebugDirectory GetDebugHeader (out byte [] header);
        void ReadSymbols ();
        void ReadSymbols (ISymbolReader reader);
        void Write (string fileName);
        void Write (Stream stream);
        void Write(string fileName, IWriterParameters parameters);
        void Write(Stream stream, IWriterParameters parameters);
        IFieldDefinition Resolve(IFieldReference field);
        IMethodDefinition Resolve (IMethodReference method);
        ITypeDefinition Resolve (ITypeReference type);
        bool HasImage { get; }
        object SyncRoot { get; }
        IMetadataReader MetadataReader { get; }
        IMetadataSystem MetadataSystem { get; }
        ReadingMode ReadingMode { get;}
        ISymbolReaderProvider SymbolReaderProvider { get; set; }
        Section GetSection (string rsrc);
        Image Image { get;}
    }

    public sealed class ModuleDefinition : ModuleReference, IModuleDefinition {


		internal ISymbolReader symbol_reader;
		internal IAssemblyResolver assembly_resolver;
		internal IMetadataResolver metadata_resolver;
		internal ITypeSystem type_system;

        readonly IMetadataReader reader;
		readonly string fq_name;

		internal string runtime_version;
		internal ModuleKind kind;
		TargetRuntime runtime;
		TargetArchitecture architecture;
		ModuleAttributes attributes;
		ModuleCharacteristics characteristics;
		Guid mvid;

		internal IAssemblyDefinition assembly;
		IMethodDefinition entry_point;

#if !READ_ONLY
		MetadataImporter importer;
#endif
		IList<CustomAttribute> custom_attributes;
		IList<IAssemblyNameReference> references;
		IList<IModuleReference> modules;
        IList<IResource> resources;
		IList<ExportedType> exported_types;
		TypeDefinitionCollection types;

        public IMetadataSystem MetadataSystem { get; private set; }

        public ReadingMode ReadingMode { get; internal set; }

        public ISymbolReaderProvider SymbolReaderProvider { get; set; }

        public Image Image { get; private set; }


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

        public bool HasImage {
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

		public IAssemblyDefinition Assembly
		{
		    get { return assembly; }
		    set { assembly = value; }
		}

#if !READ_ONLY
		internal MetadataImporter MetadataImporter {
			get {
				if (importer == null)
					Interlocked.CompareExchange (ref importer, new MetadataImporter (this), null);

				return importer;
			}
		}
#endif

		public IAssemblyResolver AssemblyResolver
		{
		    get {
				if (assembly_resolver == null)
					Interlocked.CompareExchange (ref assembly_resolver, new DefaultAssemblyResolver (), null);

				return assembly_resolver;
			}
		    set { assembly_resolver = value; }
		}

        public IMetadataResolver MetadataResolver
        {
            get {
				if (metadata_resolver == null)
					Interlocked.CompareExchange (ref metadata_resolver, new MetadataResolver (this.AssemblyResolver), null);

				return metadata_resolver;
			}
            set { metadata_resolver = value; }
        }

        public ITypeSystem TypeSystem
        {
			get {
				if (type_system == null)
                    Interlocked.CompareExchange(ref type_system, Cecil.TypeSystem.CreateTypeSystem(this), null);

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

		public IList<IAssemblyNameReference> AssemblyReferences {
			get {
				if (references != null)
					return references;

				if (HasImage)
					return this.Read (ref references, this, (_, reader) => reader.ReadAssemblyReferences ());

			    return references = new Collection<IAssemblyNameReference> ();
			}
		}

		public bool HasModuleReferences {
			get {
				if (modules != null)
					return modules.Count > 0;

				return HasImage && Image.HasTable (Table.ModuleRef);
			}
		}

		public IList<IModuleReference> ModuleReferences {
			get {
				if (modules != null)
					return modules;

				if (HasImage)
                    return this.Read(ref modules, this, (_, reader) => reader.ReadModuleReferences());

                return modules = new Collection<IModuleReference>();
			}
		}

		public bool HasResources {
			get {
				if (resources != null)
					return resources.Count > 0;

				if (HasImage)
                    return Image.HasTable(Table.ManifestResource) || this.Read(this, (_, reader) => reader.HasFileResource());

				return false;
			}
		}

        public IList<IResource> Resources
        {
			get {
				if (resources != null)
					return resources;

				if (HasImage)
                    return this.Read(ref resources, this, (_, reader) => reader.ReadResources());

                return resources = new Collection<IResource>();
			}
		}

		public bool HasCustomAttributes {
			get {
				if (custom_attributes != null)
					return custom_attributes.Count > 0;

				return this.GetHasCustomAttributes (this);
			}
		}

		public IList<CustomAttribute> CustomAttributes {
			get { return custom_attributes ?? (this.GetCustomAttributes (ref custom_attributes, this)); }
		}

		public bool HasTypes {
			get {
				if (types != null)
					return types.Count > 0;

				return HasImage && Image.HasTable (Table.TypeDef);
			}
		}

		public IList<ITypeDefinition> Types {
			get {
				if (types != null)
					return types;

				if (HasImage)
                    return this.Read(ref types, this, (_, reader) => reader.ReadTypes());

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

		public IList<ExportedType> ExportedTypes {
			get {
				if (exported_types != null)
					return exported_types;

				if (HasImage)
                    return this.Read(ref exported_types, this, (_, reader) => reader.ReadExportedTypes());

                return exported_types = new Collection<ExportedType>();
			}
		}

		public IMethodDefinition EntryPoint {
			get {
				if (entry_point != null)
					return entry_point;

				if (HasImage)
                    return this.Read(ref entry_point, this, (_, reader) => reader.ReadEntryPoint());

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

		public bool TryGetTypeReference (string fullName, out ITypeReference type)
		{
			return TryGetTypeReference (string.Empty, fullName, out type);
		}

		public bool TryGetTypeReference (string scope, string fullName, out ITypeReference type)
		{
			CheckFullName (fullName);

			if (!HasImage) {
				type = null;
				return false;
			}

			return (type = GetTypeReference (scope, fullName)) != null;
		}

		ITypeReference GetTypeReference (string scope, string fullname)
		{
            return this.Read(new Row<string, string>(scope, fullname), (row, reader) => reader.GetTypeReference(row.Col1, row.Col2));
		}

		public IEnumerable<ITypeReference> GetTypeReferences ()
		{
			if (!HasImage)
				return Empty<ITypeReference>.Array;

            return this.Read(this, (_, reader) => reader.GetTypeReferences());
		}

		public IEnumerable<IMemberReference> GetMemberReferences ()
		{
			if (!HasImage)
				return Empty<IMemberReference>.Array;

            return this.Read(this, (_, reader) => reader.GetMemberReferences());
		}

		public ITypeReference GetType (string fullName, bool runtimeName)
		{
			return runtimeName
				? TypeParser.ParseType (this, fullName)
				: GetType (fullName);
		}

		public ITypeDefinition GetType (string fullName)
		{
			CheckFullName (fullName);

			var position = fullName.IndexOf ('/');
			if (position > 0)
				return GetNestedType (fullName);

			return ((TypeDefinitionCollection) this.Types).GetType (fullName);
		}

		public ITypeDefinition GetType (string @namespace, string name)
		{
			Mixin.CheckName (name);

			return ((TypeDefinitionCollection) this.Types).GetType (@namespace ?? string.Empty, name);
		}

		public IEnumerable<ITypeDefinition> GetTypes ()
		{
			return GetTypes (Types);
		}

		static IEnumerable<ITypeDefinition> GetTypes (IList<ITypeDefinition> types)
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

		ITypeDefinition GetNestedType (string fullname)
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

        public IFieldDefinition Resolve(IFieldReference field)
		{
			return MetadataResolver.Resolve (field);
		}

        public IMethodDefinition Resolve (IMethodReference method)
		{
			return MetadataResolver.Resolve (method);
		}

        public ITypeDefinition Resolve (ITypeReference type)
		{
			return MetadataResolver.Resolve (type);
		}

#if !READ_ONLY

		static void CheckType (object type)
		{
			if (type == null)
				throw new ArgumentNullException ("type");
		}

		static void CheckField (object field)
		{
			if (field == null)
				throw new ArgumentNullException ("field");
		}

		static void CheckMethod (object method)
		{
			if (method == null)
				throw new ArgumentNullException ("method");
		}

		static void CheckContext (IGenericParameterProvider context, IModuleDefinition module)
		{
			if (context == null)
				return;

			if (context.Module != module)
				throw new ArgumentException ();
		}

		static ImportGenericContext GenericContextFor (IGenericParameterProvider context)
		{
			return context != null ? new ImportGenericContext (context) : default (ImportGenericContext);
		}

#if !CF

		public ITypeReference Import (Type type)
		{
			return Import (type, null);
		}

		public ITypeReference Import (Type type, IGenericParameterProvider context)
		{
			CheckType (type);
			CheckContext (context, this);

			return MetadataImporter.ImportType (
				type,
				GenericContextFor (context),
				context != null ? ImportGenericKind.Open : ImportGenericKind.Definition);
		}

        public IFieldReference Import(SR.FieldInfo field)
		{
			return Import (field, null);
		}

        public IFieldReference Import(SR.FieldInfo field, IGenericParameterProvider context)
		{
			CheckField (field);
			CheckContext (context, this);

			return MetadataImporter.ImportField (field, GenericContextFor (context));
		}

		public IMethodReference Import (SR.MethodBase method)
		{
			CheckMethod (method);

			return MetadataImporter.ImportMethod (method, default (ImportGenericContext), ImportGenericKind.Definition);
		}

		public IMethodReference Import (SR.MethodBase method, IGenericParameterProvider context)
		{
			CheckMethod (method);
			CheckContext (context, this);

			return MetadataImporter.ImportMethod (method,
				GenericContextFor (context),
				context != null ? ImportGenericKind.Open : ImportGenericKind.Definition);
		}
#endif

		public ITypeReference Import (ITypeReference type)
		{
			CheckType (type);

			if (type.Module == this)
				return type;

			return MetadataImporter.ImportType (type, default (ImportGenericContext));
		}

		public ITypeReference Import (ITypeReference type, IGenericParameterProvider context)
		{
			CheckType (type);

			if (type.Module == this)
				return type;

			CheckContext (context, this);

			return MetadataImporter.ImportType (type, GenericContextFor (context));
		}

        public IFieldReference Import(IFieldReference field)
		{
			CheckField (field);

			if (field.Module == this)
				return field;

			return MetadataImporter.ImportField (field, default (ImportGenericContext));
		}

        public IFieldReference Import(IFieldReference field, IGenericParameterProvider context)
		{
			CheckField (field);

			if (field.Module == this)
				return field;

			CheckContext (context, this);

			return MetadataImporter.ImportField (field, GenericContextFor (context));
		}

		public IMethodReference Import (IMethodReference method)
		{
			return Import (method, null);
		}

		public IMethodReference Import (IMethodReference method, IGenericParameterProvider context)
		{
			CheckMethod (method);

			if (method.Module == this)
				return method;

			CheckContext (context, this);

			return MetadataImporter.ImportMethod (method, GenericContextFor (context));
		}

#endif



		public IMetadataTokenProvider LookupToken (int token)
		{
			return LookupToken (new MetadataToken ((uint) token));
		}

		public IMetadataTokenProvider LookupToken (MetadataToken token)
		{
            return this.Read(token, (t, reader) => reader.LookupToken(t));
		}

		readonly object module_lock = new object();

        public object SyncRoot {
			get { return module_lock; }
		}

        

		public bool HasDebugHeader {
			get { return Image != null && !Image.Debug.IsZero; }
		}

        public IMetadataReader MetadataReader
	    {
	        get { return reader; }
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

        public Section GetSection (string name)
        {
            if (!HasImage)
                return null;

            return Image.GetSection(name);
        }

		public static IModuleDefinition CreateModule (string name, ModuleKind kind)
		{
			return CreateModule (name, new ModuleParameters { Kind = kind });
		}

		public static IModuleDefinition CreateModule (string name, ModuleParameters parameters)
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

			if (parameters.Kind != ModuleKind.NetModule) {
				var assembly = new AssemblyDefinition ();
				module.assembly = assembly;
				module.assembly.Name = CreateAssemblyName (name);
				assembly.main_module = module;
			}

			module.Types.Add (new TypeDefinition (string.Empty, "<Module>", TypeAttributes.NotPublic));

			return module;
		}

		static IAssemblyNameDefinition CreateAssemblyName (string name)
		{
			if (name.EndsWith (".dll") || name.EndsWith (".exe"))
				name = name.Substring (0, name.Length - 4);

			return new AssemblyNameDefinition (name, new Version (0, 0, 0, 0));
		}

#endif

		public void ReadSymbols ()
		{
			if (string.IsNullOrEmpty (fq_name))
				throw new InvalidOperationException ();

			var provider = SymbolProvider.GetPlatformReaderProvider ();
			if (provider == null)
				throw new InvalidOperationException ();

			ReadSymbols (provider.GetSymbolReader (this, fq_name));
		}

		public void ReadSymbols (ISymbolReader reader)
		{
			if (reader == null)
				throw new ArgumentNullException ("reader");

			symbol_reader = reader;

			ProcessDebugHeader ();
		}

		public static IModuleDefinition ReadModule (string fileName)
		{
			return ReadModule (fileName, new ReaderParameters (ReadingMode.Deferred));
		}

		public static IModuleDefinition ReadModule (Stream stream)
		{
			return ReadModule (stream, new ReaderParameters (ReadingMode.Deferred));
		}

		public static IModuleDefinition ReadModule (string fileName, ReaderParameters parameters)
		{
			using (var stream = GetFileStream (fileName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
				return ReadModule (stream, parameters);
			}
		}

		static void CheckStream (object stream)
		{
			if (stream == null)
				throw new ArgumentNullException ("stream");
		}

		public static IModuleDefinition ReadModule (Stream stream, ReaderParameters parameters)
		{
			CheckStream (stream);
			if (!stream.CanRead || !stream.CanSeek)
				throw new ArgumentException ();
			Mixin.CheckParameters (parameters);

			return ModuleReader.CreateModuleFrom (
				ImageReader.ReadImageFrom (stream),
				parameters);
		}

		static Stream GetFileStream (string fileName, FileMode mode, FileAccess access, FileShare share)
		{
			if (fileName == null)
				throw new ArgumentNullException ("fileName");
			if (fileName.Length == 0)
				throw new ArgumentException ();

			return new FileStream (fileName, mode, access, share);
		}

#if !READ_ONLY

		public void Write (string fileName)
		{
			Write (fileName, new WriterParameters ());
		}

		public void Write (Stream stream)
		{
			Write (stream, new WriterParameters ());
		}

        public void Write(string fileName, IWriterParameters parameters)
		{
			using (var stream = GetFileStream (fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.None)) {
				Write (stream, parameters);
			}
		}

        public void Write(Stream stream, IWriterParameters parameters)
		{
			CheckStream (stream);
			if (!stream.CanWrite || !stream.CanSeek)
				throw new ArgumentException ();
			Mixin.CheckParameters (parameters);

			ModuleWriter.WriteModuleTo (this, stream, parameters);
		}

#endif

	}

	static partial class Mixin {

		public static void CheckParameters (object parameters)
		{
			if (parameters == null)
				throw new ArgumentNullException ("parameters");
		}

		public static bool HasImage (this IModuleDefinition self)
		{
			return self != null && self.HasImage;
		}

		public static bool IsCorlib (this IModuleDefinition module)
		{
			if (module.Assembly == null)
				return false;

			return module.Assembly.Name.Name == "mscorlib";
		}

		public static string GetFullyQualifiedName (this Stream self)
		{
#if !SILVERLIGHT
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

        internal static TRet Read<TItem, TRet>(this IModuleDefinition moduleDefinition, TItem item, Func<TItem, IMetadataReader, TRet> read)
        {
            lock (moduleDefinition.SyncRoot)
            {
                var reader = moduleDefinition.MetadataReader;

                var position = reader.Position;
                var context = reader.Context;

                var ret = read(item, reader);

                reader.Position = position;
                reader.Context = context;

                return ret;
            }
        }

        internal static TRet Read<TItem, TRet>(this IModuleDefinition moduleDefinition, ref TRet variable, TItem item, Func<TItem, IMetadataReader, TRet> read) where TRet : class
        {
            lock (moduleDefinition.SyncRoot)
            {

                if (variable != null)
                    return variable;

                var reader = moduleDefinition.MetadataReader;

                var position = reader.Position;
                var context = reader.Context;

                var ret = read(item, reader);

                reader.Position = position;
                reader.Context = context;

                return variable = ret;
            }
        }
	}
}
