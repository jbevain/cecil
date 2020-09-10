using System;
using System.Linq;

using Mono.Cecil;
using Mono.Cecil.Cil;

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
		public void InsertAfterWithLocalScopes ()
		{
			var method = CreateTestMethodWithLocalScopes ();
			var il = method.GetILProcessor ();

			il.InsertAfter (
				0,
				il.Create (OpCodes.Nop));

			AssertOpCodeSequence (new [] { OpCodes.Ldloc_0, OpCodes.Nop, OpCodes.Ldloc_1, OpCodes.Ldloc_2 }, method);
			var wholeBodyScope = VerifyWholeBodyScope (method);
			AssertLocalScope (wholeBodyScope.Scopes [0], 0, 2);
			AssertLocalScope (wholeBodyScope.Scopes [1], 2, 3);
			AssertLocalScope (wholeBodyScope.Scopes [2], 3, null);
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
		public void ReplaceWithLocalScopes ()
		{
			var method = CreateTestMethodWithLocalScopes ();
			var il = method.GetILProcessor ();

			// Replace with larger instruction
			var instruction = il.Create (OpCodes.Ldstr, "test");
			instruction.Offset = method.Instructions [1].Offset;
			il.Replace (1, instruction);

			AssertOpCodeSequence (new [] { OpCodes.Ldloc_0, OpCodes.Ldstr, OpCodes.Ldloc_2 }, method);
			var wholeBodyScope = VerifyWholeBodyScope (method);
			AssertLocalScope (wholeBodyScope.Scopes [0], 0, 1);
			AssertLocalScope (wholeBodyScope.Scopes [1], 1, 6); // size of the new instruction is 5 bytes
			AssertLocalScope (wholeBodyScope.Scopes [2], 6, null);
		}

		[Test]
		public void Clear ()
		{
			var method = CreateTestMethod (OpCodes.Ldloc_0, OpCodes.Ldloc_2, OpCodes.Ldloc_3);
			var il = method.GetILProcessor ();

			il.Clear ();

			AssertOpCodeSequence (new OpCode[] { }, method);
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
			AssertLocalScope (debug_info.Scope, 0, null);
			return debug_info.Scope;
		}

		static void AssertLocalScope (ScopeDebugInformation scope, int startOffset, int? endOffset)
		{
			Assert.IsNotNull (scope);
			Assert.AreEqual (startOffset, scope.Start.Offset);
			if (endOffset.HasValue)
				Assert.AreEqual (endOffset.Value, scope.End.Offset);
			else
				Assert.IsTrue (scope.End.IsEndOfMethod);
		}

		static MethodBody CreateTestMethodWithLocalScopes ()
		{
			var methodBody = CreateTestMethod (OpCodes.Ldloc_0, OpCodes.Ldloc_1, OpCodes.Ldloc_2);
			var method = methodBody.Method;
			var debug_info = method.DebugInformation;

			var wholeBodyScope = new ScopeDebugInformation () {
				Start = new InstructionOffset (0),
				End = new InstructionOffset ()
			};
			int size = 0;
			var instruction = methodBody.Instructions [0];
			var innerScopeBegining = new ScopeDebugInformation () { 
				Start = new InstructionOffset (size), 
				End = new InstructionOffset (size + instruction.GetSize ())
			};
			size += instruction.GetSize ();
			wholeBodyScope.Scopes.Add (innerScopeBegining);

			instruction = methodBody.Instructions [1];
			var innerScopeMiddle = new ScopeDebugInformation () {
				Start = new InstructionOffset (size),
				End = new InstructionOffset (size + instruction.GetSize ())
			};
			size += instruction.GetSize ();
			wholeBodyScope.Scopes.Add (innerScopeMiddle);

			var innerScopeEnd = new ScopeDebugInformation () {
				Start = new InstructionOffset (size),
				End = new InstructionOffset ()
			};
			wholeBodyScope.Scopes.Add (innerScopeEnd);

			debug_info.Scope = wholeBodyScope;
			return methodBody;
		}
	}
}
