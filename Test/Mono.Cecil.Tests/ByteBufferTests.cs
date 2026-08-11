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

		// Round-trips WriteCompressedInt32/ReadCompressedInt32 across every encoding-width
		// boundary of the ECMA-335 compressed signed integer format (1/2/4-byte forms)
		[TestCase (0)]
		[TestCase (1)]
		[TestCase (-1)]
		[TestCase (63)]
		[TestCase (-64)]
		[TestCase (64)]
		[TestCase (-65)]
		[TestCase (8191)]
		[TestCase (-8192)]
		[TestCase (8192)]
		[TestCase (-8193)]
		[TestCase (-8269)]
		[TestCase (268435455)]
		[TestCase (-268435456)]
		public void CompressedInt32RoundTripsAcrossEncodingWidths (int value)
		{
			var testee = new ByteBuffer ();
			testee.WriteCompressedInt32 (value);
			testee.position = 0;
			var result = testee.ReadCompressedInt32 ();
			Assert.AreEqual (value, result);
		}
	}
}
