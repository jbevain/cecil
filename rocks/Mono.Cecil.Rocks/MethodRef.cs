using System;
using System.Reflection;

namespace Mono.Cecil.Rocks
{	
	public static class MethodRef
	{
        /// <summary>
        /// Imports method, covered by <paramref name="method"> parameter to be used in module <paramref name="context"/>
        /// </summary>
        /// <param name="context">Context to import to</param>
        /// <param name="method">Method to be imported</param>
        public static MethodReference Of(ModuleDefinition context, Action method)
		{
			return ResolveMethod(method.Method, context);
		}
		
        /// <summary>
        /// Imports method, covered by <paramref name="method"> parameter to be used in module <paramref name="context"/>
        /// </summary>
        /// <param name="context">Context to import to</param>
        /// <param name="method">Method to be imported</param>
        public static MethodReference Of<T1>(ModuleDefinition context, Action<T1> method)
		{
			return ResolveMethod(method.Method, context);
		}
		
        /// <summary>
        /// Imports method, covered by <paramref name="method"> parameter to be used in module <paramref name="context"/>
        /// </summary>
        /// <param name="context">Context to import to</param>
        /// <param name="method">Method to be imported</param>
        public static MethodReference Of<T1, T2>(ModuleDefinition context, Action<T1, T2> method)
		{
			return ResolveMethod(method.Method, context);
		}
		
        /// <summary>
        /// Imports method, covered by <paramref name="method"> parameter to be used in module <paramref name="context"/>
        /// </summary>
        /// <param name="context">Context to import to</param>
        /// <param name="method">Method to be imported</param>
        public static MethodReference Of<T1, T2, T3>(ModuleDefinition context, Action<T1, T2, T3> method)
		{
			return ResolveMethod(method.Method, context);
		}
		
        /// <summary>
        /// Imports method, covered by <paramref name="method"> parameter to be used in module <paramref name="context"/>
        /// </summary>
        /// <param name="context">Context to import to</param>
        /// <param name="method">Method to be imported</param>
        public static MethodReference Of<T1, T2, T3, T4>(ModuleDefinition context, Action<T1, T2, T3, T4> method)
		{
			return ResolveMethod(method.Method, context);
		}
		
        /// <summary>
        /// Imports method, covered by <paramref name="method"> parameter to be used in module <paramref name="context"/>
        /// </summary>
        /// <param name="context">Context to import to</param>
        /// <param name="method">Method to be imported</param>
        public static MethodReference Of<T1, T2, T3, T4, T5>(ModuleDefinition context, Action<T1, T2, T3, T4, T5> method)
		{
			return ResolveMethod(method.Method, context);
		}
		
        /// <summary>
        /// Imports method, covered by <paramref name="method"> parameter to be used in module <paramref name="context"/>
        /// </summary>
        /// <param name="context">Context to import to</param>
        /// <param name="method">Method to be imported</param>
        public static MethodReference Of<T1, T2, T3, T4, T5, T6>(ModuleDefinition context, Action<T1, T2, T3, T4, T5, T6> method)
		{
			return ResolveMethod(method.Method, context);
		}
		
        /// <summary>
        /// Imports method, covered by <paramref name="method"> parameter to be used in module <paramref name="context"/>
        /// </summary>
        /// <param name="context">Context to import to</param>
        /// <param name="method">Method to be imported</param>
        public static MethodReference Of<T1, T2, T3, T4, T5, T6, T7>(ModuleDefinition context, Action<T1, T2, T3, T4, T5, T6, T7> method)
		{
			return ResolveMethod(method.Method, context);
		}
		
        /// <summary>
        /// Imports method, covered by <paramref name="method"> parameter to be used in module <paramref name="context"/>
        /// </summary>
        /// <param name="context">Context to import to</param>
        /// <param name="method">Method to be imported</param>
        public static MethodReference Of<T1, T2, T3, T4, T5, T6, T7, T8>(ModuleDefinition context, Action<T1, T2, T3, T4, T5, T6, T7, T8> method)
		{
			return ResolveMethod(method.Method, context);
		}
		
        /// <summary>
        /// Imports method, covered by <paramref name="method"> parameter to be used in module <paramref name="context"/>
        /// </summary>
        /// <param name="context">Context to import to</param>
        /// <param name="method">Method to be imported</param>
        public static MethodReference Of<T1>(ModuleDefinition context, Func<T1> method)
		{
			return ResolveMethod(method.Method, context);
		}
		
        /// <summary>
        /// Imports method, covered by <paramref name="method"> parameter to be used in module <paramref name="context"/>
        /// </summary>
        /// <param name="context">Context to import to</param>
        /// <param name="method">Method to be imported</param>
        public static MethodReference Of<T1, T2>(ModuleDefinition context, Func<T1, T2> method)
		{
			return ResolveMethod(method.Method, context);
		}
		
        /// <summary>
        /// Imports method, covered by <paramref name="method"> parameter to be used in module <paramref name="context"/>
        /// </summary>
        /// <param name="context">Context to import to</param>
        /// <param name="method">Method to be imported</param>
        public static MethodReference Of<T1, T2, T3>(ModuleDefinition context, Func<T1, T2, T3> method)
		{
			return ResolveMethod(method.Method, context);
		}
		
        /// <summary>
        /// Imports method, covered by <paramref name="method"> parameter to be used in module <paramref name="context"/>
        /// </summary>
        /// <param name="context">Context to import to</param>
        /// <param name="method">Method to be imported</param>
        public static MethodReference Of<T1, T2, T3, T4>(ModuleDefinition context, Func<T1, T2, T3, T4> method)
		{
			return ResolveMethod(method.Method, context);
		}
		
        /// <summary>
        /// Imports method, covered by <paramref name="method"> parameter to be used in module <paramref name="context"/>
        /// </summary>
        /// <param name="context">Context to import to</param>
        /// <param name="method">Method to be imported</param>
        public static MethodReference Of<T1, T2, T3, T4, T5>(ModuleDefinition context, Func<T1, T2, T3, T4, T5> method)
		{
			return ResolveMethod(method.Method, context);
		}
		
        /// <summary>
        /// Imports method, covered by <paramref name="method"> parameter to be used in module <paramref name="context"/>
        /// </summary>
        /// <param name="context">Context to import to</param>
        /// <param name="method">Method to be imported</param>
        public static MethodReference Of<T1, T2, T3, T4, T5, T6>(ModuleDefinition context, Func<T1, T2, T3, T4, T5, T6> method)
		{
			return ResolveMethod(method.Method, context);
		}
		
        /// <summary>
        /// Imports method, covered by <paramref name="method"> parameter to be used in module <paramref name="context"/>
        /// </summary>
        /// <param name="context">Context to import to</param>
        /// <param name="method">Method to be imported</param>
        public static MethodReference Of<T1, T2, T3, T4, T5, T6, T7>(ModuleDefinition context, Func<T1, T2, T3, T4, T5, T6, T7> method)
		{
			return ResolveMethod(method.Method, context);
		}
		
        /// <summary>
        /// Imports method, covered by <paramref name="method"> parameter to be used in module <paramref name="context"/>
        /// </summary>
        /// <param name="context">Context to import to</param>
        /// <param name="method">Method to be imported</param>
        public static MethodReference Of<T1, T2, T3, T4, T5, T6, T7, T8>(ModuleDefinition context, Func<T1, T2, T3, T4, T5, T6, T7, T8> method)
		{
			return ResolveMethod(method.Method, context);
		}
		
        /// <summary>
        /// Imports method, covered by <paramref name="method"> parameter to be used in module <paramref name="context"/>
        /// </summary>
        /// <param name="context">Context to import to</param>
        /// <param name="method">Method to be imported</param>
        private static MethodReference ResolveMethod(MethodInfo method, ModuleDefinition context)
		{
			return context.Import(method);
		}		
	}
}

