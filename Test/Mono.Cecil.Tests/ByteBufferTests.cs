using Mono.Cecil.PE;
using NUnit.Framework;

namespace Mono.Cecil.Tests {
	public class ByteBufferTests {
		[Test]
		public void TestLargeIntegerCompressed ()
		{
			var testee = new ByteBuffer ();
			testee.WriteCompressedInt32 (-9076);
			testee.position = 0;
			var result = testee.ReadCompressedInt32 ();
			Assert.AreEqual (-9076, result);
		}
	}
}
