//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

using System.Collections.Generic;
using System.Linq;

namespace Mono.Collections.Generic {

	public class Collection<T> : System.Collections.ObjectModel.Collection<T> {
		public Collection ()
		{
		}

		// [Obsolete("This function unfortunately does not actually take the capacity into account.")]
		public Collection (int capacity)
		{
		}

		public Collection (IEnumerable<T> items) : base (items.ToList())
		{
		}

		protected override void InsertItem (int index, T item)
		{
			if (index == Count)
				OnAdd (item, index);
			else
				OnInsert (item, index);
			base.InsertItem (index, item);
		}

		protected virtual void OnAdd (T item, int index)
		{
		}

		protected virtual void OnInsert (T item, int index)
		{
		}

		protected override void SetItem (int index, T item)
		{
			OnSet (item, index);
			base.SetItem (index, item);
		}

		protected virtual void OnSet (T item, int index)
		{
		}

		protected override void RemoveItem (int index)
		{
			OnRemove (this [index], index);
			base.RemoveItem (index);
		}

		protected virtual void OnRemove (T item, int index)
		{
		}

		protected override void ClearItems ()
		{
			OnClear ();
			base.ClearItems ();
		}

		protected virtual void OnClear ()
		{
		}
	}
}
