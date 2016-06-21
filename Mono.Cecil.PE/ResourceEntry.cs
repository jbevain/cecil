namespace Mono.Cecil.PE
{
	public class ResourceEntry
	{
		public uint Id { get; set; }
		public string Name { get; set; }
		public ResourceDirectory Directory { get; set; }
		public uint CodePage { get; set; }
		public uint Reserved { get; set; }
		public byte[] Data { get; set; }
	}
}