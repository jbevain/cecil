using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Mdb;
using Mono.Cecil.Pdb;
using NUnit.Framework;

namespace Mono.Cecil.Tests {

	[TestFixture]
	public class MiscellaneousTests : BaseTestFixture {
		[Test]
		public void DetachedLocalConstant ()
		{
			var parameters = new ReaderParameters { SymbolReaderProvider = new PdbReaderProvider () };
			using (var module = GetResourceModule ("NullConst.dll", parameters)) {
				// big test -- we can write w/o crashing
				var unique = Guid.NewGuid ().ToString ();
				var output = Path.GetTempFileName ();
				var outputdll = output + ".dll";

				using (var sink = File.Open (outputdll, FileMode.Create, FileAccess.ReadWrite)) {
					module.Write (sink, new WriterParameters () {
						SymbolWriterProvider = new EmbeddedPortablePdbWriterProvider (),
						WriteSymbols = true
					});
				}

				Assert.Pass ();
			}
		}
	}
}