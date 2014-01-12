using System;
using System.Reflection;
using Mono.Cecil.Cil;
using System.Collections.Generic;

namespace Mono.Cecil.Rocks
{
    /// <summary>
    /// Should be used for getting Mono::Cecil MethodDefinition by runtime MethodInfo (Action or Func actually)
    /// </summary>
	public static class MethodDef
	{
        public static MethodDefinition Of(ModuleDefinition module, Action method)
        {
            return ResolveMethod(module, method.Method);
		}
		
        public static MethodDefinition Of<T1>(ModuleDefinition module, Action<T1> method)
		{
            return ResolveMethod(module, method.Method);
		}

        public static MethodDefinition Of<T1, T2>(ModuleDefinition module, Action<T1, T2> method)
		{
            return ResolveMethod(module, method.Method);
		}
		
        public static MethodDefinition Of<T1, T2, T3>(ModuleDefinition module, Action<T1, T2, T3> method)
		{
            return ResolveMethod(module, method.Method);
		}

        public static MethodDefinition Of<T1, T2, T3, T4>(ModuleDefinition module, Action<T1, T2, T3, T4> method)
		{
            return ResolveMethod(module, method.Method);
		}

        public static MethodDefinition Of<T1, T2, T3, T4, T5>(ModuleDefinition module, Action<T1, T2, T3, T4, T5> method)
		{
            return ResolveMethod(module, method.Method);
		}

        public static MethodDefinition Of<T1, T2, T3, T4, T5, T6>(ModuleDefinition module, Action<T1, T2, T3, T4, T5, T6> method)
		{
            return ResolveMethod(module, method.Method);
		}

        public static MethodDefinition Of<T1, T2, T3, T4, T5, T6, T7>(ModuleDefinition module, Action<T1, T2, T3, T4, T5, T6, T7> method)
		{
            return ResolveMethod(module, method.Method);
		}

        public static MethodDefinition Of<T1, T2, T3, T4, T5, T6, T7, T8>(ModuleDefinition module, Action<T1, T2, T3, T4, T5, T6, T7, T8> method)
		{
            return ResolveMethod(module, method.Method);
		}

        public static MethodDefinition Of<T1>(ModuleDefinition module, Func<T1> method)
		{
            return ResolveMethod(module, method.Method);
		}
		
        public static MethodDefinition Of<T1, T2>(ModuleDefinition module, Func<T1, T2> method)
		{
            return ResolveMethod(module, method.Method);
		}
		
        public static MethodDefinition Of<T1, T2, T3>(ModuleDefinition module, Func<T1, T2, T3> method)
		{
            return ResolveMethod(module, method.Method);
		}
		
        public static MethodDefinition Of<T1, T2, T3, T4>(ModuleDefinition module, Func<T1, T2, T3, T4> method)
		{
            return ResolveMethod(module, method.Method);
		}
		
        public static MethodDefinition Of<T1, T2, T3, T4, T5>(ModuleDefinition module, Func<T1, T2, T3, T4, T5> method)
		{
            return ResolveMethod(module, method.Method);
		}
		
        public static MethodDefinition Of<T1, T2, T3, T4, T5, T6>(ModuleDefinition module, Func<T1, T2, T3, T4, T5, T6> method)
		{
            return ResolveMethod(module, method.Method);
		}
		
        public static MethodDefinition Of<T1, T2, T3, T4, T5, T6, T7>(ModuleDefinition module, Func<T1, T2, T3, T4, T5, T6, T7> method)
		{
            return ResolveMethod(module, method.Method);
		}
		
        public static MethodDefinition Of<T1, T2, T3, T4, T5, T6, T7, T8>(ModuleDefinition module, Func<T1, T2, T3, T4, T5, T6, T7, T8> method)
		{
            return ResolveMethod(module, method.Method);
		}

        private static MethodDefinition ResolveMethod(ModuleDefinition module, MethodInfo method)
		{
            return module.Import(method).Resolve();
        }
	}
}
