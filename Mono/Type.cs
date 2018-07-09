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
using System.Reflection;

namespace Mono {
	static class TypeExtensions {

		public static TypeCode GetTypeCode (this Type type)
		{
			return Type.GetTypeCode (type);
		}

		public static Assembly Assembly (this Type type)
		{
			return type.Assembly;
		}

		public static MethodBase DeclaringMethod (this Type type)
		{
			return type.DeclaringMethod;
		}

		public static Type [] GetGenericArguments (this Type type)
		{
			return type.GetGenericArguments ();
		}

		public static bool IsGenericType (this Type type)
		{
			return type.IsGenericType;
		}

		public static bool IsGenericTypeDefinition (this Type type)
		{
			return type.IsGenericTypeDefinition;
		}

		public static bool IsValueType (this Type type)
		{
			return type.IsValueType;
		}
	}
}
