using System.IO;
using System.Text;

namespace Mono.Cecil.PE
{
	internal class RsrcReader
	{
		internal static ResourceDirectory ReadResourceDirectory(byte[] b, uint baseAddress)
		{
			var dr = new MemoryStream(b);
			return ReadResourceDirectory(new BinaryReader(dr), baseAddress);
		}

		private static ResourceDirectory ReadResourceDirectory(BinaryReader dr, uint baseAddress)
		{
			var d = ReadResourceDirectoryTable(dr);
			int ne = d.NumNameEntries + d.NumIdEntries;
			for (int i = 0; i < ne; i++)
				d.Entries.Add(ReadResourceEntry(dr, baseAddress));
			return d;
		}

		private static ResourceEntry ReadResourceEntry(BinaryReader dr, uint baseAddress)
		{
			var re = new ResourceEntry();
			uint id = dr.ReadUInt32();
			uint offset = dr.ReadUInt32();
			long pos = dr.BaseStream.Position;
			if ((id & 0x80000000) != 0)
			{
				dr.BaseStream.Position = (id & 0x7fffffff);
				var b = new StringBuilder();
				int c;
				while ((c = dr.Read()) > 0)
					b.Append((char) c);
				re.Name = b.ToString();
			}
			else
			{
				re.Id = id;
			}
			if ((offset & 0x80000000) != 0)
			{
				dr.BaseStream.Position = (offset & 0x7fffffff);
				re.Directory = ReadResourceDirectory(dr, baseAddress);
			}
			else
			{
				dr.BaseStream.Position = offset;
				uint rva = dr.ReadUInt32();
				uint size = dr.ReadUInt32();
				uint cp = dr.ReadUInt32();
				uint res = dr.ReadUInt32();
				re.CodePage = cp;
				re.Reserved = res;
				dr.BaseStream.Position = (rva - baseAddress);
				re.Data = dr.ReadBytes((int)size);
			}
			dr.BaseStream.Position = pos;
			return re;
		}

		private static ResourceDirectory ReadResourceDirectoryTable(BinaryReader dr)
		{
			var t = new ResourceDirectory
			{
				Characteristics = dr.ReadUInt32(),
				TimeDateStamp = dr.ReadUInt32(),
				MajorVersion = dr.ReadUInt16(),
				MinVersion = dr.ReadUInt16(),
				NumNameEntries = dr.ReadUInt16(),
				NumIdEntries = dr.ReadUInt16()
			};
			return t;
		}
	}
}
