using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Mdb;

public class Issue
{
  private static void Main(string[] args)
  {
    var here = Assembly.GetExecutingAssembly().Location;
    var target = Path.Combine(
      Path.GetDirectoryName(here),
      "Sample3.dll"
      );

    var definition = AssemblyDefinition.ReadAssembly(target);

    var provider = new MdbReaderProvider();
    var reader = provider.GetSymbolReader(definition.MainModule, target);
    definition.MainModule.ReadSymbols(reader);

    var pathGetterDef =
               definition.MainModule.GetTypes().
                 SelectMany(t => t.Methods).
                 First(m => m.Name.Equals("get_Defer"));

    var body = pathGetterDef.Body;
    var worker = body.GetILProcessor();
    var initialBody = body.Instructions.ToList();
    var head = initialBody.First();
    var opcode = worker.Create(OpCodes.Ldc_I4_1);
    worker.InsertBefore(head, opcode);
  }
}