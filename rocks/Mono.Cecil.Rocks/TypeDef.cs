using Mono.Cecil.Cil;

namespace Mono.Cecil.Rocks
{
    public static class TypeDef
    {
        public static TypeDefinition Of(FieldReference field)
        {
            return field.FieldType.Resolve();
        }
        
        public static TypeDefinition Of(ParameterReference param)
        {
            return param.ParameterType.Resolve();
        }
        
        public static TypeDefinition Of(VariableReference variable)
        {
            return variable.VariableType.Resolve();
        }
        
        public static TypeDefinition Of(TypeReference type)
        {
            return type.Resolve();
        }
    }
}

