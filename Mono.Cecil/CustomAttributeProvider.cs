//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

using Mono.Collections.Generic;

namespace Mono.Cecil {

	class CustomAttributeProvider : ICustomAttributeProvider {

		readonly ModuleDefinition module;
		MetadataToken token;
		Collection<CustomAttribute> custom_attributes;

		public MetadataToken MetadataToken {
			get { return token; }
			set { token = value; }
		}

		public bool HasCustomAttributes {
			get {
				if (custom_attributes != null)
					return custom_attributes.Count > 0;

				return this.GetHasCustomAttributes (module);
			}
		}

		public Collection<CustomAttribute> CustomAttributes {
			get { return custom_attributes ?? this.GetCustomAttributes (ref custom_attributes, module); }
		}

		public CustomAttributeProvider (ModuleDefinition module)
		{
			this.module = module;
			this.token = new MetadataToken (TokenType.InterfaceImpl);
		}

		public CustomAttributeProvider (ModuleDefinition module, MetadataToken token)
		{
			this.module = module;
			this.token = token;
		}

		public void Clear ()
		{
			token = new MetadataToken (TokenType.InterfaceImpl);
			custom_attributes = null;
		}
	}
}
