/*
    Copyright (C) 2011-2012 de4dot@gmail.com

    This file is part of de4dot.

    de4dot is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    de4dot is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with de4dot.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;

namespace Mono.MyStuff {
	public class DumpedMethod {
		public UInt16 mhFlags;			// method header Flags
		public UInt16 mhMaxStack;		// method header MaxStack
		public UInt32 mhCodeSize;		// method header CodeSize
		public UInt32 mhLocalVarSigTok; // method header LocalVarSigTok

		public UInt16 mdImplFlags;		// methodDef ImplFlags
		public UInt16 mdFlags;			// methodDef Flags
		public UInt32 mdName;			// methodDef Name (index into #String)
		public UInt32 mdSignature;		// methodDef Signature (index into #Blob)
		public UInt32 mdParamList;		// methodDef ParamList (index into Param table)

		public UInt32 token;			// metadata token

		public byte[] code;
		public byte[] extraSections;
	}
}
