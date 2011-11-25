using Mono.Cecil.PE;

namespace Mono.Cecil.Metadata {
	public sealed class MetadataStream : Heap {

		public string Name;

		public MetadataStream (Section section, uint start, uint size, string name)
			: base (section, start, size) {
			this.Name = name;
		}
	}
}
