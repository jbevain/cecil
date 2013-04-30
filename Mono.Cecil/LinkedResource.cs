//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

using System.IO;
using System;
namespace Mono.Cecil {

	/// <summary>
	/// Specifies how to calculate the hash of a linked resource.
	/// </summary>
	public enum LinkedResourceHashSource {
		/// <summary>
		/// The hash is calculated from the file specified in the File property of the LinkedResource.
		/// </summary>
		File, 
		/// <summary>
		/// The hash is not calculated, but space is reserved in the assembly for later calculation with a tool such as sn.exe.
		/// </summary>
		Delay, 
		/// <summary>
		/// The hash is explicitly provided.
		/// </summary>
		Explicit }

	public sealed class LinkedResource : Resource {

		private byte[] hash;
		private string file;
		private string resourceFileName;
		private LinkedResourceHashSource hashSource;
		private DateTime fileTime;

		private void CalculateHash()
		{
			if (hashSource == LinkedResourceHashSource.File) {
				try {
					//Ensure Write+Delete lock on file.
					using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read)) {
						DateTime lastWriteTime = System.IO.File.GetLastWriteTimeUtc(file);
						if (hash == null || lastWriteTime > fileTime) {
							fileTime = lastWriteTime;
							hash = CryptoService.ComputeHash(file);
						}
					}
				}
				catch (FileNotFoundException e) {
					throw new FileNotFoundException("Cannot calculate hash for nonexistant file.", file, e);
				}
			}
			else if (hash == null && hashSource == LinkedResourceHashSource.Delay)
				hash = new byte[20];
		}

		public byte [] Hash {
			get {
				CalculateHash();
				return hash;
			}
			set {
				hashSource = LinkedResourceHashSource.Explicit;
				hash = value;
			}
		}

		/// <summary>
		/// The file path to calculate the hash from. Only used when HashSource == LinkedResourceHashSource.File.
		/// Setting this value will force HashSource to LinkedResourceHashSource.File.
		/// </summary>
		public string File {
			get { return file; }
			set { 
				file = value;
				hashSource = LinkedResourceHashSource.File;
			}
		}

		public string ResourceFileName {
			get { return resourceFileName; }
			set { resourceFileName = value; }
		}

		public LinkedResourceHashSource HashSource {
			get { return hashSource; }
			set { hashSource = value; }
		}

		public override ResourceType ResourceType {
			get { return ResourceType.Linked; }
		}

		/// <summary>
		/// Calculate the hash of the specified file right now instead of delaying until assembly write.
		/// </summary>
		/// <param name="fileName">The file to calculate hash from. Uses the current File if not specified.</param>
		public void UseCurrentFileHash(string fileName = null)
		{
			if (fileName != null)
				File = fileName;
			hashSource = LinkedResourceHashSource.File;
			CalculateHash();
			hashSource = LinkedResourceHashSource.Explicit;
		}
		
		public LinkedResource (string name, ManifestResourceAttributes flags)
			: base (name, flags)
		{
			this.resourceFileName = name;
			this.hashSource = LinkedResourceHashSource.Delay;
		}
		
		public LinkedResource (string name, ManifestResourceAttributes flags, string resourceFileName, byte[] hash)
			: base (name, flags)
		{
			this.resourceFileName = name;
			this.hash = hash;
			this.hashSource = LinkedResourceHashSource.Explicit;
		}

		public LinkedResource (string name, ManifestResourceAttributes flags, string resourceFileName, LinkedResourceHashSource hashSource)
			: base (name, flags)
		{
			this.resourceFileName = resourceFileName;
			this.hashSource = hashSource;
		}

		public LinkedResource (string name, ManifestResourceAttributes flags, string file)
			: base (name, flags)
		{
			this.file = file;
			this.resourceFileName = Path.GetFileName(file);
		}
	}
}
