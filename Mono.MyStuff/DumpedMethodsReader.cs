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
using System.IO;
using System.Collections.Generic;

namespace Mono.MyStuff {
    public class DumpedMethodsReader {
		private BinaryReader reader;

		const uint DUMPED_METHODS_HEADER_MAGIC = 0x12345678;

		public DumpedMethodsReader (BinaryReader reader) {
		    this.reader = reader;
		}

		public Dictionary<uint, DumpedMethod> read () {
		    var dict = new Dictionary<uint, DumpedMethod>();

			uint magic = reader.ReadUInt32 ();
			if (magic != DUMPED_METHODS_HEADER_MAGIC)
				throw new ApplicationException ("Invalid .methods file magic");

		    uint numMethods = reader.ReadUInt32 ();
		    for (uint i = 0; i < numMethods; i++) {
				var dm = readDumpedMethod ();
				dict.Add (dm.token, dm);
		    }

		    return dict;
		}

		private DumpedMethod readDumpedMethod () {
		    var dm = new DumpedMethod ();

		    dm.mhFlags = reader.ReadUInt16 ();
		    dm.mhMaxStack = reader.ReadUInt16 ();
		    dm.mhCodeSize = reader.ReadUInt32 ();
		    dm.mhLocalVarSigTok = reader.ReadUInt32 ();

		    dm.mdImplFlags = reader.ReadUInt16 ();
		    dm.mdFlags = reader.ReadUInt16 ();
		    dm.mdName = reader.ReadUInt32 ();
		    dm.mdSignature = reader.ReadUInt32 ();
		    dm.mdParamList = reader.ReadUInt32 ();

		    dm.token = reader.ReadUInt32 ();

		    dm.code = reader.ReadBytes ((int) dm.mhCodeSize);

		    return dm;
		}
    }
}
