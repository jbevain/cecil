//
// ILProcessor.cs
//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2010 Jb Evain
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;

using Mono.Collections.Generic;

namespace Mono.Cecil.Cil {

	public sealed class ILProcessor {

		readonly MethodBody body;
		readonly Collection<Instruction> instructions;

		internal ILProcessor (MethodBody body)
		{
			this.body = body;
			this.instructions = body.Instructions;
		}

		public Instruction Create (OpCode opcode)
		{
			return Instruction.Create (opcode);
		}

		public Instruction Create (OpCode opcode, TypeReference type)
		{
			return Instruction.Create (opcode, type);
		}

		public Instruction Create (OpCode opcode, CallSite site)
		{
			return Instruction.Create (opcode, site);
		}

		public Instruction Create (OpCode opcode, MethodReference method)
		{
			return Instruction.Create (opcode, method);
		}

		public Instruction Create (OpCode opcode, FieldReference field)
		{
			return Instruction.Create (opcode, field);
		}

		public Instruction Create (OpCode opcode, string value)
		{
			return Instruction.Create (opcode, value);
		}

		public Instruction Create (OpCode opcode, sbyte value)
		{
			return Instruction.Create (opcode, value);
		}

		public Instruction Create (OpCode opcode, byte value)
		{
			if (opcode.OperandType == OperandType.ShortInlineVar)
				return Instruction.Create (opcode, body.Variables [value]);

			if (opcode.OperandType == OperandType.ShortInlineArg)
				return Instruction.Create (opcode, body.GetParameter (value));

			return Instruction.Create (opcode, value);
		}

		public Instruction Create (OpCode opcode, int value)
		{
			if (opcode.OperandType == OperandType.InlineVar)
				return Instruction.Create (opcode, body.Variables [value]);

			if (opcode.OperandType == OperandType.InlineArg)
				return Instruction.Create (opcode, body.GetParameter (value));

			return Instruction.Create (opcode, value);
		}

		public Instruction Create (OpCode opcode, long value)
		{
			return Instruction.Create (opcode, value);
		}

		public Instruction Create (OpCode opcode, float value)
		{
			return Instruction.Create (opcode, value);
		}

		public Instruction Create (OpCode opcode, double value)
		{
			return Instruction.Create (opcode, value);
		}

		public Instruction Create (OpCode opcode, Instruction target)
		{
			return Instruction.Create (opcode, target);
		}

		public Instruction Create (OpCode opcode, Instruction [] targets)
		{
			return Instruction.Create (opcode, targets);
		}

		public Instruction Create (OpCode opcode, VariableDefinition variable)
		{
			return Instruction.Create (opcode, variable);
		}

		public Instruction Create (OpCode opcode, ParameterDefinition parameter)
		{
			return Instruction.Create (opcode, parameter);
		}

		public Instruction Emit (OpCode opcode)
		{
			var instruction = Create (opcode);
			Append (instruction);
			return instruction;
		}

		public Instruction Emit (OpCode opcode, TypeReference type)
		{
			var instruction = Create (opcode, type);
			Append (instruction);
			return instruction;
		}

		public Instruction Emit (OpCode opcode, MethodReference method)
		{
			var instruction = Create (opcode, method);
			Append (instruction);
			return instruction;
		}

		public Instruction Emit (OpCode opcode, CallSite site)
		{
			var instruction = Create (opcode, site);
			Append (instruction);
			return instruction;
		}

		public Instruction Emit (OpCode opcode, FieldReference field)
		{
			var instruction = Create (opcode, field);
			Append (instruction);
			return instruction;
		}

		public Instruction Emit (OpCode opcode, string value)
		{
			var instruction = Create (opcode, value);
			Append (instruction);
			return instruction;
		}

		public Instruction Emit (OpCode opcode, byte value)
		{
			var instruction = Create (opcode, value);
			Append (instruction);
			return instruction;
		}

		public Instruction Emit (OpCode opcode, sbyte value)
		{
			var instruction = Create (opcode, value);
			Append (instruction);
			return instruction;
		}

		public Instruction Emit (OpCode opcode, int value)
		{
			var instruction = Create (opcode, value);
			Append (instruction);
			return instruction;
		}

		public Instruction Emit (OpCode opcode, long value)
		{
			var instruction = Create (opcode, value);
			Append (instruction);
			return instruction;
		}

		public Instruction Emit (OpCode opcode, float value)
		{
			var instruction = Create (opcode, value);
			Append (instruction);
			return instruction;
		}

		public Instruction Emit (OpCode opcode, double value)
		{
			var instruction = Create (opcode, value);
			Append (instruction);
			return instruction;
		}

		public Instruction Emit (OpCode opcode, Instruction target)
		{
			var instruction = Create (opcode, target);
			Append (instruction);
			return instruction;
		}

		public Instruction Emit (OpCode opcode, Instruction [] targets)
		{
			var instruction = Create (opcode, targets);
			Append (instruction);
			return instruction;
		}

		public Instruction Emit (OpCode opcode, VariableDefinition variable)
		{
			var instruction = Create (opcode, variable);
			Append (instruction);
			return instruction;
		}

		public Instruction Emit (OpCode opcode, ParameterDefinition parameter)
		{
			var instruction = Create (opcode, parameter);
			Append (instruction);
			return instruction;
		}

		public void InsertBefore (Instruction target, Instruction instruction)
		{
			var index = instructions.IndexOf (target);
			if (index == -1)
				throw new ArgumentOutOfRangeException ("target");

			instructions.Insert (index, instruction);
		}

		public void InsertAfter (Instruction target, Instruction instruction)
		{
			var index = instructions.IndexOf (target);
			if (index == -1)
				throw new ArgumentOutOfRangeException ("target");

			instructions.Insert (index + 1, instruction);
		}

		public void Append (Instruction instruction)
		{
			instructions.Add (instruction);
		}

		public void Replace (Instruction target, Instruction instruction)
		{
			InsertAfter (target, instruction);
			Remove (target);
		}

		public void Remove (Instruction instruction)
		{
			if (!instructions.Remove (instruction))
				throw new ArgumentOutOfRangeException ("instruction");
		}
	}
}
