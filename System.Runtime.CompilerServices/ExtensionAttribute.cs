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

#if !NET_3_5 && !NET_4_0

namespace System.Runtime.CompilerServices {

	[AttributeUsage (AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly)]
	sealed class ExtensionAttribute : Attribute {
	}
}

#endif
