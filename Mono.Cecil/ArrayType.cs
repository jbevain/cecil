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
using System.Text;
using System.Threading;
using Mono.Collections.Generic;
using MD = Mono.Cecil.Metadata;

namespace Mono.Cecil {

	public struct ArrayDimension {

		int? lower_bound;
		int? upper_bound;

		public int? LowerBound {
			get { return lower_bound; }
			set { lower_bound = value; }
		}

		public int? UpperBound {
			get { return upper_bound; }
			set { upper_bound = value; }
		}

		public bool IsSized {
			get { return lower_bound.HasValue || upper_bound.HasValue; }
		}

		public ArrayDimension (int? lowerBound, int? upperBound)
		{
			this.lower_bound = lowerBound;
			this.upper_bound = upperBound;
		}

		public override string ToString ()
		{
			return !IsSized
				? string.Empty
				: lower_bound + "..." + upper_bound;
		}
	}

	public sealed class ArrayType : TypeSpecification {

		Collection<ArrayDimension> dimensions;

		/// <summary>
		/// Gets the dimensions of a non-vector array, or null for a vector (T[]).
		/// </summary>
		public Collection<ArrayDimension> Dimensions {
			get {
				if (etype == MD.ElementType.SzArray)
					return null;

				if (dimensions != null)
					return dimensions;

				var empty_dimensions = new Collection<ArrayDimension> ();
				empty_dimensions.Add (new ArrayDimension ());

				Interlocked.CompareExchange (ref dimensions, empty_dimensions, null);
				return dimensions;
			}
		}

		public int Rank {
			get { return (dimensions == null || etype == MD.ElementType.SzArray) ? 1 : dimensions.Count; }
		}

		public bool IsVector {
			get { return etype == MD.ElementType.SzArray; }
		}

		public override bool IsValueType {
			get { return false; }
			set { throw new InvalidOperationException (); }
		}

		public override string Name {
			get { return base.Name + Suffix; }
		}

		public override string FullName {
			get { return base.FullName + Suffix; }
		}

		string Suffix {
			get {
				if (IsVector)
					return "[]";
				if (Rank == 1 && !Dimensions [0].IsSized)
					return "[*]";

				var suffix = new StringBuilder ();
				suffix.Append ("[");
				for (int i = 0; i < dimensions.Count; i++) {
					if (i > 0)
						suffix.Append (",");

					suffix.Append (dimensions [i].ToString ());
				}
				suffix.Append ("]");

				return suffix.ToString ();
			}
		}

		public override bool IsArray {
			get { return true; }
		}

		public ArrayType (TypeReference type)
			: this (type, true)
		{
		}

		/// <summary>
		/// Creates a vector (T[]) or a rank-one array with unspecified bounds (T[*]).
		/// </summary>
		public ArrayType (TypeReference type, bool isVector)
			: base (type)
		{
			Mixin.CheckType (type);
			this.etype = isVector ? MD.ElementType.SzArray : MD.ElementType.Array;
		}

		public ArrayType (TypeReference type, int rank)
			: this (type, rank, rank == 1)
		{
		}

		/// <summary>
		/// Creates an array of the specified rank and kind. Vectors must have rank one.
		/// </summary>
		public ArrayType (TypeReference type, int rank, bool vector)
			: this (type, vector)
		{
			if (rank < 1 || rank > 0x1fffffff || (vector && rank != 1))
				throw new ArgumentOutOfRangeException ("rank");

			if (vector)
				return;

			dimensions = new Collection<ArrayDimension> (rank);
			for (int i = 0; i < rank; i++)
				dimensions.Add (new ArrayDimension ());
		}

		internal ArrayType (TypeReference type, ArrayType array)
			: this (type, array.IsVector)
		{
			if (array.IsVector)
				return;

			var source = array.Dimensions;
			dimensions = new Collection<ArrayDimension> (source.Count);
			for (int i = 0; i < source.Count; i++)
				dimensions.Add (source [i]);
		}
	}
}
