//
// CustomAttribute.cs
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

namespace Mono.Cecil {

	public struct CustomAttributeArgument {

		readonly ITypeReference type;
		readonly object value;

		public ITypeReference Type {
			get { return type; }
		}

		public object Value {
			get { return value; }
		}

		public CustomAttributeArgument (ITypeReference type, object value)
		{
			Mixin.CheckType (type);
			this.type = type;
			this.value = value;
		}
	}

	public struct CustomAttributeNamedArgument {

		readonly string name;
		readonly CustomAttributeArgument argument;

		public string Name {
			get { return name; }
		}

		public CustomAttributeArgument Argument {
			get { return argument; }
		}

		public CustomAttributeNamedArgument (string name, CustomAttributeArgument argument)
		{
			Mixin.CheckName (name);
			this.name = name;
			this.argument = argument;
		}
	}

    public interface IAttribute {
        ITypeReference AttributeType { get; }
        bool HasFields { get; }
        bool HasProperties { get; }
        IList<CustomAttributeNamedArgument> Fields { get; set; }
        IList<CustomAttributeNamedArgument> Properties { get; set; }

        IList<CustomAttributeNamedArgument> GetFields ();
        IList<CustomAttributeNamedArgument> GetProperties ();
    }

    public interface ICustomAttribute : IAttribute {
        uint Signature { get; }
        IMethodReference Constructor { get; set; }
        bool HasConstructorArguments { get; }
        IList<CustomAttributeArgument> ConstructorArguments { get; }
        IList<CustomAttributeArgument> Arguments { get; set; }
        bool IsResolved { get; set; }
        byte[] GetBlob ();
    }

    public sealed class CustomAttribute : ICustomAttribute {

        public uint Signature { get; private set; }
		IMethodReference constructor;
		byte [] blob;
        private IList<CustomAttributeNamedArgument> fields = new Collection<CustomAttributeNamedArgument> ();
        private IList<CustomAttributeNamedArgument> properties = new Collection<CustomAttributeNamedArgument>();
        private IList<CustomAttributeArgument> arguments = new Collection<CustomAttributeArgument> ();

        public IList<CustomAttributeArgument> Arguments
        {
            get { return arguments; }
            set { arguments = value; }
        }

        public IMethodReference Constructor {
			get { return constructor; }
			set { constructor = value; }
		}

		public ITypeReference AttributeType {
			get { return constructor.DeclaringType; }
		}

        public bool IsResolved { get; set; }

		public bool HasConstructorArguments {
			get {
				Resolve ();

				return !Arguments.IsNullOrEmpty ();
			}
		}

		public IList<CustomAttributeArgument> ConstructorArguments {
			get {
				Resolve ();

				return Arguments;
			}
		}

		public bool HasFields {
			get {
				Resolve ();

				return !fields.IsNullOrEmpty ();
			}
		}

		public IList<CustomAttributeNamedArgument> Fields {
			get {
				Resolve ();

                return fields;
			}
            set { fields = value; }

		}

		public bool HasProperties {
			get {
				Resolve ();

				return !properties.IsNullOrEmpty ();
			}
		}

		public IList<CustomAttributeNamedArgument> Properties
		{
		    get {
				Resolve ();

                return properties;
			}
            set { properties = value; }
		}

        public IList<CustomAttributeNamedArgument> GetFields ()
        {
            return fields;
        }

        public IList<CustomAttributeNamedArgument> GetProperties ()
        {
            return properties;
        }

        internal bool HasImage {
			get { return constructor != null && ((MethodReference)constructor).HasImage; }
		}

		internal IModuleDefinition Module {
			get { return constructor.Module; }
		}

		internal CustomAttribute (uint signature, IMethodReference constructor)
		{
			this.Signature = signature;
			this.constructor = constructor;
			this.IsResolved = false;
		}

		public CustomAttribute (IMethodReference constructor)
		{
			this.constructor = constructor;
            this.IsResolved = true;
		}

		public CustomAttribute (IMethodReference constructor, byte [] blob)
		{
			this.constructor = constructor;
            this.IsResolved = false;
			this.blob = blob;
		}

		public byte [] GetBlob ()
		{
			if (blob != null)
				return blob;

			if (!HasImage)
				throw new NotSupportedException ();

			return Module.Read (ref blob, this, (attribute, reader) => reader.ReadCustomAttributeBlob (attribute.Signature));
		}

		void Resolve ()
		{
            if (IsResolved || !HasImage)
				return;

			Module.Read (this, (attribute, reader) => {
				try {
					reader.ReadCustomAttributeSignature (attribute);
                    IsResolved = true;
				} catch (ResolutionException) {
					if (Arguments != null)
						Arguments.Clear ();
					if (fields != null)
						fields.Clear ();
					if (properties != null)
						properties.Clear ();

                    IsResolved = false;
				}
				return this;
			});
		}
	}

	static partial class Mixin {

		public static void CheckName (string name)
		{
			if (name == null)
				throw new ArgumentNullException ("name");
			if (name.Length == 0)
				throw new ArgumentException ("Empty name");
		}
	}
}
