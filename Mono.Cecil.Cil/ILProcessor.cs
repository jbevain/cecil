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
			if (opcode.OperandType != OperandType.InlineNone)
				throw new ArgumentException ("opcode");

			return FinalCreate (opcode);
		}

		public Instruction Create (OpCode opcode, TypeReference type)
		{
			if (type == null)
				throw new ArgumentNullException ("type");
			if (opcode.OperandType != OperandType.InlineType &&
				opcode.OperandType != OperandType.InlineTok)
				throw new ArgumentException ("opcode");

			return FinalCreate (opcode, type);
		}

		public Instruction Create (OpCode opcode, CallSite site)
		{
			if (site == null)
				throw new ArgumentNullException ("site");
			if (opcode.Code != Code.Calli)
				throw new ArgumentException ("code");

			return FinalCreate (opcode, site);
		}

		public Instruction Create (OpCode opcode, MethodReference method)
		{
			if (method == null)
				throw new ArgumentNullException ("method");
			if (opcode.OperandType != OperandType.InlineMethod &&
				opcode.OperandType != OperandType.InlineTok)
				throw new ArgumentException ("opcode");

			return FinalCreate (opcode, method);
		}

		public Instruction Create (OpCode opcode, FieldReference field)
		{
			if (field == null)
				throw new ArgumentNullException ("field");
			if (opcode.OperandType != OperandType.InlineField &&
				opcode.OperandType != OperandType.InlineTok)
				throw new ArgumentException ("opcode");

			return FinalCreate (opcode, field);
		}

		public Instruction Create (OpCode opcode, string value)
		{
			if (value == null)
				throw new ArgumentNullException ("value");
			if (opcode.OperandType != OperandType.InlineString)
				throw new ArgumentException ("opcode");

			return FinalCreate (opcode, value);
		}

		public Instruction Create (OpCode opcode, sbyte value)
		{
			if (opcode.OperandType != OperandType.ShortInlineI &&
				opcode != OpCodes.Ldc_I4_S)
				throw new ArgumentException ("opcode");

			return FinalCreate (opcode, value);
		}

		public Instruction Create (OpCode opcode, byte value)
		{
			if (opcode.OperandType == OperandType.ShortInlineVar)
				return Create (opcode, body.Variables [value]);

			if (opcode.OperandType == OperandType.ShortInlineArg)
				return Create (opcode, body.GetParameter (value));

			if (opcode.OperandType != OperandType.ShortInlineI ||
				opcode == OpCodes.Ldc_I4_S)
				throw new ArgumentException ("opcode");

			return FinalCreate (opcode, value);
		}

		public Instruction Create (OpCode opcode, int value)
		{
			if (opcode.OperandType == OperandType.InlineVar)
				return Create (opcode, body.Variables [value]);

			if (opcode.OperandType == OperandType.InlineArg)
				return Create (opcode, body.GetParameter (value));

			if (opcode.OperandType != OperandType.InlineI)
				throw new ArgumentException ("opcode");

			return FinalCreate (opcode, value);
		}

		public Instruction Create (OpCode opcode, long value)
		{
			if (opcode.OperandType != OperandType.InlineI8)
				throw new ArgumentException ("opcode");

			return FinalCreate (opcode, value);
		}

		public Instruction Create (OpCode opcode, float value)
		{
			if (opcode.OperandType != OperandType.ShortInlineR)
				throw new ArgumentException ("opcode");

			return FinalCreate (opcode, value);
		}

		public Instruction Create (OpCode opcode, double value)
		{
			if (opcode.OperandType != OperandType.InlineR)
				throw new ArgumentException ("opcode");

			return FinalCreate (opcode, value);
		}

		public Instruction Create (OpCode opcode, Instruction label)
		{
			if (label == null)
				throw new ArgumentNullException ("label");
			if (opcode.OperandType != OperandType.InlineBrTarget &&
				opcode.OperandType != OperandType.ShortInlineBrTarget)
				throw new ArgumentException ("opcode");

			return FinalCreate (opcode, label);
		}

		public Instruction Create (OpCode opcode, Instruction [] targets)
		{
			if (targets == null)
				throw new ArgumentNullException ("targets");
			if (opcode.OperandType != OperandType.InlineSwitch)
				throw new ArgumentException ("opcode");

			return FinalCreate (opcode, targets);
		}

		public Instruction Create (OpCode opcode, VariableDefinition variable)
		{
			if (variable == null)
				throw new ArgumentNullException ("variable");
			if (opcode.OperandType != OperandType.ShortInlineVar &&
				opcode.OperandType != OperandType.InlineVar)
				throw new ArgumentException ("opcode");

			return FinalCreate (opcode, variable);
		}

		public Instruction Create (OpCode opcode, ParameterDefinition parameter)
		{
			if (parameter == null)
				throw new ArgumentNullException ("parameter");
			if (opcode.OperandType != OperandType.ShortInlineArg &&
				opcode.OperandType != OperandType.InlineArg)
				throw new ArgumentException ("opcode");

			return FinalCreate (opcode, parameter);
		}

		static Instruction FinalCreate (OpCode opcode)
		{
			return FinalCreate (opcode, null);
		}

		static Instruction FinalCreate (OpCode opcode, object operand)
		{
			return new Instruction (opcode, operand);
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
			instruction.Previous = target.Previous;
			if (target.Previous != null)
				target.Previous.Next = instruction;
			target.Previous = instruction;
			instruction.Next = target;
		}

		public void InsertAfter (Instruction target, Instruction instruction)
		{
			var index = instructions.IndexOf (target);
			if (index == -1)
				throw new ArgumentOutOfRangeException ("target");

			instructions.Insert (index + 1, instruction);
			instruction.Next = target.Next;
			if (target.Next != null)
				target.Next.Previous = instruction;
			target.Next = instruction;
			instruction.Previous = target;
		}

		public void Append (Instruction instruction)
		{
			Instruction last = null, current = instruction;
			if (instructions.Count > 0)
				last = instructions [instructions.Count - 1];

			if (last != null) {
				last.Next = instruction;
				current.Previous = last;
			}

			instructions.Add (current);
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

			if (instruction.Previous != null)
				instruction.Previous.Next = instruction.Next;
			if (instruction.Next != null)
				instruction.Next.Previous = instruction.Previous;

		}
	}
}
