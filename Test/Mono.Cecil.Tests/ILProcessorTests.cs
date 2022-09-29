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
			var methodBody = CreateTestMethodWithLocalScopes (roundtripType, padIL);
			methodBody = RoundtripMethodBody (methodBody, roundtripType, forceUnresolved, reverseScopes);

			var il = methodBody.GetILProcessor ();
			il.InsertAfter (1, il.Create (OpCodes.Ldstr, "Test"));

			methodBody = RoundtripMethodBody (methodBody, roundtripType, false, reverseScopes);
			var wholeBodyScope = VerifyWholeBodyScope (methodBody);
			AssertLocalScope (methodBody, wholeBodyScope.Scopes [0], 1, 3);
			AssertLocalScope (methodBody, wholeBodyScope.Scopes [1], 4, null);
			AssertLocalScope (methodBody, wholeBodyScope.Scopes [1].Scopes [0], 5, 6);
			AssertStateMachineScope (methodBody, 1, 7);
			AssertAsyncMethodSteppingInfo (methodBody, 0, 1, 1);
			AssertAsyncMethodSteppingInfo (methodBody, 1, 5, 6);
			AssertAsyncMethodSteppingInfo (methodBody, 2, 7, 7);

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
			var methodBody = CreateTestMethodWithLocalScopes (roundtripType, padIL);
			methodBody = RoundtripMethodBody (methodBody, roundtripType, forceUnresolved, reverseScopes);

			var il = methodBody.GetILProcessor ();
			il.RemoveAt (1);

			methodBody = RoundtripMethodBody (methodBody, roundtripType, false, reverseScopes);
			var wholeBodyScope = VerifyWholeBodyScope (methodBody);
			AssertLocalScope (methodBody, wholeBodyScope.Scopes [0], 1, 1);
			AssertLocalScope (methodBody, wholeBodyScope.Scopes [1], 2, null);
			AssertLocalScope (methodBody, wholeBodyScope.Scopes [1].Scopes [0], 3, 4);
			AssertStateMachineScope (methodBody, 1, 5);
			AssertAsyncMethodSteppingInfo (methodBody, 0, 1, 1);
			AssertAsyncMethodSteppingInfo (methodBody, 1, 3, 4);
			AssertAsyncMethodSteppingInfo (methodBody, 2, 5, 5);

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
			var methodBody = CreateTestMethodWithLocalScopes (roundtripType, padIL);
			methodBody = RoundtripMethodBody (methodBody, roundtripType, forceUnresolved, reverseScopes);

			var il = methodBody.GetILProcessor ();
			il.Replace (1, il.Create (OpCodes.Ldstr, "Test"));

			methodBody = RoundtripMethodBody (methodBody, roundtripType, false, reverseScopes);
			var wholeBodyScope = VerifyWholeBodyScope (methodBody);
			AssertLocalScope (methodBody, wholeBodyScope.Scopes [0], 1, 2);
			AssertLocalScope (methodBody, wholeBodyScope.Scopes [1], 3, null);
			AssertLocalScope (methodBody, wholeBodyScope.Scopes [1].Scopes [0], 4, 5);
			AssertStateMachineScope (methodBody, 1, 6);
			AssertAsyncMethodSteppingInfo (methodBody, 0, 1, 1);
			AssertAsyncMethodSteppingInfo (methodBody, 1, 4, 5);
			AssertAsyncMethodSteppingInfo (methodBody, 2, 6, 6);

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
			var methodBody = CreateTestMethodWithLocalScopes (roundtripType, padIL);
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
			AssertStateMachineScope (methodBody, 1, 7);
			AssertAsyncMethodSteppingInfo (methodBody, 0, 1, 1);
			AssertAsyncMethodSteppingInfo (methodBody, 1, 4, 5);
			AssertAsyncMethodSteppingInfo (methodBody, 2, 7, 7);

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

		static MethodDefinition CreateEmptyTestMethod (ModuleDefinition module, string name)
		{
			var method = new MethodDefinition {
				Name = name,
				Attributes = MethodAttributes.Public | MethodAttributes.Static
			};

			var il = method.Body.GetILProcessor ();
			il.Emit (OpCodes.Ret);

			method.ReturnType = module.ImportReference (typeof (void));

			return method;
		}

		static ScopeDebugInformation VerifyWholeBodyScope (MethodBody body)
		{
			var debug_info = body.Method.DebugInformation;
			Assert.IsNotNull (debug_info);
			AssertLocalScope (body, debug_info.Scope, 0, null);
			return debug_info.Scope;
		}

		static void AssertInstructionOffset (Instruction instruction, InstructionOffset instructionOffset)
		{
			if (instructionOffset.IsResolved)
				Assert.AreEqual (instruction, instructionOffset.ResolvedInstruction);
			else
				Assert.AreEqual (instruction.Offset, instructionOffset.Offset);
		}

		static void AssertEndOfScopeOffset (MethodBody methodBody, InstructionOffset instructionOffset, int? index)
		{
			if (index.HasValue)
				AssertInstructionOffset (methodBody.Instructions [index.Value], instructionOffset);
			else
				Assert.IsTrue (instructionOffset.IsEndOfMethod);
		}

		static void AssertLocalScope (MethodBody methodBody, ScopeDebugInformation scope, int startIndex, int? endIndex)
		{
			Assert.IsNotNull (scope);
			AssertInstructionOffset (methodBody.Instructions [startIndex], scope.Start);
			AssertEndOfScopeOffset (methodBody, scope.End, endIndex);
		}

		static void AssertStateMachineScope (MethodBody methodBody, int startIndex, int? endIndex)
		{
			var customDebugInfo = methodBody.Method.HasCustomDebugInformations ? methodBody.Method.CustomDebugInformations : methodBody.Method.DebugInformation.CustomDebugInformations;
			var stateMachineScope = customDebugInfo.OfType<StateMachineScopeDebugInformation> ().SingleOrDefault ();
			Assert.IsNotNull (stateMachineScope);
			Assert.AreEqual (1, stateMachineScope.Scopes.Count);
			AssertInstructionOffset (methodBody.Instructions [startIndex], stateMachineScope.Scopes [0].Start);
			AssertEndOfScopeOffset (methodBody, stateMachineScope.Scopes [0].End, endIndex);
		}

		static void AssertAsyncMethodSteppingInfo (MethodBody methodBody, int infoNumber, int yieldIndex, int resumeIndex)
		{
			var customDebugInfo = methodBody.Method.HasCustomDebugInformations ? methodBody.Method.CustomDebugInformations : methodBody.Method.DebugInformation.CustomDebugInformations;
			var asyncMethodInfo = customDebugInfo.OfType<AsyncMethodBodyDebugInformation> ().SingleOrDefault ();
			Assert.IsNotNull (asyncMethodInfo);
			Assert.Greater (asyncMethodInfo.Yields.Count, infoNumber);
			Assert.AreEqual (asyncMethodInfo.Yields.Count, asyncMethodInfo.Resumes.Count);
			AssertInstructionOffset (methodBody.Instructions [yieldIndex], asyncMethodInfo.Yields [infoNumber]);
			AssertInstructionOffset (methodBody.Instructions [resumeIndex], asyncMethodInfo.Resumes [infoNumber]);
		}

		static MethodBody CreateTestMethodWithLocalScopes (RoundtripType roundtripType, bool padILWithNulls)
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

			var emptyMethod = CreateEmptyTestMethod (module, "empty");
			type.Methods.Add (emptyMethod);

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
			// Scopes
			//            | Scope | Scope.Scopes[0] | Scope.Scopes[1] | Scope.Scopes[1].Scopes[0]
			// #0 Nop     | >     |                 |                 |
			// #1 Ldloc_0 | .     | >               |                 |
			// #2 Nop     | .     | <               |                 |
			// #3 Ldloc_1 | .     |                 | >               | 
			// #4 Nop     | .     |                 | .               | >
			// #5 Ldloc_2 | .     |                 | .               | <
			// #6 Nop     | .     |                 | .               |
			// <end of m> | <     |                 | <               |  
			//
			// Async and state machine infos
			//            | Catch handler | Yields | Resumes | State machine |
			// #0 Nop     |               |        |         |               |
			// #1 Ldloc_0 |               | 0      | 0       | >             |
			// #2 Nop     |               |        |         | .             |
			// #3 Ldloc_1 |               |        |         | .             |
			// #4 Nop     |               | 1      |         | .             |
			// #5 Ldloc_2 |               |        | 1       | .             |
			// #6 Nop     | *             | 2      | 2       | <             |
			// <end of m> |               |        |         |               |

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

			// For some reason the Native PDB reader/writer store the custom info on the method.DebugInfo.CustomInfo, while portable PDB stores it on method.CustomInfo.
			var customDebugInfo = (roundtripType == RoundtripType.Pdb && Platform.HasNativePdbSupport)
				? method.DebugInformation.CustomDebugInformations : method.CustomDebugInformations;
			customDebugInfo.Add (new StateMachineScopeDebugInformation () {
				Scopes = {
					new StateMachineScope(instructions[1], instructions[6])
				}
			});
			customDebugInfo.Add (new AsyncMethodBodyDebugInformation () {
				CatchHandler = new InstructionOffset (instructions [6]),
				Yields = { 
					new InstructionOffset (instructions [1]),
					new InstructionOffset (instructions [4]),
					new InstructionOffset (instructions [6])
				},
				Resumes = {
					new InstructionOffset (instructions [1]),
					new InstructionOffset (instructions [5]),
					new InstructionOffset (instructions [6]),
				},
				ResumeMethods = {
					emptyMethod,
					emptyMethod,
					emptyMethod
				}
			});

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
