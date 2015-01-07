//
// ParameterDefinition.cs
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

using System.Collections.Generic;
using Mono.Collections.Generic;

namespace Mono.Cecil {

	public sealed class ParameterDefinition : ParameterReference, ICustomAttributeProvider, IConstantProvider, IMarshalInfoProvider {

		ushort attributes;

		internal IMethodSignature method;

		object constant = Mixin.NotResolved;
		IList<CustomAttribute> custom_attributes;
		MarshalInfo marshal_info;

		public ParameterAttributes Attributes {
			get { return (ParameterAttributes) attributes; }
			set { attributes = (ushort) value; }
		}

		public IMethodSignature Method {
			get { return method; }
		}

		public int Sequence {
			get {
				if (method == null)
					return -1;

				return method.HasImplicitThis () ? index + 1 : index;
			}
		}

		public bool HasConstant {
			get {
				this.ResolveConstant (ref constant, parameter_type.Module);

				return constant != Mixin.NoValue;
			}
			set { if (!value) constant = Mixin.NoValue; }
		}

		public object Constant {
			get { return HasConstant ? constant : null;	}
			set { constant = value; }
		}

		public bool HasCustomAttributes {
			get {
				if (custom_attributes != null)
					return custom_attributes.Count > 0;

				return this.GetHasCustomAttributes (parameter_type.Module);
			}
		}

		public IList<CustomAttribute> CustomAttributes {
			get { return custom_attributes ?? (this.GetCustomAttributes (ref custom_attributes, parameter_type.Module)); }
		}

		public bool HasMarshalInfo {
			get {
				if (marshal_info != null)
					return true;

				return this.GetHasMarshalInfo (parameter_type.Module);
			}
		}

		public MarshalInfo MarshalInfo {
			get { return marshal_info ?? (this.GetMarshalInfo (ref marshal_info, parameter_type.Module)); }
			set { marshal_info = value; }
		}

		#region ParameterAttributes

		public bool IsIn {
			get { return attributes.GetAttributes ((ushort) ParameterAttributes.In); }
			set { attributes = attributes.SetAttributes ((ushort) ParameterAttributes.In, value); }
		}

		public bool IsOut {
			get { return attributes.GetAttributes ((ushort) ParameterAttributes.Out); }
			set { attributes = attributes.SetAttributes ((ushort) ParameterAttributes.Out, value); }
		}

		public bool IsLcid {
			get { return attributes.GetAttributes ((ushort) ParameterAttributes.Lcid); }
			set { attributes = attributes.SetAttributes ((ushort) ParameterAttributes.Lcid, value); }
		}

		public bool IsReturnValue {
			get { return attributes.GetAttributes ((ushort) ParameterAttributes.Retval); }
			set { attributes = attributes.SetAttributes ((ushort) ParameterAttributes.Retval, value); }
		}

		public bool IsOptional {
			get { return attributes.GetAttributes ((ushort) ParameterAttributes.Optional); }
			set { attributes = attributes.SetAttributes ((ushort) ParameterAttributes.Optional, value); }
		}

		public bool HasDefault {
			get { return attributes.GetAttributes ((ushort) ParameterAttributes.HasDefault); }
			set { attributes = attributes.SetAttributes ((ushort) ParameterAttributes.HasDefault, value); }
		}

		public bool HasFieldMarshal {
			get { return attributes.GetAttributes ((ushort) ParameterAttributes.HasFieldMarshal); }
			set { attributes = attributes.SetAttributes ((ushort) ParameterAttributes.HasFieldMarshal, value); }
		}

		#endregion

		internal ParameterDefinition (ITypeReference parameterType, IMethodSignature method)
			: this (string.Empty, ParameterAttributes.None, parameterType)
		{
			this.method = method;
		}

		public ParameterDefinition (ITypeReference parameterType)
			: this (string.Empty, ParameterAttributes.None, parameterType)
		{
		}

		public ParameterDefinition (string name, ParameterAttributes attributes, ITypeReference parameterType)
			: base (name, parameterType)
		{
			this.attributes = (ushort) attributes;
			this.token = new MetadataToken (TokenType.Param);
		}

		public override ParameterDefinition Resolve ()
		{
			return this;
		}
	}
}
