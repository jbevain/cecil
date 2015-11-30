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

namespace Mono.Cecil {

	public abstract class MemberReference : IMetadataTokenProvider {

		string name;
		TypeReference declaring_type;

		internal MetadataToken token;
		internal uint treatment = 0;

		public virtual string Name {
			get { return name; }
			set {
				if (Treatment != MemberReferenceTreatment.None && value != name)
					throw new InvalidOperationException ("Projected member reference name can't be changed.");
				name = value;
			}
		}

		public abstract string FullName {
			get;
		}

		public virtual TypeReference DeclaringType {
			get { return declaring_type; }
			set { declaring_type = value; }
		}

		public MetadataToken MetadataToken {
			get { return token; }
			set { token = value; }
		}

		internal bool HasImage {
			get {
				var module = Module;
				if (module == null)
					return false;

				return module.HasImage;
			}
		}

		public virtual ModuleDefinition Module {
			get { return declaring_type != null ? declaring_type.Module : null; }
		}

		public virtual bool IsDefinition {
			get { return false; }
		}

		public virtual bool ContainsGenericParameter {
			get { return declaring_type != null && declaring_type.ContainsGenericParameter; }
		}

		internal MemberReferenceTreatment Treatment {
			get { return (MemberReferenceTreatment) treatment; }
			set { treatment = (uint) value; }
		}

		internal MemberReference ()
		{
		}

		internal MemberReference (string name)
		{
			this.name = name ?? string.Empty;
		}

		internal string MemberFullName ()
		{
			if (declaring_type == null)
				return name;

			return declaring_type.FullName + "::" + name;
		}

		public override string ToString ()
		{
			return FullName;
		}
	}
}
