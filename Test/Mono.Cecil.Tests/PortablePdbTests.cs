using System;
using System.IO;

using NUnit.Framework;

using Mono.Cecil.Cil;

namespace Mono.Cecil.Tests {

	[TestFixture]
	public class PortablePdbTests : BaseTestFixture {

		[Test]
		public void SequencePoints ()
		{
			TestPortablePdbModule (module => {
				var type = module.GetType ("PdbTarget.Program");
				var main = type.GetMethod ("Main");

				AssertCode (@"
	.locals init (System.Int32 a, System.String[] V_1, System.Int32 V_2, System.String arg)
	.line 21,21:3,4 'C:\sources\PdbTarget\Program.cs'
	IL_0000: nop
	.line 22,22:4,11 'C:\sources\PdbTarget\Program.cs'
	IL_0001: nop
	.line 22,22:24,28 'C:\sources\PdbTarget\Program.cs'
	IL_0002: ldarg.0
	IL_0003: stloc.1
	IL_0004: ldc.i4.0
	IL_0005: stloc.2
	.line 16707566,0:16707566,0 'C:\sources\PdbTarget\Program.cs'
	IL_0006: br.s IL_0017
	.line 22,22:13,20 'C:\sources\PdbTarget\Program.cs'
	IL_0008: ldloc.1
	IL_0009: ldloc.2
	IL_000a: ldelem.ref
	IL_000b: stloc.3
	.line 23,23:5,20 'C:\sources\PdbTarget\Program.cs'
	IL_000c: ldloc.3
	IL_000d: call System.Void System.Console::WriteLine(System.String)
	IL_0012: nop
	.line 16707566,0:16707566,0 'C:\sources\PdbTarget\Program.cs'
	IL_0013: ldloc.2
	IL_0014: ldc.i4.1
	IL_0015: add
	IL_0016: stloc.2
	.line 22,22:21,23 'C:\sources\PdbTarget\Program.cs'
	IL_0017: ldloc.2
	IL_0018: ldloc.1
	IL_0019: ldlen
	IL_001a: conv.i4
	IL_001b: blt.s IL_0008
	.line 25,25:4,22 'C:\sources\PdbTarget\Program.cs'
	IL_001d: ldc.i4.1
	IL_001e: ldc.i4.2
	IL_001f: call System.Int32 System.Math::Min(System.Int32,System.Int32)
	IL_0024: stloc.0
	.line 26,26:3,4 'C:\sources\PdbTarget\Program.cs'
	IL_0025: ret
", main);
			});
		}

		[Test]
		public void SequencePointsMultipleDocument ()
		{
			TestPortablePdbModule (module => {
				var type = module.GetType ("PdbTarget.B");
				var main = type.GetMethod (".ctor");

				AssertCode (@"
	.locals ()
	.line 7,7:3,25 'C:\sources\PdbTarget\B.cs'
	IL_0000: ldarg.0
	IL_0001: ldstr """"
	IL_0006: stfld System.String PdbTarget.B::s
	.line 110,110:3,21 'C:\sources\PdbTarget\Program.cs'
	IL_000b: ldarg.0
	IL_000c: ldc.i4.2
	IL_000d: stfld System.Int32 PdbTarget.B::a
	.line 111,111:3,21 'C:\sources\PdbTarget\Program.cs'
	IL_0012: ldarg.0
	IL_0013: ldc.i4.3
	IL_0014: stfld System.Int32 PdbTarget.B::b
	.line 9,9:3,13 'C:\sources\PdbTarget\B.cs'
	IL_0019: ldarg.0
	IL_001a: call System.Void System.Object::.ctor()
	IL_001f: nop
	.line 10,10:3,4 'C:\sources\PdbTarget\B.cs'
	IL_0020: nop
	.line 11,11:4,19 'C:\sources\PdbTarget\B.cs'
	IL_0021: ldstr ""B""
	IL_0026: call System.Void System.Console::WriteLine(System.String)
	IL_002b: nop
	.line 12,12:3,4 'C:\sources\PdbTarget\B.cs'
	IL_002c: ret
", main);
			});
		}

		[Test]
		public void LocalVariables ()
		{
			TestPortablePdbModule (module => {
				var type = module.GetType ("PdbTarget.Program");
				var method = type.GetMethod ("Bar");
				var debug_info = method.DebugInformation;

				Assert.IsNotNull (debug_info.Scope);
				Assert.IsTrue (debug_info.Scope.HasScopes);
				Assert.AreEqual (2, debug_info.Scope.Scopes.Count);

				var scope = debug_info.Scope.Scopes [0];

				Assert.IsNotNull (scope);
				Assert.IsTrue (scope.HasVariables);
				Assert.AreEqual (1, scope.Variables.Count);

				var variable = scope.Variables [0];

				Assert.AreEqual ("s", variable.Name);
				Assert.IsFalse (variable.IsDebuggerHidden);
				Assert.AreEqual (2, variable.Index);

				scope = debug_info.Scope.Scopes [1];

				Assert.IsNotNull (scope);
				Assert.IsTrue (scope.HasVariables);
				Assert.AreEqual (1, scope.Variables.Count);

				variable = scope.Variables [0];

				Assert.AreEqual ("s", variable.Name);
				Assert.IsFalse (variable.IsDebuggerHidden);
				Assert.AreEqual (3, variable.Index);

				Assert.IsTrue (scope.HasScopes);
				Assert.AreEqual (1, scope.Scopes.Count);

				scope = scope.Scopes [0];

				Assert.IsNotNull (scope);
				Assert.IsTrue (scope.HasVariables);
				Assert.AreEqual (1, scope.Variables.Count);

				variable = scope.Variables [0];

				Assert.AreEqual ("u", variable.Name);
				Assert.IsFalse (variable.IsDebuggerHidden);
				Assert.AreEqual (5, variable.Index);
			});
		}

		[Test]
		public void LocalConstants ()
		{
			TestPortablePdbModule (module => {
				var type = module.GetType ("PdbTarget.Program");
				var method = type.GetMethod ("Bar");
				var debug_info = method.DebugInformation;

				Assert.IsNotNull (debug_info.Scope);
				Assert.IsTrue (debug_info.Scope.HasScopes);
				Assert.AreEqual (2, debug_info.Scope.Scopes.Count);

				var scope = debug_info.Scope.Scopes [1];

				Assert.IsNotNull (scope);
				Assert.IsTrue (scope.HasConstants);
				Assert.AreEqual (2, scope.Constants.Count);

				var constant = scope.Constants [0];

				Assert.AreEqual ("b", constant.Name);
				Assert.AreEqual (12, constant.Value);
				Assert.AreEqual (MetadataType.Int32, constant.ConstantType.MetadataType);

				constant = scope.Constants [1];
				Assert.AreEqual ("c", constant.Name);
				Assert.AreEqual ((decimal) 74, constant.Value);
				Assert.AreEqual (MetadataType.ValueType, constant.ConstantType.MetadataType);

				method = type.GetMethod ("Foo");
				debug_info = method.DebugInformation;

				Assert.IsNotNull (debug_info.Scope);
				Assert.IsTrue (debug_info.Scope.HasConstants);
				Assert.AreEqual (4, debug_info.Scope.Constants.Count);

				constant = debug_info.Scope.Constants [0];
				Assert.AreEqual ("s", constant.Name);
				Assert.AreEqual ("const string", constant.Value);
				Assert.AreEqual (MetadataType.String, constant.ConstantType.MetadataType);

				constant = debug_info.Scope.Constants [1];
				Assert.AreEqual ("f", constant.Name);
				Assert.AreEqual (1, constant.Value);
				Assert.AreEqual (MetadataType.Int32, constant.ConstantType.MetadataType);

				constant = debug_info.Scope.Constants [2];
				Assert.AreEqual ("o", constant.Name);
				Assert.AreEqual (null, constant.Value);
				Assert.AreEqual (MetadataType.Object, constant.ConstantType.MetadataType);

				constant = debug_info.Scope.Constants [3];
				Assert.AreEqual ("u", constant.Name);
				Assert.AreEqual (null, constant.Value);
				Assert.AreEqual (MetadataType.String, constant.ConstantType.MetadataType);
			});
		}

		[Test]
		public void ImportScope ()
		{
			TestPortablePdbModule (module => {
				var type = module.GetType ("PdbTarget.Program");
				var method = type.GetMethod ("Bar");
				var debug_info = method.DebugInformation;

				Assert.IsNotNull (debug_info.Scope);

				var import = debug_info.Scope.Import;
				Assert.IsNotNull (import);

				Assert.IsFalse (import.HasTargets);
				Assert.IsNotNull (import.Parent);

				import = import.Parent;

				Assert.IsTrue (import.HasTargets);
				Assert.AreEqual (9, import.Targets.Count);
				var target = import.Targets [0];

				Assert.AreEqual (ImportTargetKind.ImportAlias, target.Kind);
				Assert.AreEqual ("XML", target.Alias);

				target = import.Targets [1];

				Assert.AreEqual (ImportTargetKind.ImportNamespace, target.Kind);
				Assert.AreEqual ("System", target.Namespace);

				target = import.Targets [2];

				Assert.AreEqual (ImportTargetKind.ImportNamespace, target.Kind);
				Assert.AreEqual ("System.Collections.Generic", target.Namespace);

				target = import.Targets [3];

				Assert.AreEqual (ImportTargetKind.ImportNamespace, target.Kind);
				Assert.AreEqual ("System.IO", target.Namespace);

				target = import.Targets [4];

				Assert.AreEqual (ImportTargetKind.ImportNamespace, target.Kind);
				Assert.AreEqual ("System.Threading.Tasks", target.Namespace);

				target = import.Targets [5];

				Assert.AreEqual (ImportTargetKind.ImportNamespaceInAssembly, target.Kind);
				Assert.AreEqual ("System.Xml.Resolvers", target.Namespace);
				Assert.AreEqual ("System.Xml", target.AssemblyReference.Name);


				target = import.Targets [6];

				Assert.AreEqual (ImportTargetKind.ImportType, target.Kind);
				Assert.AreEqual ("System.Console", target.Type.FullName);

				target = import.Targets [7];

				Assert.AreEqual (ImportTargetKind.ImportType, target.Kind);
				Assert.AreEqual ("System.Math", target.Type.FullName);

				target = import.Targets [8];

				Assert.AreEqual (ImportTargetKind.DefineTypeAlias, target.Kind);
				Assert.AreEqual ("Foo", target.Alias);
				Assert.AreEqual ("System.Xml.XmlDocumentType", target.Type.FullName);

				Assert.IsNotNull (import.Parent);

				import = import.Parent;

				Assert.IsTrue (import.HasTargets);
				Assert.AreEqual (1, import.Targets.Count);
				Assert.IsNull (import.Parent);

				target = import.Targets [0];

				Assert.AreEqual (ImportTargetKind.DefineAssemblyAlias, target.Kind);
				Assert.AreEqual ("XML", target.Alias);
				Assert.AreEqual ("System.Xml", target.AssemblyReference.Name);
			});
		}

		[Test]
		public void StateMachineKickOff ()
		{
			TestPortablePdbModule (module => {
				var state_machine = module.GetType ("PdbTarget.Program/<Baz>d__7");
				var main = state_machine.GetMethod ("MoveNext");
				var symbol = main.DebugInformation;

				Assert.IsNotNull (symbol);
				Assert.IsNotNull (symbol.StateMachineKickOffMethod);
				Assert.AreEqual ("System.Threading.Tasks.Task PdbTarget.Program::Baz(System.IO.StreamReader)", symbol.StateMachineKickOffMethod.FullName);
			});
		}

		[Test]
		public void StateMachineCustomDebugInformation ()
		{
			TestPortablePdbModule (module => {
				var state_machine = module.GetType ("PdbTarget.Program/<Baz>d__7");
				var move_next = state_machine.GetMethod ("MoveNext");

				Assert.IsTrue (move_next.HasCustomDebugInformations);
				Assert.AreEqual (2, move_next.CustomDebugInformations.Count);

				var state_machine_scope = move_next.CustomDebugInformations [0] as StateMachineScopeDebugInformation;
				Assert.IsNotNull (state_machine_scope);
				Assert.AreEqual (0, state_machine_scope.Start.Offset);
				Assert.IsTrue (state_machine_scope.End.IsEndOfMethod);

				var async_body = move_next.CustomDebugInformations [1] as AsyncMethodBodyDebugInformation;
				Assert.IsNotNull (async_body);
				Assert.AreEqual (-1, async_body.CatchHandler.Offset);

				Assert.AreEqual (2, async_body.Yields.Count);
				Assert.AreEqual (61, async_body.Yields [0].Offset);
				Assert.AreEqual (221, async_body.Yields [1].Offset);

				Assert.AreEqual (2, async_body.Resumes.Count);
				Assert.AreEqual (91, async_body.Resumes [0].Offset);
				Assert.AreEqual (252, async_body.Resumes [1].Offset);

				Assert.AreEqual (move_next, async_body.MoveNextMethod);
			});
		}

		void TestPortablePdbModule (Action<ModuleDefinition> test)
		{
			TestModule ("PdbTarget.exe", test, symbolReaderProvider: typeof (PortablePdbReaderProvider), symbolWriterProvider: typeof (PortablePdbWriterProvider));
			TestModule ("EmbeddedPdbTarget.exe", test, verify: !Platform.OnMono);
		}

		[Test]
		public void RoundTripCecilPortablePdb ()
		{
			TestModule ("cecil.dll", module => {
				Assert.IsTrue (module.HasSymbols);
			}, symbolReaderProvider: typeof (PortablePdbReaderProvider), symbolWriterProvider: typeof (PortablePdbWriterProvider));
		}
	}
}
