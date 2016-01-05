//
// Author:
//   Virgile Bello (virgile.bello@gmail.com)
//
// Copyright (c) 2016 - 2016 Virgile Bello
//
// Licensed under the MIT/X11 license.
//

using Mono.Cecil.Cil;

namespace Mono.Cecil.Pdb {
	public class PdbIteratorScope {
		public Instruction Start { get; private set; }
		public Instruction End { get; private set; }

		public PdbIteratorScope (Instruction start, Instruction end)
		{
			Start = start;
			End = end;
		}
	}
}