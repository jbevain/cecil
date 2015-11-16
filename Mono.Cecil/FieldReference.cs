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

	public class FieldReference : MemberReference {

		TypeReference field_type;

		public TypeReference FieldType {
			get { return field_type; }
			set { field_type = value; }
		}

		public override string FullName {
			get { return field_type.FullName + " " + MemberFullName (); }
		}

		public override bool ContainsGenericParameter {
			get { return field_type.ContainsGenericParameter || base.ContainsGenericParameter; }
		}

		internal FieldReference ()
		{
			this.token = new MetadataToken (TokenType.MemberRef);
		}

		public FieldReference (string name, TypeReference fieldType)
			: base (name)
		{
			if (fieldType == null)
				throw new ArgumentNullException ("fieldType");

			this.field_type = fieldType;
			this.token = new MetadataToken (TokenType.MemberRef);
		}

		public FieldReference (string name, TypeReference fieldType, TypeReference declaringType)
			: this (name, fieldType)
		{
			if (declaringType == null)
				throw new ArgumentNullException("declaringType");

			this.DeclaringType = declaringType;
		}

		protected override IMemberDefinition ResolveImpl()
		{
			var module = this.Module;
			if (module == null)
				throw new NotSupportedException();

			return module.Resolve(this);
		}

		public new FieldDefinition Resolve()
		{
			return (FieldDefinition)base.Resolve();
		}
	}
}
