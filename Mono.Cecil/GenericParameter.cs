//
// GenericParameter.cs
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
using Mono.Collections.Generic;

using Mono.Cecil.Metadata;

namespace Mono.Cecil {
    public interface IGenericParameter : ITypeReference, ICustomAttributeProvider {
        GenericParameterAttributes Attributes { get; set; }
        int Position { get; set; }
        GenericParameterType Type { get; set; }
        IGenericParameterProvider Owner { get; set; }
        bool HasConstraints { get; }
        IList<ITypeReference> Constraints { get; }
        IMethodReference DeclaringMethod { get; }
        bool IsNonVariant { get; set; }
        bool IsCovariant { get; set; }
        bool IsContravariant { get; set; }
        bool HasReferenceTypeConstraint { get; set; }
        bool HasNotNullableValueTypeConstraint { get; set; }
        bool HasDefaultConstructorConstraint { get; set; }
    }

    public sealed class GenericParameter : TypeReference, IGenericParameter {

		internal int position;
		internal GenericParameterType type;
		internal IGenericParameterProvider owner;

		ushort attributes;
		IList<ITypeReference> constraints;
		IList<CustomAttribute> custom_attributes;

		public GenericParameterAttributes Attributes {
			get { return (GenericParameterAttributes) attributes; }
			set { attributes = (ushort) value; }
		}

		public int Position
		{
		    get { return position; }
		    set { position = value; }
		}

        public GenericParameterType Type
        {
            get { return type; }
            set { type = value; }
        }

        public IGenericParameterProvider Owner
        {
            get { return owner; }
            set { owner = value; }
        }

        public bool HasConstraints {
			get {
				if (constraints != null)
					return constraints.Count > 0;

				if (HasImage)
					return Module.Read (this, (generic_parameter, reader) => reader.HasGenericConstraints (generic_parameter));

				return false;
			}
		}

		public IList<ITypeReference> Constraints {
			get {
				if (constraints != null)
					return constraints;

				if (HasImage)
					return Module.Read (ref constraints, this, (generic_parameter, reader) => reader.ReadGenericConstraints (generic_parameter));

				return constraints = new Collection<ITypeReference> ();
			}
		}

		public bool HasCustomAttributes {
			get {
				if (custom_attributes != null)
					return custom_attributes.Count > 0;

				return this.GetHasCustomAttributes (Module);
			}
		}

		public IList<CustomAttribute> CustomAttributes {
			get { return custom_attributes ?? (this.GetCustomAttributes (ref custom_attributes, Module)); }
		}

		public override IMetadataScope Scope {
			get {
				if (owner == null)
					return null;

				return owner.GenericParameterType == GenericParameterType.Method
					? ((IMethodReference) owner).DeclaringType.Scope
					: ((ITypeReference) owner).Scope;
			}
			set { throw new InvalidOperationException (); }
		}

		public override ITypeReference DeclaringType {
			get { return owner as ITypeReference; }
			set { throw new InvalidOperationException (); }
		}

		public IMethodReference DeclaringMethod {
			get { return owner as IMethodReference; }
		}

		public override IModuleDefinition Module {
			get { return module ?? owner.Module; }
		}

		public override string Name {
			get {
				if (!string.IsNullOrEmpty (base.Name))
					return base.Name;

				return base.Name = (type == GenericParameterType.Method ? "!!" : "!") + position;
			}
		}

		public override string Namespace {
			get { return string.Empty; }
			set { throw new InvalidOperationException (); }
		}

		public override string FullName {
			get { return Name; }
		}

		public override bool IsGenericParameter {
			get { return true; }
		}

		public override bool ContainsGenericParameter {
			get { return true; }
		}

		public override MetadataType MetadataType {
			get { return (MetadataType) etype; }
		}

		#region GenericParameterAttributes

		public bool IsNonVariant {
			get { return attributes.GetMaskedAttributes ((ushort) GenericParameterAttributes.VarianceMask, (ushort) GenericParameterAttributes.NonVariant); }
			set { attributes = attributes.SetMaskedAttributes ((ushort) GenericParameterAttributes.VarianceMask, (ushort) GenericParameterAttributes.NonVariant, value); }
		}

		public bool IsCovariant {
			get { return attributes.GetMaskedAttributes ((ushort) GenericParameterAttributes.VarianceMask, (ushort) GenericParameterAttributes.Covariant); }
			set { attributes = attributes.SetMaskedAttributes ((ushort) GenericParameterAttributes.VarianceMask, (ushort) GenericParameterAttributes.Covariant, value); }
		}

		public bool IsContravariant {
			get { return attributes.GetMaskedAttributes ((ushort) GenericParameterAttributes.VarianceMask, (ushort) GenericParameterAttributes.Contravariant); }
			set { attributes = attributes.SetMaskedAttributes ((ushort) GenericParameterAttributes.VarianceMask, (ushort) GenericParameterAttributes.Contravariant, value); }
		}

		public bool HasReferenceTypeConstraint {
			get { return attributes.GetAttributes ((ushort) GenericParameterAttributes.ReferenceTypeConstraint); }
			set { attributes = attributes.SetAttributes ((ushort) GenericParameterAttributes.ReferenceTypeConstraint, value); }
		}

		public bool HasNotNullableValueTypeConstraint {
			get { return attributes.GetAttributes ((ushort) GenericParameterAttributes.NotNullableValueTypeConstraint); }
			set { attributes = attributes.SetAttributes ((ushort) GenericParameterAttributes.NotNullableValueTypeConstraint, value); }
		}

		public bool HasDefaultConstructorConstraint {
			get { return attributes.GetAttributes ((ushort) GenericParameterAttributes.DefaultConstructorConstraint); }
			set { attributes = attributes.SetAttributes ((ushort) GenericParameterAttributes.DefaultConstructorConstraint, value); }
		}

		#endregion

		public GenericParameter (IGenericParameterProvider owner)
			: this (string.Empty, owner)
		{
		}

		public GenericParameter (string name, IGenericParameterProvider owner)
			: base (string.Empty, name)
		{
			if (owner == null)
				throw new ArgumentNullException ();

			this.position = -1;
			this.owner = owner;
			this.type = owner.GenericParameterType;
			this.etype = ConvertGenericParameterType (this.type);
			this.token = new MetadataToken (TokenType.GenericParam);

		}

		public GenericParameter (int position, GenericParameterType type, IModuleDefinition module)
			: base (string.Empty, string.Empty)
		{
			if (module == null)
				throw new ArgumentNullException ();

			this.position = position;
			this.type = type;
			this.etype = ConvertGenericParameterType (type);
			this.module = module;
			this.token = new MetadataToken (TokenType.GenericParam);
		}

		static ElementType ConvertGenericParameterType (GenericParameterType type)
		{
			switch (type) {
			case GenericParameterType.Type:
				return ElementType.Var;
			case GenericParameterType.Method:
				return ElementType.MVar;
			}

			throw new ArgumentOutOfRangeException ();
		}

		public override ITypeDefinition Resolve ()
		{
			return null;
		}
	}

    sealed class GenericParameterCollection : Collection<IGenericParameter>
    {

		readonly IGenericParameterProvider owner;

		internal GenericParameterCollection (IGenericParameterProvider owner)
		{
			this.owner = owner;
		}

		internal GenericParameterCollection (IGenericParameterProvider owner, int capacity)
			: base (capacity)
		{
			this.owner = owner;
		}

        protected override void OnAdd(IGenericParameter item, int index)
		{
			UpdateGenericParameter (item, index);
		}

        protected override void OnInsert(IGenericParameter item, int index)
		{
			UpdateGenericParameter (item, index);

			for (int i = index; i < size; i++)
				items[i].Position = i + 1;
		}

        protected override void OnSet(IGenericParameter item, int index)
		{
			UpdateGenericParameter (item, index);
		}

        void UpdateGenericParameter(IGenericParameter item, int index)
		{
			item.Owner = owner;
			item.Position = index;
			item.Type = owner.GenericParameterType;
		}

        protected override void OnRemove(IGenericParameter item, int index)
		{
			item.Owner = null;
			item.Position = -1;
			item.Type = GenericParameterType.Type;

			for (int i = index + 1; i < size; i++)
				items[i].Position = i - 1;
		}
	}
}
