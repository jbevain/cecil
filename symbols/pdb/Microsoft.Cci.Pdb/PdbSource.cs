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
  public class PdbSource {
    //internal uint index;
    public string name;
    public Guid doctype;
    public Guid language;
    public Guid vendor;

    internal PdbSource(/*uint index, */string name, Guid doctype, Guid language, Guid vendor) {
      //this.index = index;
      this.name = name;
      this.doctype = doctype;
      this.language = language;
      this.vendor = vendor;
    }
  }
}
