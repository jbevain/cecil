using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Mono.Cecil.Tests {
	[TestFixture]
	public class ImageWriteTests : BaseTestFixture {

		[TestCase("EmbeddedPdbTarget.exe")]
		// [TestCase("ExternalPdbDeterministic.dll")]
		public void MultipleWriteOperationsShouldReturnSameOutput (string testee)
		{
			TestModule (testee, module => {
				Assert.IsTrue (module.HasDebugHeader);

				const int numberOfStreams = 5;

				MemoryStream Write(int _)
				{
					var stream = new MemoryStream ();
					module.Write (stream, new WriterParameters { WriteSymbols = true });
					return stream;
				}

				var streams = Enumerable.Range (0, numberOfStreams)
					.Select (Write)
					.ToArray ();

				static bool AreStreamsEqual ((MemoryStream First, MemoryStream Second) pair)
				{
					var buffer1 = pair.First.GetBuffer ();
					var buffer2 = pair.Second.GetBuffer ();
					return buffer1.SequenceEqual (buffer2);
				}

				var results = streams.Zip (streams.Skip (1))
					.Select (AreStreamsEqual)
					.ToArray ();

				Assert.That (results, Is.EquivalentTo (results.Select (_ => true)));
			});
		}
	}
}