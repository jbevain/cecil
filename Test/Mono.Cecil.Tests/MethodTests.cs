using System;
using System.Linq;

using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Metadata;
using Mono.Collections.Generic;
using NUnit.Framework;

namespace Mono.Cecil.Tests {

	[TestFixture]
	public class MethodTests : BaseTestFixture {

		[Test]
		public void AbstractMethod ()
		{
			TestCSharp ("Methods.cs", module => {
				var type = module.Types [1];
				Assert.AreEqual ("Foo", type.Name);
				Assert.AreEqual (2, type.Methods.Count);

				var method = type.GetMethod ("Bar");
				Assert.AreEqual ("Bar", method.Name);
				Assert.IsTrue (method.IsAbstract);
				Assert.IsNotNull (method.ReturnType);

				Assert.AreEqual (1, method.Parameters.Count);

				var parameter = method.Parameters [0];

				Assert.AreEqual ("a", parameter.Name);
				Assert.AreEqual ("System.Int32", parameter.ParameterType.FullName);
			});
		}

		[Test]
		public void SimplePInvoke ()
		{
			TestCSharp ("Methods.cs", module => {
				var bar = module.GetType ("Bar");
				var pan = bar.GetMethod ("Pan");

				Assert.IsTrue (pan.IsPInvokeImpl);
				Assert.IsNotNull (pan.PInvokeInfo);

				Assert.AreEqual ("Pan", pan.PInvokeInfo.EntryPoint);
				Assert.IsNotNull (pan.PInvokeInfo.Module);
				Assert.AreEqual ("foo.dll", pan.PInvokeInfo.Module.Name);
			});
		}

		[Test]
		public void GenericMethodDefinition ()
		{
			TestCSharp ("Generics.cs", module => {
				var baz = module.GetType ("Baz");

				var gazonk = baz.GetMethod ("Gazonk");

				Assert.IsNotNull (gazonk);

				Assert.IsTrue (gazonk.HasGenericParameters);
				Assert.AreEqual (1, gazonk.GenericParameters.Count);
				Assert.AreEqual ("TBang", gazonk.GenericParameters [0].Name);
			});
		}

		[Test]
		public void ReturnGenericInstance ()
		{
			TestCSharp ("Generics.cs", module => {
				var bar = module.GetType ("Bar`1");

				var self = bar.GetMethod ("Self");
				Assert.IsNotNull (self);

				var bar_t = self.ReturnType;

				Assert.IsTrue (bar_t.IsGenericInstance);

				var bar_t_instance = (GenericInstanceType) bar_t;

				Assert.AreEqual (bar.GenericParameters [0], bar_t_instance.GenericArguments [0]);

				var self_str = bar.GetMethod ("SelfString");
				Assert.IsNotNull (self_str);

				var bar_str = self_str.ReturnType;
				Assert.IsTrue (bar_str.IsGenericInstance);

				var bar_str_instance = (GenericInstanceType) bar_str;

				Assert.AreEqual ("System.String", bar_str_instance.GenericArguments [0].FullName);
			});
		}

		[Test]
		public void ReturnGenericInstanceWithMethodParameter ()
		{
			TestCSharp ("Generics.cs", module => {
				var baz = module.GetType ("Baz");

				var gazoo = baz.GetMethod ("Gazoo");
				Assert.IsNotNull (gazoo);

				var bar_bingo = gazoo.ReturnType;

				Assert.IsTrue (bar_bingo.IsGenericInstance);

				var bar_bingo_instance = (GenericInstanceType) bar_bingo;

				Assert.AreEqual (gazoo.GenericParameters [0], bar_bingo_instance.GenericArguments [0]);
			});
		}

		[Test]
		public void SimpleOverrides ()
		{
			TestCSharp ("Interfaces.cs", module => {
				var ibingo = module.GetType ("IBingo");
				var ibingo_foo = ibingo.GetMethod ("Foo");
				Assert.IsNotNull (ibingo_foo);

				var ibingo_bar = ibingo.GetMethod ("Bar");
				Assert.IsNotNull (ibingo_bar);

				var bingo = module.GetType ("Bingo");

				var foo = bingo.GetMethod ("IBingo.Foo");
				Assert.IsNotNull (foo);

				Assert.IsTrue (foo.HasOverrides);
				Assert.AreEqual (ibingo_foo, foo.Overrides [0]);

				var bar = bingo.GetMethod ("IBingo.Bar");
				Assert.IsNotNull (bar);

				Assert.IsTrue (bar.HasOverrides);
				Assert.AreEqual (ibingo_bar, bar.Overrides [0]);
			});
		}

		[Test]
		public void VarArgs ()
		{
			TestModule ("varargs.exe", module => {
				var module_type = module.Types [0];

				Assert.AreEqual (3, module_type.Methods.Count);

				var bar = module_type.GetMethod ("Bar");
				var baz = module_type.GetMethod ("Baz");
				var foo = module_type.GetMethod ("Foo");

				Assert.IsTrue (bar.IsVarArg ());
				Assert.IsFalse (baz.IsVarArg ());

				Assert.IsTrue (foo.IsVarArg ());

				var foo_reference = (MethodReference) baz.Body.Instructions.First (i => i.Offset == 0x000a).Operand;

				Assert.IsTrue (foo_reference.IsVarArg ());
				Assert.AreEqual (0, foo_reference.GetSentinelPosition ());

				Assert.AreEqual (foo, foo_reference.Resolve ());

				var bar_reference = (MethodReference) baz.Body.Instructions.First (i => i.Offset == 0x0023).Operand;

				Assert.IsTrue (bar_reference.IsVarArg ());

				Assert.AreEqual (1, bar_reference.GetSentinelPosition ());

				Assert.AreEqual (bar, bar_reference.Resolve ());
			});
		}

		[Test]
		public void GenericInstanceMethod ()
		{
			TestCSharp ("Generics.cs", module => {
				var type = module.GetType ("It");
				var method = type.GetMethod ("ReadPwow");

				GenericInstanceMethod instance = null;

				foreach (var instruction in method.Body.Instructions) {
					instance = instruction.Operand as GenericInstanceMethod;
					if (instance != null)
						break;
				}

				Assert.IsNotNull (instance);

				Assert.AreEqual (TokenType.MethodSpec, instance.MetadataToken.TokenType);
				Assert.AreNotEqual (0, instance.MetadataToken.RID);
			});
		}

		[Test]
		public void MethodRefDeclaredOnGenerics ()
		{
			TestCSharp ("Generics.cs", module => {
				var type = module.GetType ("Tamtam");
				var beta = type.GetMethod ("Beta");
				var charlie = type.GetMethod ("Charlie");

				// Note that the test depends on the C# compiler emitting the constructor call instruction as
				// the first instruction of the method body. This requires optimizations to be enabled.
				var new_list_beta = (MethodReference) beta.Body.Instructions [0].Operand;
				var new_list_charlie = (MethodReference) charlie.Body.Instructions [0].Operand;

				Assert.AreEqual ("System.Collections.Generic.List`1<TBeta>", new_list_beta.DeclaringType.FullName);
				Assert.AreEqual ("System.Collections.Generic.List`1<TCharlie>", new_list_charlie.DeclaringType.FullName);
			});
		}

		[Test]
		public void ReturnParameterMethod ()
		{
			var method = typeof (MethodTests).ToDefinition ().GetMethod ("ReturnParameterMethod");
			Assert.IsNotNull (method);
			Assert.AreEqual (method, method.MethodReturnType.Parameter.Method);
		}

		[Test]
		public void InstanceAndStaticMethodComparison ()
		{
			TestIL ("others.il", module => {
				var others = module.GetType ("Others");
				var instance_method = others.Methods.Single (m => m.Name == "SameMethodNameInstanceStatic" && m.HasThis);
				var static_method_reference = new MethodReference ("SameMethodNameInstanceStatic", instance_method.ReturnType, others)
					{
						HasThis = false
					};

				Assert.AreNotEqual(instance_method, static_method_reference.Resolve ());
			});
		}

		[Test]
		public void FunctionPointerArgumentOverload ()
		{
			TestIL ("others.il", module => {
				var others = module.GetType ("Others");
				var overloaded_methods = others.Methods.Where (m => m.Name == "OverloadedWithFpArg").ToArray ();
				// Manually create the function-pointer type so `AreSame` won't exit early due to reference equality
				var overloaded_method_int_reference = new MethodReference ("OverloadedWithFpArg", module.TypeSystem.Void, others) 
				{
					HasThis = false,
					Parameters = { new ParameterDefinition ("X", ParameterAttributes.None, new FunctionPointerType () {
						HasThis = false,
						ReturnType = module.TypeSystem.Int32,
						Parameters = { new ParameterDefinition (module.TypeSystem.Int32) }
					}) }
				};
				
				var overloaded_method_long_reference = new MethodReference ("OverloadedWithFpArg", module.TypeSystem.Void, others) 
				{
					HasThis = false,
					Parameters = { new ParameterDefinition ("X", ParameterAttributes.None, new FunctionPointerType () {
						HasThis = false,
						ReturnType = module.TypeSystem.Int32,
						Parameters = { new ParameterDefinition (module.TypeSystem.Int64) }
					}) }
				};
				
				var overloaded_method_cdecl_reference = new MethodReference ("OverloadedWithFpArg", module.TypeSystem.Void, others) 
				{
					HasThis = false,
					Parameters = { new ParameterDefinition ("X", ParameterAttributes.None, new FunctionPointerType () {
						CallingConvention = MethodCallingConvention.C,
						HasThis = false,
						ReturnType = module.TypeSystem.Int32,
						Parameters = { new ParameterDefinition (module.TypeSystem.Int32) }
					}) } 
				};
				

				Assert.AreEqual (overloaded_methods[0], overloaded_method_int_reference.Resolve ()); 
				Assert.AreEqual (overloaded_methods[1], overloaded_method_long_reference.Resolve ()); 
				Assert.AreEqual (overloaded_methods[2], overloaded_method_cdecl_reference.Resolve ()); 
			});
		}
		
		[Test]
		public void PrivateScope ()
		{
			TestIL ("privatescope.il", module => {
				var foo = module.GetType ("Foo");
				var call_same_name_methods = foo.GetMethod ("CallSameNameMethods");
				var call_instructions = call_same_name_methods.Body.Instructions
					.Where (ins => ins.OpCode.Code == Code.Call)
					.ToArray ();
				
				var first_same_name_index = 2;

				// The first method will be the normal non-privatescope method.
				var first_call_resolved = ((MethodReference)call_instructions [0].Operand).Resolve ();
				var expected_first_call_resolved = foo.Methods [first_same_name_index];
				Assert.IsFalse(first_call_resolved.IsCompilerControlled);
				Assert.AreEqual(expected_first_call_resolved, first_call_resolved);
				
				// This is the first privatescope method.
				var second_call_resolved = ((MethodReference)call_instructions [1].Operand).Resolve();
				var expected_second_call_resolved = foo.Methods [first_same_name_index + 1];
				
				// Sanity check to make sure the ordering assumptions were correct.
				Assert.IsTrue(expected_second_call_resolved.IsCompilerControlled, "The expected method should have been compiler controlled.");
				
				// The equality failure isn't going to be very helpful since both methods will have the same ToString value,
				// so before we assert equality, we'll assert that the method is compiler controlled because that is the key difference
				Assert.IsTrue(second_call_resolved.IsCompilerControlled, "Expected the method reference to resolve to a compiler controlled method");
				Assert.AreEqual(expected_second_call_resolved, second_call_resolved);
				
				// This is the second privatescope method.
				var third_call_resolved = ((MethodReference)call_instructions [2].Operand).Resolve ();
				var expected_third_call_resolved = foo.Methods [first_same_name_index + 2];
				
				// Sanity check to make sure the ordering assumptions were correct.
				Assert.IsTrue(expected_third_call_resolved.IsCompilerControlled, "The expected method should have been compiler controlled.");
				
				// The equality failure isn't going to be very helpful since both methods will have the same ToString value,
				// so before we assert equality, we'll assert that the method is compiler controlled because that is the key difference
				Assert.IsTrue(third_call_resolved.IsCompilerControlled, "Expected the method reference to resolve to a compiler controlled method");
				Assert.AreEqual(expected_third_call_resolved, third_call_resolved);
			});
		}
		
		[Test]
		public void PrivateScopeGeneric ()
		{
			TestIL ("privatescope.il", module => {
				var foo = module.GetType ("Foo");
				var call_same_name_methods = foo.GetMethod ("CallSameNameMethodsGeneric");
				var call_instructions = call_same_name_methods.Body.Instructions
					.Where (ins => ins.OpCode.Code == Code.Call)
					.ToArray ();

				var first_same_name_generic_index = 6;

				// The first method will be the normal non-privatescope method.
				var first_call_resolved = ((MethodReference)call_instructions [0].Operand).Resolve();
				var expected_first_call_resolved = foo.Methods [first_same_name_generic_index];
				Assert.IsFalse(first_call_resolved.IsCompilerControlled);
				Assert.AreEqual(expected_first_call_resolved, first_call_resolved);
				
				// This is the first privatescope method.
				var second_call_resolved = ((MethodReference)call_instructions [1].Operand).Resolve();
				var expected_second_call_resolved = foo.Methods [first_same_name_generic_index + 1];
				
				// Sanity check to make sure the ordering assumptions were correct.
				Assert.IsTrue(expected_second_call_resolved.IsCompilerControlled, "The expected method should have been compiler controlled.");
				
				// The equality failure isn't going to be very helpful since both methods will have the same ToString value,
				// so before we assert equality, we'll assert that the method is compiler controlled because that is the key difference
				Assert.IsTrue (second_call_resolved.IsCompilerControlled, "Expected the method reference to resolve to a compiler controlled method");
				Assert.AreEqual(expected_second_call_resolved, second_call_resolved);

				// This is the second privatescope method.
				var third_call_resolved = ((MethodReference)call_instructions [2].Operand).Resolve();
				var expected_third_call_resolved = foo.Methods [first_same_name_generic_index + 2];
				
				// Sanity check to make sure the ordering assumptions were correct.
				Assert.IsTrue(expected_third_call_resolved.IsCompilerControlled, "The expected method should have been compiler controlled.");
				
				// The equality failure isn't going to be very helpful since both methods will have the same ToString value,
				// so before we assert equality, we'll assert that the method is compiler controlled because that is the key difference
				Assert.IsTrue(third_call_resolved.IsCompilerControlled, "Expected the method reference to resolve to a compiler controlled method");
				Assert.AreEqual(expected_third_call_resolved, third_call_resolved);
			});
		}
	}
}
