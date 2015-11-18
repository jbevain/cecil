//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

namespace Mono.Cecil {

	class InterfaceCustomAttributeProvider : CustomAttributeProvider {

		sealed class MetadataTokenProvider : IMetadataTokenProvider {

			public MetadataToken MetadataToken { get; set; }

			public MetadataTokenProvider ()
			{
				MetadataToken = new MetadataToken (TokenType.InterfaceImpl);
			}
		}

		public InterfaceCustomAttributeProvider (ModuleDefinition module)
			: base (module, new MetadataTokenProvider ())
		{
		}

		public override void Clear ()
		{
			MetadataToken = new MetadataToken (TokenType.InterfaceImpl);
			base.Clear ();
		}
	}
}
