//-----------------------------------------------------------------------------
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the Microsoft Public License.
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//-----------------------------------------------------------------------------
using System;

namespace Microsoft.Cci.Pdb {
  public struct PdbLine {
    public uint offset;
    public uint lineBegin;
    public uint lineEnd;
    public ushort colBegin;
    public ushort colEnd;

    internal PdbLine(uint offset, uint lineBegin, ushort colBegin, uint lineEnd, ushort colEnd) {
      this.offset = offset;
      this.lineBegin = lineBegin;
      this.colBegin = colBegin;
      this.lineEnd = lineEnd;
      this.colEnd = colEnd;
    }
  }
}
