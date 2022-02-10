using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Mdb;
using Mono.Cecil.Pdb;
using NUnit.Framework;

namespace Mono.Cecil.Tests {

	[TestFixture]
	public class ILProcessorTests : BaseTestFixture {

		[Test]
		public void Append ()
		{
			var method = CreateTestMethod ();
			var il = method.GetILProcessor ();

			var ret = il.Create (OpCodes.Ret);
			il.Append (ret);

			AssertOpCodeSequence (new [] { OpCodes.Ret }, method);
		}

		[Test]
		public void InsertBefore ()
		{
			var method = CreateTestMethod (OpCodes.Ldloc_0, OpCodes.Ldloc_2, OpCodes.Ldloc_3);
			var il = method.GetILProcessor ();

			var ldloc_2 = method.Instructions.Where (i => i.OpCode == OpCodes.Ldloc_2).First ();

			il.InsertBefore (
				ldloc_2,
				il.Create (OpCodes.Ldloc_1));

			AssertOpCodeSequence (new [] { OpCodes.Ldloc_0, OpCodes.Ldloc_1, OpCodes.Ldloc_2, OpCodes.Ldloc_3 }, method);
		}

		[Test]
		public void InsertBeforeIssue697 ()
		{
			var parameters = new ReaderParameters { SymbolReaderProvider = new MdbReaderProvider () };
			using (var module = GetResourceModule ("Issue697.dll", parameters))
			{
				var pathGetterDef = module.GetTypes ()
					.SelectMany (t => t.Methods)
					.First (m => m.Name.Equals ("get_Defer"));

				var body = pathGetterDef.Body;
				var worker = body.GetILProcessor ();
				var initialBody = body.Instructions.ToList ();
				var head = initialBody.First ();
				var opcode = worker.Create (OpCodes.Ldc_I4_1);
				worker.InsertBefore (head, opcode);
				worker.InsertBefore (head, worker.Create (OpCodes.Ret));
				initialBody.ForEach (worker.Remove);
				AssertOpCodeSequence (new [] { OpCodes.Ldc_I4_1, OpCodes.Ret }, pathGetterDef.body);	
			}
		}

		[Test]
		public void InsertBeforeIssue697bis ()
		{
			var parameters = new ReaderParameters { SymbolReaderProvider = new MdbReaderProvider () };
			using (var module = GetResourceModule ("Issue697.dll", parameters)) {
				var pathGetterDef = module.GetTypes ()
					.SelectMany (t => t.Methods)
					.First (m => m.Name.Equals ("get_Defer"));

				var body = pathGetterDef.Body;
				var worker = body.GetILProcessor ();
				var initialBody = body.Instructions.ToList ();
				Console.WriteLine (initialBody.Sum (i => i.GetSize ()));

				var head = initialBody.First ();
				var opcode = worker.Create (OpCodes.Ldc_I4_1);
				worker.InsertBefore (head, opcode);

				Assert.That (pathGetterDef.DebugInformation.Scope.Start.IsEndOfMethod, Is.False);
				foreach (var subscope in pathGetterDef.DebugInformation.Scope.Scopes)
					Assert.That (subscope.Start.IsEndOfMethod, Is.False);

				// big test -- we can write w/o crashing
				var unique = Guid.NewGuid ().ToString ();
				var output = Path.GetTempFileName ();
				var outputdll = output + ".dll";

				var writer = new WriterParameters () {
					SymbolWriterProvider = new MdbWriterProvider (),
					WriteSymbols = true
				};
				using (var sink = File.Open (outputdll, FileMode.Create, FileAccess.ReadWrite)) {
					module.Write (sink, writer);
				}

				Assert.Pass ();
			}
		}

		[Test]
		public void InsertAfter ()
		{
			var method = CreateTestMethod (OpCodes.Ldloc_0, OpCodes.Ldloc_2, OpCodes.Ldloc_3);
			var il = method.GetILProcessor ();

			var ldloc_0 = method.Instructions.First ();

			il.InsertAfter (
				ldloc_0,
				il.Create (OpCodes.Ldloc_1));

			AssertOpCodeSequence (new [] { OpCodes.Ldloc_0, OpCodes.Ldloc_1, OpCodes.Ldloc_2, OpCodes.Ldloc_3 }, method);
		}

		[Test]
		public void InsertAfterUsingIndex ()
		{
			var method = CreateTestMethod (OpCodes.Ldloc_0, OpCodes.Ldloc_2, OpCodes.Ldloc_3);
			var il = method.GetILProcessor ();

			il.InsertAfter (
				0,
				il.Create (OpCodes.Ldloc_1));

			AssertOpCodeSequence (new [] { OpCodes.Ldloc_0, OpCodes.Ldloc_1, OpCodes.Ldloc_2, OpCodes.Ldloc_3 }, method);
		}

		[Test]
		public void ReplaceUsingIndex ()
		{
			var method = CreateTestMethod (OpCodes.Ldloc_0, OpCodes.Ldloc_2, OpCodes.Ldloc_3);
			var il = method.GetILProcessor ();

			il.Replace (1, il.Create (OpCodes.Nop));

			AssertOpCodeSequence (new [] { OpCodes.Ldloc_0, OpCodes.Nop, OpCodes.Ldloc_3 }, method);
		}

		[Test]
		public void Clear ()
		{
			var method = CreateTestMethod (OpCodes.Ldloc_0, OpCodes.Ldloc_2, OpCodes.Ldloc_3);
			var il = method.GetILProcessor ();

			il.Clear ();

			AssertOpCodeSequence (new OpCode[] { }, method);
		}

		[TestCase (RoundtripType.None, false, false, false)]
		[TestCase (RoundtripType.Pdb, false, false, false)]
		[TestCase (RoundtripType.Pdb, true, false, false)]
		[TestCase (RoundtripType.Pdb, true, false, true)]
		[TestCase (RoundtripType.Pdb, true, true, false)]
		[TestCase (RoundtripType.PortablePdb, false, false, false)]
		[TestCase (RoundtripType.PortablePdb, true, false, false)]
		[TestCase (RoundtripType.PortablePdb, true, false, true)]
		[TestCase (RoundtripType.PortablePdb, true, true, false)]
		public void InsertAfterWithSymbolRoundtrip (RoundtripType roundtripType, bool forceUnresolved, bool reverseScopes, bool padIL)
		{
			var methodBody = CreateTestMethodWithLocalScopes (padIL);
			methodBody = RoundtripMethodBody (methodBody, roundtripType, forceUnresolved, reverseScopes);

			var il = methodBody.GetILProcessor ();
			il.InsertAfter (1, il.Create (OpCodes.Ldstr, "Test"));

			methodBody = RoundtripMethodBody (methodBody, roundtripType, false, reverseScopes);
			var wholeBodyScope = VerifyWholeBodyScope (methodBody);
			AssertLocalScope (methodBody, wholeBodyScope.Scopes [0], 1, 3);
			AssertLocalScope (methodBody, wholeBodyScope.Scopes [1], 4, null);
			AssertLocalScope (methodBody, wholeBodyScope.Scopes [1].Scopes [0], 5, 6);

			methodBody.Method.Module.Dispose ();
		}

		[TestCase (RoundtripType.None, false, false, false)]
		[TestCase (RoundtripType.Pdb, false, false, false)]
		[TestCase (RoundtripType.Pdb, true, false, false)]
		[TestCase (RoundtripType.Pdb, true, false, true)]
		[TestCase (RoundtripType.Pdb, true, true, false)]
		[TestCase (RoundtripType.PortablePdb, false, false, false)]
		[TestCase (RoundtripType.PortablePdb, true, false, false)]
		[TestCase (RoundtripType.PortablePdb, true, false, true)]
		[TestCase (RoundtripType.PortablePdb, true, true, false)]
		public void RemoveWithSymbolRoundtrip (RoundtripType roundtripType, bool forceUnresolved, bool reverseScopes, bool padIL)
		{
			var methodBody = CreateTestMethodWithLocalScopes (padIL);
			methodBody = RoundtripMethodBody (methodBody, roundtripType, forceUnresolved, reverseScopes);

			var il = methodBody.GetILProcessor ();
			il.RemoveAt (1);

			methodBody = RoundtripMethodBody (methodBody, roundtripType, false, reverseScopes);
			var wholeBodyScope = VerifyWholeBodyScope (methodBody);
			AssertLocalScope (methodBody, wholeBodyScope.Scopes [0], 1, 1);
			AssertLocalScope (methodBody, wholeBodyScope.Scopes [1], 2, null);
			AssertLocalScope (methodBody, wholeBodyScope.Scopes [1].Scopes [0], 3, 4);

			methodBody.Method.Module.Dispose ();
		}

		[TestCase (RoundtripType.None, false, false, false)]
		[TestCase (RoundtripType.Pdb, false, false, false)]
		[TestCase (RoundtripType.Pdb, true, false, false)]
		[TestCase (RoundtripType.Pdb, true, false, true)]
		[TestCase (RoundtripType.Pdb, true, true, false)]
		[TestCase (RoundtripType.PortablePdb, false, false, false)]
		[TestCase (RoundtripType.PortablePdb, true, false, false)]
		[TestCase (RoundtripType.PortablePdb, true, false, true)]
		[TestCase (RoundtripType.PortablePdb, true, true, false)]
		public void ReplaceWithSymbolRoundtrip (RoundtripType roundtripType, bool forceUnresolved, bool reverseScopes, bool padIL)
		{
			var methodBody = CreateTestMethodWithLocalScopes (padIL);
			methodBody = RoundtripMethodBody (methodBody, roundtripType, forceUnresolved, reverseScopes);

			var il = methodBody.GetILProcessor ();
			il.Replace (1, il.Create (OpCodes.Ldstr, "Test"));

			methodBody = RoundtripMethodBody (methodBody, roundtripType, false, reverseScopes);
			var wholeBodyScope = VerifyWholeBodyScope (methodBody);
			AssertLocalScope (methodBody, wholeBodyScope.Scopes [0], 1, 2);
			AssertLocalScope (methodBody, wholeBodyScope.Scopes [1], 3, null);
			AssertLocalScope (methodBody, wholeBodyScope.Scopes [1].Scopes [0], 4, 5);

			methodBody.Method.Module.Dispose ();
		}

		[TestCase (RoundtripType.None, false, false, false)]
		[TestCase (RoundtripType.Pdb, false, false, false)]
		[TestCase (RoundtripType.Pdb, true, false, false)]
		[TestCase (RoundtripType.Pdb, true, false, true)]
		[TestCase (RoundtripType.Pdb, true, true, false)]
		[TestCase (RoundtripType.PortablePdb, false, false, false)]
		[TestCase (RoundtripType.PortablePdb, true, false, false)]
		[TestCase (RoundtripType.PortablePdb, true, false, true)]
		[TestCase (RoundtripType.PortablePdb, true, true, false)]
		public void EditBodyWithScopesAndSymbolRoundtrip (RoundtripType roundtripType, bool forceUnresolved, bool reverseScopes, bool padIL)
		{
			var methodBody = CreateTestMethodWithLocalScopes (padIL);
			methodBody = RoundtripMethodBody (methodBody, roundtripType, forceUnresolved, reverseScopes);

			var il = methodBody.GetILProcessor ();
			il.Replace (4, il.Create (OpCodes.Ldstr, "Test"));
			il.InsertAfter (5, il.Create (OpCodes.Ldloc_3));
			var tempVar3 = new VariableDefinition (methodBody.Method.Module.ImportReference (typeof (string)));
			methodBody.Variables.Add (tempVar3);
			methodBody.Method.DebugInformation.Scope.Scopes [reverseScopes ? 0 : 1].Scopes.Insert (reverseScopes ? 0 : 1,
				new ScopeDebugInformation (methodBody.Instructions [5], methodBody.Instructions [6]) {
					Variables = { new VariableDebugInformation (tempVar3, "tempVar3") }
				});

			methodBody = RoundtripMethodBody (methodBody, roundtripType, false, reverseScopes);
			var wholeBodyScope = VerifyWholeBodyScope (methodBody);
			AssertLocalScope (methodBody, wholeBodyScope.Scopes [0], 1, 2);
			AssertLocalScope (methodBody, wholeBodyScope.Scopes [1], 3, null);
			AssertLocalScope (methodBody, wholeBodyScope.Scopes [1].Scopes [0], 4, 5);
			AssertLocalScope (methodBody, wholeBodyScope.Scopes [1].Scopes [1], 5, 6);

			methodBody.Method.Module.Dispose ();
		}

		[Test]
		public void EditWithDebugInfoButNoLocalScopes()
		{
			var methodBody = CreateTestMethod (OpCodes.Ret);
			methodBody.Method.DebugInformation = new MethodDebugInformation (methodBody.Method);

			var il = methodBody.GetILProcessor ();
			il.Replace (methodBody.Instructions [0], il.Create (OpCodes.Nop));

			Assert.AreEqual (1, methodBody.Instructions.Count);
			Assert.AreEqual (Code.Nop, methodBody.Instructions [0].OpCode.Code);
			Assert.Null (methodBody.Method.DebugInformation.Scope);
		}

		static void AssertOpCodeSequence (OpCode [] expected, MethodBody body)
		{
			var opcodes = body.Instructions.Select (i => i.OpCode).ToArray ();
			Assert.AreEqual (expected.Length, opcodes.Length);

			for (int i = 0; i < opcodes.Length; i++)
				Assert.AreEqual (expected [i], opcodes [i]);
		}

		static MethodBody CreateTestMethod (params OpCode [] opcodes)
		{
			var method = new MethodDefinition {
				Name = "function",
				Attributes = MethodAttributes.Public | MethodAttributes.Static
			};

			var il = method.Body.GetILProcessor ();

			foreach (var opcode in opcodes)
				il.Emit (opcode);

			var instructions = method.Body.Instructions;
			int size = 0;
			for (int i = 0; i < instructions.Count; i++) {
				var instruction = instructions [i];
				instruction.Offset = size;
				size += instruction.GetSize ();
			}

			return method.Body;
		}

		static ScopeDebugInformation VerifyWholeBodyScope (MethodBody body)
		{
			var debug_info = body.Method.DebugInformation;
			Assert.IsNotNull (debug_info);
			AssertLocalScope (body, debug_info.Scope, 0, null);
			return debug_info.Scope;
		}

		static void AssertLocalScope (MethodBody methodBody, ScopeDebugInformation scope, int startIndex, int? endIndex)
		{
			Assert.IsNotNull (scope);
			Assert.AreEqual (methodBody.Instructions [startIndex], scope.Start.ResolvedInstruction);
			if (endIndex.HasValue)
				Assert.AreEqual (methodBody.Instructions [endIndex.Value], scope.End.ResolvedInstruction);
			else
				Assert.IsTrue (scope.End.IsEndOfMethod);
		}

		static MethodBody CreateTestMethodWithLocalScopes (bool padILWithNulls)
		{
			var module = ModuleDefinition.CreateModule ("TestILProcessor", ModuleKind.Dll);
			var type = new TypeDefinition ("NS", "TestType", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed, module.ImportReference (typeof (object)));
			module.Types.Add (type);

			var methodBody = CreateTestMethod (OpCodes.Nop, OpCodes.Ldloc_0, OpCodes.Nop, OpCodes.Ldloc_1, OpCodes.Nop, OpCodes.Ldloc_2, OpCodes.Nop);
			if (padILWithNulls)
				methodBody.Instructions.Capacity += 10;

			var method = methodBody.Method;
			method.ReturnType = module.ImportReference (typeof (void));
			type.Methods.Add (method);

			methodBody.InitLocals = true;
			var tempVar1 = new VariableDefinition (module.ImportReference (typeof (string)));
			methodBody.Variables.Add (tempVar1);
			var tempVar2 = new VariableDefinition (module.ImportReference (typeof (string)));
			methodBody.Variables.Add (tempVar2);

			var debug_info = method.DebugInformation;

			// Must add a sequence point, otherwise the native PDB writer will not actually write the method's debug info
			// into the PDB.
			foreach (var instr in methodBody.Instructions) {
				var sequence_point = new SequencePoint (instr, new Document (@"C:\test.cs")) {
					StartLine = 0,
					StartColumn = 0,
					EndLine = 0,
					EndColumn = 4,
				};
				debug_info.SequencePoints.Add (sequence_point);
			}

			// The method looks like this:
			//            | Scope | Scope.Scopes[0] | Scope.Scopes[1] | Scope.Scopes[1].Scopes[0]
			// #0 Nop     | >     |                 |                 |
			// #1 Ldloc_0 | .     | >               |                 |
			// #2 Nop     | .     | <               |                 |
			// #3 Ldloc_1 | .     |                 | >               | 
			// #4 Nop     | .     |                 | .               | >
			// #5 Ldloc_2 | .     |                 | .               | <
			// #6 Nop     | .     |                 | .               |
			// <end of m> | <     |                 | <               |  

			var instructions = methodBody.Instructions;
			debug_info.Scope = new ScopeDebugInformation (instructions[0], null) {
				Scopes = {
					new ScopeDebugInformation (instructions[1], instructions[2]) {
						Variables = { new VariableDebugInformation(tempVar1, "tempVar1") }
					},
					new ScopeDebugInformation (instructions[3], null) {
						Scopes = {
							new ScopeDebugInformation (instructions[4], instructions[5]) {
								Variables = { new VariableDebugInformation(tempVar2, "tempVar2") }
							}
						}
					}
				}
			};

			return methodBody;
		}

		public enum RoundtripType {
			None,
			Pdb,
			PortablePdb
		}

		static MethodBody RoundtripMethodBody(MethodBody methodBody, RoundtripType roundtripType, bool forceUnresolvedScopes = false, bool reverseScopeOrder = false)
		{
			var newModule = RoundtripModule (methodBody.Method.Module, roundtripType);
			var newMethodBody = newModule.GetType ("NS.TestType").GetMethod ("function").Body;

			if (forceUnresolvedScopes)
				UnresolveScopes (newMethodBody.Method.DebugInformation.Scope);

			if (reverseScopeOrder)
				ReverseScopeOrder (newMethodBody.Method.DebugInformation.Scope);

			return newMethodBody;
		}

		static void UnresolveScopes(ScopeDebugInformation scope)
		{
			scope.Start = new InstructionOffset (scope.Start.Offset);
			if (!scope.End.IsEndOfMethod)
				scope.End = new InstructionOffset (scope.End.Offset);

			foreach (var subScope in scope.Scopes)
				UnresolveScopes (subScope);
		}

		static void ReverseScopeOrder(ScopeDebugInformation scope)
		{
			List<ScopeDebugInformation> subScopes = scope.Scopes.ToList ();
			subScopes.Reverse ();
			scope.Scopes.Clear ();
			foreach (var subScope in subScopes)
				scope.Scopes.Add (subScope);

			foreach (var subScope in scope.Scopes)
				ReverseScopeOrder (subScope);
		}

		static ModuleDefinition RoundtripModule(ModuleDefinition module, RoundtripType roundtripType)
		{
			if (roundtripType == RoundtripType.None)
				return module;

			var file = Path.Combine (Path.GetTempPath (), "TestILProcessor.dll");
			if (File.Exists (file))
				File.Delete (file);

			ISymbolWriterProvider symbolWriterProvider;
			switch (roundtripType) {
			case RoundtripType.Pdb when Platform.HasNativePdbSupport:
				symbolWriterProvider = new PdbWriterProvider ();
				break;
			case RoundtripType.PortablePdb:
			default:
				symbolWriterProvider = new PortablePdbWriterProvider ();
				break;
			}

			module.Write (file, new WriterParameters {
				SymbolWriterProvider = symbolWriterProvider,
			});
			module.Dispose ();

			ISymbolReaderProvider symbolReaderProvider;
			switch (roundtripType) {
			case RoundtripType.Pdb when Platform.HasNativePdbSupport:
				symbolReaderProvider = new PdbReaderProvider ();
				break;
			case RoundtripType.PortablePdb:
			default:
				symbolReaderProvider = new PortablePdbReaderProvider ();
				break;
			}

			return ModuleDefinition.ReadModule (file, new ReaderParameters {
				SymbolReaderProvider = symbolReaderProvider,
				InMemory = true
			});
		}
	}
}
