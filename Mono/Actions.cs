//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

#if !NET_3_5 && !NET_4_0

namespace Mono {
	//delegate void Action ();
	delegate void Action<T1, T2> (T1 arg1, T2 arg2);
	//delegate void Action<T1, T2, T3> (T1 arg1, T2 arg2, T3 arg3);
	//delegate void Action<T1, T2, T3, T4> (T1 arg1, T2 arg2, T3 arg3, T4 arg4);
}

#endif
