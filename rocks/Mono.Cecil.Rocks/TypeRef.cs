using Mono.Cecil.Cil;
using System;

namespace Mono.Cecil.Rocks
{
    public static class TypeRef
    {
        public static TypeReference Of<T>(ModuleDefinition context)
        {
            return context.Import(typeof(T));
        }
        
        public static TypeReference Of(Type type, ModuleDefinition context)
        {
            return context.Import(type);
        }

        public static TypeReference Of(FieldReference field, ModuleDefinition context = null)
        {
            return context == null ? field.FieldType : context.Import(field.FieldType);
        }
        
        public static TypeReference Of(ParameterReference param, ModuleDefinition context = null)
        {
            return context == null ? param.ParameterType : context.Import(param.ParameterType);
        }
        
        public static TypeReference Of(VariableReference variable, ModuleDefinition context = null)
        {
            return context == null ? variable.VariableType : context.Import(variable.VariableType);
        }
        
        public static TypeReference Of(TypeReference type, ModuleDefinition context = null)
        {
            return context == null ? type : context.Import(type);
        }
    }
}

