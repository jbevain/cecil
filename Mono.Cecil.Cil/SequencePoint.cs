//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

namespace Mono.Cecil.Cil {

	public sealed class SequencePoint {

		Document document;

		int start_line;
		int start_column;
		int end_line;
		int end_column;

		public int StartLine {
			get { return start_line; }
			set { start_line = value; }
		}

		public int StartColumn {
			get { return start_column; }
			set { start_column = value; }
		}

		public int EndLine {
			get { return end_line; }
			set { end_line = value; }
		}

		public int EndColumn {
			get { return end_column; }
			set { end_column = value; }
		}

		public Document Document {
			get { return document; }
			set { document = value; }
		}

		public SequencePoint (Document document)
		{
			this.document = document;
		}
	}
}
