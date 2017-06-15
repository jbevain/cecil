// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;

using Mono.Collections.Generic;

using Mono.Cecil.Cil;

namespace Mono.Cecil.WindowsPdb {

	public class NativePdbReader : ISymbolReader {

		internal NativePdbReader (Disposable<Stream> file)
		{
		}

#if !READ_ONLY
		public ISymbolWriterProvider GetWriterProvider ()
		{
			return new NativePdbWriterProvider ();
		}
#endif

		public bool ProcessDebugHeader (ImageDebugHeader header)
		{
			throw null;
		}

		public MethodDebugInformation Read (MethodDefinition method)
		{
			throw null;
		}

		public void Dispose ()
		{
			throw null;
		}
	}
}
