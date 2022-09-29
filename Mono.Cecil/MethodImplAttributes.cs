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

	[Flags]
	public enum MethodImplAttributes : ushort {
		CodeTypeMask			= 0x0003,
		IL						= 0x0000,	// Method impl is CIL
		Native					= 0x0001,	// Method impl is native
		OPTIL					= 0x0002,	// Reserved: shall be zero in conforming implementations
		Runtime					= 0x0003,	// Method impl is provided by the runtime

		ManagedMask				= 0x0004,	// Flags specifying whether the code is managed or unmanaged
		Unmanaged				= 0x0004,	// Method impl is unmanaged, otherwise managed
		Managed					= 0x0000,	// Method impl is managed

		ForwardRef				= 0x0010,	// The method is declared, but its implementation is provided elsewhere.
		PreserveSig				= 0x0080,	// The method signature is exported exactly as declared.
		InternalCall			= 0x1000,	// The call is internal, that is, it calls a method that's implemented within the CLR.
		Synchronized			= 0x0020,	// The method can be executed by only one thread at a time.
		 									// Static methods lock on the type, whereas instance methods lock on the instance.
		NoOptimization			= 0x0040,	// The method is not optimized by the just-in-time (JIT) compiler or by native codegen.
		NoInlining				= 0x0008,	// The method cannot be inlined.
		AggressiveInlining		= 0x0100,	// The method should be inlined if possible.
		AggressiveOptimization	= 0x0200,	// The method contains code that should always be optimized by the JIT compiler.
	}
}
