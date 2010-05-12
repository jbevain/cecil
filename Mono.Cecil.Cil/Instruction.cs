//
// Instruction.cs
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

using System.Text;

namespace Mono.Cecil.Cil {

	public sealed class Instruction {

		internal int offset;
		internal OpCode opcode;
		internal object operand;

		internal Instruction previous;
		internal Instruction next;

		SequencePoint sequence_point;

		public int Offset {
			get { return offset; }
			set { offset = value; }
		}

		public OpCode OpCode {
			get { return opcode; }
			set { opcode = value; }
		}

		public object Operand {
			get { return operand; }
			set { operand = value; }
		}

		public Instruction Previous {
			get { return previous; }
			set { previous = value; }
		}

		public Instruction Next {
			get { return next; }
			set { next = value; }
		}

		public SequencePoint SequencePoint {
			get { return sequence_point; }
			set { sequence_point = value; }
		}

		internal Instruction (int offset, OpCode opCode)
		{
			this.offset = offset;
			this.opcode = opCode;
		}

		internal Instruction (OpCode opcode, object operand)
		{
			this.opcode = opcode;
			this.operand = operand;
		}

		public int GetSize ()
		{
			int size = opcode.Size;

			switch (opcode.OperandType) {
			case OperandType.InlineSwitch:
				size += (1 + ((Instruction []) operand).Length) * 4;
				break;
			case OperandType.InlineI8:
			case OperandType.InlineR:
				size += 8;
				break;
			case OperandType.InlineBrTarget:
			case OperandType.InlineField:
			case OperandType.InlineI:
			case OperandType.InlineMethod:
			case OperandType.InlineString:
			case OperandType.InlineTok:
			case OperandType.InlineType:
			case OperandType.ShortInlineR:
			case OperandType.InlineSig:
				size += 4;
				break;
			case OperandType.InlineArg:
			case OperandType.InlineVar:
				size += 2;
				break;
			case OperandType.ShortInlineBrTarget:
			case OperandType.ShortInlineI:
			case OperandType.ShortInlineArg:
			case OperandType.ShortInlineVar:
				size += 1;
				break;
			}

			return size;
		}

		public override string ToString ()
		{
			var instruction = new StringBuilder ();

			AppendLabel (instruction, this);
			instruction.Append (':');
			instruction.Append (' ');
			instruction.Append (opcode.Name);

			if (operand == null)
				return instruction.ToString ();

			instruction.Append (' ');

			switch (opcode.OperandType) {
			case OperandType.ShortInlineBrTarget:
			case OperandType.InlineBrTarget:
				AppendLabel (instruction, (Instruction) operand);
				break;
			case OperandType.InlineSwitch:
				var labels = (Instruction []) operand;
				for (int i = 0; i < labels.Length; i++) {
					if (i > 0)
						instruction.Append (',');

					AppendLabel (instruction, labels [i]);
				}
				break;
			case OperandType.InlineString:
				instruction.Append ('\"');
				instruction.Append (operand);
				instruction.Append ('\"');
				break;
			default:
				instruction.Append (operand);
				break;
			}

			return instruction.ToString ();
		}

		static void AppendLabel (StringBuilder builder, Instruction instruction)
		{
			builder.Append ("IL_");
			builder.Append (instruction.offset.ToString ("x4"));
		}
	}
}
