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

using Mono.Collections.Generic;

namespace Mono.Cecil {

	public abstract class PropertyReference : MemberReference {

		TypeReference property_type;

		public TypeReference PropertyType {
			get { return property_type; }
			set { property_type = value; }
		}

		public abstract Collection<ParameterDefinition> Parameters {
			get;
		}

		internal PropertyReference (string name, TypeReference propertyType)
			: base (name)
		{
			if (propertyType == null)
				throw new ArgumentNullException ("propertyType");

			property_type = propertyType;
		}

		public new PropertyDefinition Resolve()
		{
			return (PropertyDefinition)base.Resolve();
		}
	}
}
