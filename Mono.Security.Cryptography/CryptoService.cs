//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

#if !READ_ONLY

#if !NET_CORE

using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Runtime.Serialization;

using Mono.Security.Cryptography;

using Mono.Cecil.PE;

namespace Mono.Cecil {

	// Most of this code has been adapted
	// from Jeroen Frijters' fantastic work
	// in IKVM.Reflection.Emit. Thanks!

	static class CryptoService {

		public static byte[] GetPublicKey (WriterParameters parameters)
		{
			using (var rsa = parameters.CreateRSA ()) {
				var cspBlob = CryptoConvert.ToCapiPublicKeyBlob(rsa);
				var publicKey = new byte[12 + cspBlob.Length];
				Buffer.BlockCopy(cspBlob, 0, publicKey, 12, cspBlob.Length);
				publicKey[1] = 36;
				publicKey[4] = 4;
				publicKey[5] = 128;
				publicKey[8] = (byte)(cspBlob.Length >> 0);
				publicKey[9] = (byte)(cspBlob.Length >> 8);
				publicKey[10] = (byte)(cspBlob.Length >> 16);
				publicKey[11] = (byte)(cspBlob.Length >> 24);
				return publicKey;
			}
		}

		public static void StrongName (Stream stream, ImageWriter writer, WriterParameters parameters)
		{
			int strong_name_pointer;

			var strong_name = CreateStrongName (parameters, HashStream (stream, writer, out strong_name_pointer));
			PatchStrongName (stream, strong_name_pointer, strong_name);
		}

		static void PatchStrongName (Stream stream, int strong_name_pointer, byte [] strong_name)
		{
			stream.Seek (strong_name_pointer, SeekOrigin.Begin);
			stream.Write (strong_name, 0, strong_name.Length);
		}

		static byte [] CreateStrongName (WriterParameters parameters, byte [] hash)
		{
			const string hash_algo = "SHA1";

			using (var rsa = parameters.CreateRSA ()) {
				var formatter = new RSAPKCS1SignatureFormatter (rsa);
				formatter.SetHashAlgorithm (hash_algo);

				byte [] signature = formatter.CreateSignature (hash);
				Array.Reverse (signature);

				return signature;
			}
		}

		static byte [] HashStream (Stream stream, ImageWriter writer, out int strong_name_pointer)
		{
			const int buffer_size = 8192;

			var text = writer.text;
			var header_size = (int) writer.GetHeaderSize ();
			var text_section_pointer = (int) text.PointerToRawData;
			var strong_name_directory = writer.GetStrongNameSignatureDirectory ();

			if (strong_name_directory.Size == 0)
				throw new InvalidOperationException ();

			strong_name_pointer = (int) (text_section_pointer
				+ (strong_name_directory.VirtualAddress - text.VirtualAddress));
			var strong_name_length = (int) strong_name_directory.Size;

			var sha1 = new SHA1Managed ();
			var buffer = new byte [buffer_size];
			using (var crypto_stream = new CryptoStream (Stream.Null, sha1, CryptoStreamMode.Write)) {

				stream.Seek (0, SeekOrigin.Begin);
				CopyStreamChunk (stream, crypto_stream, buffer, header_size);

				stream.Seek (text_section_pointer, SeekOrigin.Begin);
				CopyStreamChunk (stream, crypto_stream, buffer, (int) strong_name_pointer - text_section_pointer);

				stream.Seek (strong_name_length, SeekOrigin.Current);
				CopyStreamChunk (stream, crypto_stream, buffer, (int) (stream.Length - (strong_name_pointer + strong_name_length)));
			}

			return sha1.Hash;
		}

		static void CopyStreamChunk (Stream stream, Stream dest_stream, byte [] buffer, int length)
		{
			while (length > 0) {
				int read = stream.Read (buffer, 0, System.Math.Min (buffer.Length, length));
				dest_stream.Write (buffer, 0, read);
				length -= read;
			}
		}

		public static byte [] ComputeHash (string file)
		{
			if (!File.Exists (file))
				return Empty<byte>.Array;

			const int buffer_size = 8192;

			var sha1 = new SHA1Managed ();

			using (var stream = new FileStream (file, FileMode.Open, FileAccess.Read, FileShare.Read)) {

				var buffer = new byte [buffer_size];

				using (var crypto_stream = new CryptoStream (Stream.Null, sha1, CryptoStreamMode.Write))
					CopyStreamChunk (stream, crypto_stream, buffer, (int) stream.Length);
			}

			return sha1.Hash;
		}
	}

	static partial class Mixin {

		public static RSA CreateRSA (this WriterParameters writer_parameters)
		{
			byte [] key;
			string key_container;

			if (writer_parameters.StrongNameKeyBlob != null)
				return CryptoConvert.FromCapiKeyBlob (writer_parameters.StrongNameKeyBlob);

			if (writer_parameters.StrongNameKeyContainer != null)
				key_container = writer_parameters.StrongNameKeyContainer ;
			else
			#pragma warning disable 0618
			if (!TryGetKeyContainer (writer_parameters.StrongNameKeyPair, out key, out key_container))
			#pragma warning restore 0618
				return CryptoConvert.FromCapiKeyBlob (key);

			var parameters = new CspParameters {
				Flags = CspProviderFlags.UseMachineKeyStore,
				KeyContainerName = key_container,
				KeyNumber = 2,
			};

			return new RSACryptoServiceProvider (parameters);
		}

		static bool TryGetKeyContainer (ISerializable key_pair, out byte [] key, out string key_container)
		{
			var info = new SerializationInfo (typeof (StrongNameKeyPair), new FormatterConverter ());
			key_pair.GetObjectData (info, new StreamingContext ());

			key = (byte []) info.GetValue ("_keyPairArray", typeof (byte []));
			key_container = info.GetString ("_keyPairContainer");
			return key_container != null;
		}
	}
}

#endif

#endif
