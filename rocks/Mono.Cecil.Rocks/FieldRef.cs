using System.Linq;
using System.Reflection;

namespace Mono.Cecil.Rocks
{
    public static class FieldRef
    {
        public static FieldReference Of(string name, TypeReference ref_type, ModuleDefinition context)
        { 
            var def_field = ref_type.Resolve().Fields.FirstOrDefault(field => field.Name == name);
            return context.Import(def_field);
        }

        public static FieldReference Of(FieldInfo field, ModuleDefinition context)
        {
            return context.Import(field);
        }
    }
}

