//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

using System;

namespace Mono.Cecil.PE {

	class ByteBuffer {

		internal byte [] buffer;
		internal int length;
		internal int position;

		public ByteBuffer ()
		{
			this.buffer = Empty<byte>.Array;
		}

		public ByteBuffer (int length)
		{
			this.buffer = new byte [length];
		}

		public ByteBuffer (byte [] buffer)
		{
			this.buffer = buffer ?? Empty<byte>.Array;
			this.length = this.buffer.Length;
		}

		public void Advance (int length)
		{
			position += length;
		}

		public byte ReadByte ()
		{
			return buffer [position++];
		}

		public sbyte ReadSByte ()
		{
			return (sbyte) ReadByte ();
		}

		public byte [] ReadBytes (int length)
		{
			var bytes = new byte [length];
			Buffer.BlockCopy (buffer, position, bytes, 0, length);
			position += length;
			return bytes;
		}

		public ushort ReadUInt16 ()
		{
			ushort value = (ushort) (buffer [position]
				| (buffer [position + 1] << 8));
			position += 2;
			return value;
		}

		public short ReadInt16 ()
		{
			return (short) ReadUInt16 ();
		}

		public uint ReadUInt32 ()
		{
			uint value = (uint) (buffer [position]
				| (buffer [position + 1] << 8)
				| (buffer [position + 2] << 16)
				| (buffer [position + 3] << 24));
			position += 4;
			return value;
		}

		public int ReadInt32 ()
		{
			return (int) ReadUInt32 ();
		}

		public ulong ReadUInt64 ()
		{
			uint low = ReadUInt32 ();
			uint high = ReadUInt32 ();

			return (((ulong) high) << 32) | low;
		}

		public long ReadInt64 ()
		{
			return (long) ReadUInt64 ();
		}

		public uint ReadCompressedUInt32 ()
		{
			byte first = ReadByte ();
			if ((first & 0x80) == 0)
				return first;

			if ((first & 0x40) == 0)
				return ((uint) (first & ~0x80) << 8)
					| ReadByte ();

			return ((uint) (first & ~0xc0) << 24)
				| (uint) ReadByte () << 16
				| (uint) ReadByte () << 8
				| ReadByte ();
		}

		public int ReadCompressedInt32 ()
		{
			var b = buffer [position];
			var u = (int) ReadCompressedUInt32 ();
			var v = u >> 1;
			if ((u & 1) == 0)
				return v;

			switch (b & 0xc0)
			{
				case 0:
				case 0x40:
					return v - 0x40;
				case 0x80:
					return v - 0x2000;
				default:
					return v - 0x10000000;
			}
		}

		public float ReadSingle ()
		{
			if (!BitConverter.IsLittleEndian) {
				var bytes = ReadBytes (4);
				Array.Reverse (bytes);
				return BitConverter.ToSingle (bytes, 0);
			}

			float value = BitConverter.ToSingle (buffer, position);
			position += 4;
			return value;
		}

		public double ReadDouble ()
		{
			if (!BitConverter.IsLittleEndian) {
				var bytes = ReadBytes (8);
				Array.Reverse (bytes);
				return BitConverter.ToDouble (bytes, 0);
			}

			double value = BitConverter.ToDouble (buffer, position);
			position += 8;
			return value;
		}

		public void WriteByte (byte value)
		{
			if (position == buffer.Length)
				Grow (1);

			buffer [position++] = value;

			if (position > length)
				length = position;
		}

		public void WriteSByte (sbyte value)
		{
			WriteByte ((byte) value);
		}

		public void WriteUInt16 (ushort value)
		{
			if (position + 2 > buffer.Length)
				Grow (2);

			buffer [position++] = (byte) value;
			buffer [position++] = (byte) (value >> 8);

			if (position > length)
				length = position;
		}

		public void WriteInt16 (short value)
		{
			WriteUInt16 ((ushort) value);
		}

		public void WriteUInt32 (uint value)
		{
			if (position + 4 > buffer.Length)
				Grow (4);

			buffer [position++] = (byte) value;
			buffer [position++] = (byte) (value >> 8);
			buffer [position++] = (byte) (value >> 16);
			buffer [position++] = (byte) (value >> 24);

			if (position > length)
				length = position;
		}

		public void WriteInt32 (int value)
		{
			WriteUInt32 ((uint) value);
		}

		public void WriteUInt64 (ulong value)
		{
			if (position + 8 > buffer.Length)
				Grow (8);

			buffer [position++] = (byte) value;
			buffer [position++] = (byte) (value >> 8);
			buffer [position++] = (byte) (value >> 16);
			buffer [position++] = (byte) (value >> 24);
			buffer [position++] = (byte) (value >> 32);
			buffer [position++] = (byte) (value >> 40);
			buffer [position++] = (byte) (value >> 48);
			buffer [position++] = (byte) (value >> 56);

			if (position > length)
				length = position;
		}

		public void WriteInt64 (long value)
		{
			WriteUInt64 ((ulong) value);
		}

		public void WriteCompressedUInt32 (uint value)
		{
			if (value < 0x80)
				WriteByte ((byte) value);
			else if (value < 0x4000) {
				WriteByte ((byte) (0x80 | (value >> 8)));
				WriteByte ((byte) (value & 0xff));
			} else {
				WriteByte ((byte) ((value >> 24) | 0xc0));
				WriteByte ((byte) ((value >> 16) & 0xff));
				WriteByte ((byte) ((value >> 8) & 0xff));
				WriteByte ((byte) (value & 0xff));
			}
		}

		public void WriteCompressedInt32 (int value)
		{
			// extracted from System.Reflection.Metadata
			unchecked {
				const int b6 = (1 << 6) - 1;
				const int b13 = (1 << 13) - 1;
				const int b28 = (1 << 28) - 1;

				// 0xffffffff for negative value
				// 0x00000000 for non-negative

				int signMask = value >> 31;

				if ((value & ~b6) == (signMask & ~b6)) {
					int n = ((value & b6) << 1) | (signMask & 1);
					WriteByte ((byte)n);
				} else if ((value & ~b13) == (signMask & ~b13)) {
					int n = ((value & b13) << 1) | (signMask & 1);
					ushort val = (ushort)(0x8000 | n);
					WriteUInt16 (BitConverter.IsLittleEndian ? ReverseEndianness (val) : val);
				} else if ((value & ~b28) == (signMask & ~b28)) {
					int n = ((value & b28) << 1) | (signMask & 1);
					uint val = 0xc0000000 | (uint)n;
					WriteUInt32 (BitConverter.IsLittleEndian ? ReverseEndianness (val) : val);
				} else {
					throw new ArgumentOutOfRangeException ("value", "valid range is -2^28 to 2^28 -1");
				}
			}
		}

		static uint ReverseEndianness (uint value)
		{
			// extracted from .net BCL

			// This takes advantage of the fact that the JIT can detect
			// ROL32 / ROR32 patterns and output the correct intrinsic.
			//
			// Input: value = [ ww xx yy zz ]
			//
			// First line generates : [ ww xx yy zz ]
			//                      & [ 00 FF 00 FF ]
			//                      = [ 00 xx 00 zz ]
			//             ROR32(8) = [ zz 00 xx 00 ]
			//
			// Second line generates: [ ww xx yy zz ]
			//                      & [ FF 00 FF 00 ]
			//                      = [ ww 00 yy 00 ]
			//             ROL32(8) = [ 00 yy 00 ww ]
			//
			//                (sum) = [ zz yy xx ww ]
			//
			// Testing shows that throughput increases if the AND
			// is performed before the ROL / ROR.

			return RotateRight (value & 0x00FF00FFu, 8) // xx zz
				+ RotateLeft (value & 0xFF00FF00u, 8); // ww yy
		}

		// extracted from .net BCL
		static uint RotateRight (uint value, int offset)
			=> (value >> offset) | (value << (32 - offset));

		static uint RotateLeft (uint value, int offset)
			=> (value << offset) | (value >> (32 - offset));

		static ushort ReverseEndianness (ushort value)
		{
			// extracted from .net BCL

			// Don't need to AND with 0xFF00 or 0x00FF since the final
			// cast back to ushort will clear out all bits above [ 15 .. 00 ].
			// This is normally implemented via "movzx eax, ax" on the return.
			// Alternatively, the compiler could elide the movzx instruction
			// entirely if it knows the caller is only going to access "ax"
			// instead of "eax" / "rax" when the function returns.

			return (ushort)((value >> 8) + (value << 8));
		}

		public void WriteBytes (byte [] bytes)
		{
			var length = bytes.Length;
			if (position + length > buffer.Length)
				Grow (length);

			Buffer.BlockCopy (bytes, 0, buffer, position, length);
			position += length;

			if (position > this.length)
				this.length = position;
		}

		public void WriteBytes (int length)
		{
			if (position + length > buffer.Length)
				Grow (length);

			position += length;

			if (position > this.length)
				this.length = position;
		}

		public void WriteBytes (ByteBuffer buffer)
		{
			if (position + buffer.length > this.buffer.Length)
				Grow (buffer.length);

			Buffer.BlockCopy (buffer.buffer, 0, this.buffer, position, buffer.length);
			position += buffer.length;

			if (position > this.length)
				this.length = position;
		}

		public void WriteSingle (float value)
		{
			var bytes = BitConverter.GetBytes (value);

			if (!BitConverter.IsLittleEndian)
				Array.Reverse (bytes);

			WriteBytes (bytes);
		}

		public void WriteDouble (double value)
		{
			var bytes = BitConverter.GetBytes (value);

			if (!BitConverter.IsLittleEndian)
				Array.Reverse (bytes);

			WriteBytes (bytes);
		}

		void Grow (int desired)
		{
			var current = this.buffer;
			var current_length = current.Length;

			var buffer = new byte [System.Math.Max (current_length + desired, current_length * 2)];
			Buffer.BlockCopy (current, 0, buffer, 0, current_length);
			this.buffer = buffer;
		}
	}
}
