using System;
using System.Linq;

using Mono.Cecil;
using Mono.Cecil.Cil;

using NUnit.Framework;

namespace Mono.Cecil.Tests {

	[TestFixture]
	public class VariableTests : BaseTestFixture {

		[Test]
		public void AddVariableIndex ()
		{
			var object_ref = new TypeReference ("System", "Object", null, null, false);
			var method = new MethodDefinition ("foo", MethodAttributes.Static, object_ref);
			var body = new MethodBody (method);

			var x = new VariableDefinition (object_ref);
			var y = new VariableDefinition (object_ref);

			body.Variables.Add (x);
			body.Variables.Add (y);

			Assert.AreEqual (0, x.Index);
			Assert.AreEqual (1, y.Index);
		}

		[Test]
		public void RemoveAtVariableIndex ()
		{
			var object_ref = new TypeReference ("System", "Object", null, null, false);
			var method = new MethodDefinition ("foo", MethodAttributes.Static, object_ref);
			var body = new MethodBody (method);

			var x = new VariableDefinition (object_ref);
			var y = new VariableDefinition (object_ref);
			var z = new VariableDefinition (object_ref);

			body.Variables.Add (x);
			body.Variables.Add (y);
			body.Variables.Add (z);

			Assert.AreEqual (0, x.Index);
			Assert.AreEqual (1, y.Index);
			Assert.AreEqual (2, z.Index);

			body.Variables.RemoveAt (1);

			Assert.AreEqual (0, x.Index);
			Assert.AreEqual (-1, y.Index);
			Assert.AreEqual (1, z.Index);
		}

		[Test]
		public void RemoveVariableIndex ()
		{
			var object_ref = new TypeReference ("System", "Object", null, null, false);
			var method = new MethodDefinition ("foo", MethodAttributes.Static, object_ref);
			var body = new MethodBody (method);

			var x = new VariableDefinition (object_ref);
			var y = new VariableDefinition (object_ref);
			var z = new VariableDefinition (object_ref);

			body.Variables.Add (x);
			body.Variables.Add (y);
			body.Variables.Add (z);

			Assert.AreEqual (0, x.Index);
			Assert.AreEqual (1, y.Index);
			Assert.AreEqual (2, z.Index);

			body.Variables.Remove (y);

			Assert.AreEqual (0, x.Index);
			Assert.AreEqual (-1, y.Index);
			Assert.AreEqual (1, z.Index);
		}

		[Test]
		public void RemoveVariableWithDebugInfo ()
		{
			var object_ref = new TypeReference ("System", "Object", null, null, false);
			var method = new MethodDefinition ("foo", MethodAttributes.Static, object_ref);
			var body = new MethodBody (method);
			var il = body.GetILProcessor ();
			il.Emit (OpCodes.Ret);

			var x = new VariableDefinition (object_ref);
			var y = new VariableDefinition (object_ref);
			var z = new VariableDefinition (object_ref);
			var z2 = new VariableDefinition (object_ref);

			body.Variables.Add (x);
			body.Variables.Add (y);
			body.Variables.Add (z);
			body.Variables.Add (z2);

			var scope = new ScopeDebugInformation (body.Instructions [0], body.Instructions [0]);
			method.DebugInformation = new MethodDebugInformation (method) {
				Scope = scope
			};
			scope.Variables.Add (new VariableDebugInformation (x.index, nameof (x)));
			scope.Variables.Add (new VariableDebugInformation (y.index, nameof (y)));
			scope.Variables.Add (new VariableDebugInformation (z.index, nameof (z)));
			scope.Variables.Add (new VariableDebugInformation (z2, nameof (z2)));

			body.Variables.Remove (y);

			Assert.AreEqual (3, scope.Variables.Count);
			Assert.AreEqual (x.Index, scope.Variables [0].Index);
			Assert.AreEqual (nameof (x), scope.Variables [0].Name);
			Assert.AreEqual (z.Index, scope.Variables [1].Index);
			Assert.AreEqual (nameof (z), scope.Variables [1].Name);
			Assert.AreEqual (z2.Index, scope.Variables [2].Index);
			Assert.AreEqual (nameof (z2), scope.Variables [2].Name);
		}

		[Test]
		public void InsertVariableIndex ()
		{
			var object_ref = new TypeReference ("System", "Object", null, null, false);
			var method = new MethodDefinition ("foo", MethodAttributes.Static, object_ref);
			var body = new MethodBody (method);

			var x = new VariableDefinition (object_ref);
			var y = new VariableDefinition (object_ref);
			var z = new VariableDefinition (object_ref);

			body.Variables.Add (x);
			body.Variables.Add (z);

			Assert.AreEqual (0, x.Index);
			Assert.AreEqual (-1, y.Index);
			Assert.AreEqual (1, z.Index);

			body.Variables.Insert (1, y);

			Assert.AreEqual (0, x.Index);
			Assert.AreEqual (1, y.Index);
			Assert.AreEqual (2, z.Index);
		}

		[Test]
		public void InsertVariableWithDebugInfo ()
		{
			var object_ref = new TypeReference ("System", "Object", null, null, false);
			var method = new MethodDefinition ("foo", MethodAttributes.Static, object_ref);
			var body = new MethodBody (method);
			var il = body.GetILProcessor ();
			il.Emit (OpCodes.Ret);

			var x = new VariableDefinition (object_ref);
			var y = new VariableDefinition (object_ref);
			var z = new VariableDefinition (object_ref);
			var z2 = new VariableDefinition (object_ref);

			body.Variables.Add (x);
			body.Variables.Add (z);
			body.Variables.Add (z2);

			var scope = new ScopeDebugInformation (body.Instructions [0], body.Instructions [0]);
			method.DebugInformation = new MethodDebugInformation (method) {
				Scope = scope
			};
			scope.Variables.Add (new VariableDebugInformation (x.index, nameof (x)));
			scope.Variables.Add (new VariableDebugInformation (z.index, nameof (z)));
			scope.Variables.Add (new VariableDebugInformation (z2, nameof (z2)));

			body.Variables.Insert (1, y);

			// Adding local variable doesn't add debug info for it (since there's no way to deduce the name of the variable)
			Assert.AreEqual (3, scope.Variables.Count);
			Assert.AreEqual (x.Index, scope.Variables [0].Index);
			Assert.AreEqual (nameof (x), scope.Variables [0].Name);
			Assert.AreEqual (z.Index, scope.Variables [1].Index);
			Assert.AreEqual (nameof (z), scope.Variables [1].Name);
			Assert.AreEqual (z2.Index, scope.Variables [2].Index);
			Assert.AreEqual (nameof (z2), scope.Variables [2].Name);
		}
	}
}
