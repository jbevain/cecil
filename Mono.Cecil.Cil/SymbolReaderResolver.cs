//
// Author:
//   Virgile Bello (virgile.bello@gmail.com)
//
// Copyright (c) 2016 - 2016 Virgile Bello
//
// Licensed under the MIT/X11 license.
//

namespace Mono.Cecil.Cil {
	sealed class SymbolReaderResolver : ISymbolReaderResolver {
		private MetadataReader reader;

		public SymbolReaderResolver(MetadataReader reader)
		{
			this.reader = reader;
		}

		public MethodReference LookupMethod(MetadataToken old_token)
		{
			var provider = reader.LookupToken(old_token);
			return provider as MethodReference;
		}
	}
}