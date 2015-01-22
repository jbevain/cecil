//
// ByteBuffer.cs
//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2011 Jb Evain
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;

namespace Mono.Cecil.PE {
    public interface IByteBuffer {
        void Reset (byte [] buffer);
        void Advance (int length);
        byte ReadByte ();
        sbyte ReadSByte ();
        byte [] ReadBytes (int length);
        ushort ReadUInt16 ();
        short ReadInt16 ();
        uint ReadUInt32 ();
        int ReadInt32 ();
        ulong ReadUInt64 ();
        long ReadInt64 ();
        uint ReadCompressedUInt32 ();
        int ReadCompressedInt32 ();
        float ReadSingle ();
        double ReadDouble ();
        void WriteByte (byte value);
        void WriteSByte (sbyte value);
        void WriteUInt16 (ushort value);
        void WriteInt16 (short value);
        void WriteUInt32 (uint value);
        void WriteInt32 (int value);
        void WriteUInt64 (ulong value);
        void WriteInt64 (long value);
        void WriteCompressedUInt32 (uint value);
        void WriteCompressedInt32 (int value);
        void WriteBytes (byte [] bytes);
        void WriteBytes (int length);
        void WriteBytes (IByteBuffer buffer);
        void WriteSingle (float value);
        void WriteDouble (double value);
        int Length { get; set; }
        int Position { get; set; }
        byte[] Buffer { get; set; }
    }

    public class ByteBuffer : IByteBuffer {

        public byte[] Buffer { get; set; }

        public int Position { get; set; }

        public int Length { get; set; }

		public ByteBuffer ()
		{
			this.Buffer = Empty<byte>.Array;
		}

		public ByteBuffer (int length)
		{
			this.Buffer = new byte [length];
		}

		public ByteBuffer (byte [] buffer)
		{
			this.Buffer = buffer ?? Empty<byte>.Array;
			this.Length = this.Buffer.Length;
		}

		public void Reset (byte [] buffer)
		{
			this.Buffer = buffer ?? Empty<byte>.Array;
			this.Length = this.Buffer.Length;
		}

		public void Advance (int length)
		{
			Position += length;
		}

		public byte ReadByte ()
		{
			return Buffer [Position++];
		}

		public sbyte ReadSByte ()
		{
			return (sbyte) ReadByte ();
		}

		public byte [] ReadBytes (int length)
		{
			var bytes = new byte [length];
			System.Buffer.BlockCopy (Buffer, Position, bytes, 0, length);
			Position += length;
			return bytes;
		}

		public ushort ReadUInt16 ()
		{
			ushort value = (ushort) (Buffer [Position]
				| (Buffer [Position + 1] << 8));
			Position += 2;
			return value;
		}

		public short ReadInt16 ()
		{
			return (short) ReadUInt16 ();
		}

		public uint ReadUInt32 ()
		{
			uint value = (uint) (Buffer [Position]
				| (Buffer [Position + 1] << 8)
				| (Buffer [Position + 2] << 16)
				| (Buffer [Position + 3] << 24));
			Position += 4;
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
			var value = (int) (ReadCompressedUInt32 () >> 1);
			if ((value & 1) == 0)
				return value;
			if (value < 0x40)
				return value - 0x40;
			if (value < 0x2000)
				return value - 0x2000;
			if (value < 0x10000000)
				return value - 0x10000000;
			return value - 0x20000000;
		}

		public float ReadSingle ()
		{
			if (!BitConverter.IsLittleEndian) {
				var bytes = ReadBytes (4);
				Array.Reverse (bytes);
				return BitConverter.ToSingle (bytes, 0);
			}

			float value = BitConverter.ToSingle (Buffer, Position);
			Position += 4;
			return value;
		}

		public double ReadDouble ()
		{
			if (!BitConverter.IsLittleEndian) {
				var bytes = ReadBytes (8);
				Array.Reverse (bytes);
				return BitConverter.ToDouble (bytes, 0);
			}

			double value = BitConverter.ToDouble (Buffer, Position);
			Position += 8;
			return value;
		}

#if !READ_ONLY

		public void WriteByte (byte value)
		{
			if (Position == Buffer.Length)
				Grow (1);

			Buffer [Position++] = value;

			if (Position > Length)
				Length = Position;
		}

		public void WriteSByte (sbyte value)
		{
			WriteByte ((byte) value);
		}

		public void WriteUInt16 (ushort value)
		{
			if (Position + 2 > Buffer.Length)
				Grow (2);

			Buffer [Position++] = (byte) value;
			Buffer [Position++] = (byte) (value >> 8);

			if (Position > Length)
				Length = Position;
		}

		public void WriteInt16 (short value)
		{
			WriteUInt16 ((ushort) value);
		}

		public void WriteUInt32 (uint value)
		{
			if (Position + 4 > Buffer.Length)
				Grow (4);

			Buffer [Position++] = (byte) value;
			Buffer [Position++] = (byte) (value >> 8);
			Buffer [Position++] = (byte) (value >> 16);
			Buffer [Position++] = (byte) (value >> 24);

			if (Position > Length)
				Length = Position;
		}

		public void WriteInt32 (int value)
		{
			WriteUInt32 ((uint) value);
		}

		public void WriteUInt64 (ulong value)
		{
			if (Position + 8 > Buffer.Length)
				Grow (8);

			Buffer [Position++] = (byte) value;
			Buffer [Position++] = (byte) (value >> 8);
			Buffer [Position++] = (byte) (value >> 16);
			Buffer [Position++] = (byte) (value >> 24);
			Buffer [Position++] = (byte) (value >> 32);
			Buffer [Position++] = (byte) (value >> 40);
			Buffer [Position++] = (byte) (value >> 48);
			Buffer [Position++] = (byte) (value >> 56);

			if (Position > Length)
				Length = Position;
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
			if (value >= 0) {
				WriteCompressedUInt32 ((uint) (value << 1));
				return;
			}

			if (value > -0x40)
				value = 0x40 + value;
			else if (value >= -0x2000)
				value = 0x2000 + value;
			else if (value >= -0x20000000)
				value = 0x20000000 + value;

			WriteCompressedUInt32 ((uint) ((value << 1) | 1));
		}

		public void WriteBytes (byte [] bytes)
		{
			var length = bytes.Length;
			if (Position + length > Buffer.Length)
				Grow (length);

			System.Buffer.BlockCopy (bytes, 0, Buffer, Position, length);
			Position += length;

			if (Position > this.Length)
				this.Length = Position;
		}

		public void WriteBytes (int length)
		{
			if (Position + length > Buffer.Length)
				Grow (length);

			Position += length;

			if (Position > this.Length)
				this.Length = Position;
		}

        public void WriteBytes(IByteBuffer buffer)
		{
			if (Position + buffer.Length > this.Buffer.Length)
				Grow (buffer.Length);

			System.Buffer.BlockCopy (buffer.Buffer, 0, this.Buffer, Position, buffer.Length);
			Position += buffer.Length;

			if (Position > this.Length)
				this.Length = Position;
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
			var current = this.Buffer;
			var current_length = current.Length;

			var buffer = new byte [System.Math.Max (current_length + desired, current_length * 2)];
			System.Buffer.BlockCopy (current, 0, buffer, 0, current_length);
			this.Buffer = buffer;
		}

#endif

	}
}
