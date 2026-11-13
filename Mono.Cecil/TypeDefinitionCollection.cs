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
using System.Collections.Generic;

using Mono.Cecil.Metadata;

using Mono.Collections.Generic;

namespace Mono.Cecil {

	using Slot = Row<string, string>;

	sealed class TypeDefinitionCollection : Collection<TypeDefinition> {

		readonly ModuleDefinition container;

		internal TypeDefinitionCollection (ModuleDefinition container)
		{
			this.container = container;
		}

		internal TypeDefinitionCollection (ModuleDefinition container, int capacity)
			: base (capacity)
		{
			this.container = container;
		}

		protected override void OnAdd (TypeDefinition item, int index)
		{
			Attach (item);
		}

		protected override void OnSet (TypeDefinition item, int index)
		{
			Attach (item);
		}

		protected override void OnInsert (TypeDefinition item, int index)
		{
			Attach (item);
		}

		protected override void OnRemove (TypeDefinition item, int index)
		{
			Detach (item);
		}

		protected override void OnClear ()
		{
			foreach (var type in this)
				Detach (type);
		}

		void Attach (TypeDefinition type)
		{
			if (type.Module != null && type.Module != container)
				throw new ArgumentException ("Type already attached");

			type.module = container;
			type.scope = container;
		}

		void Detach (TypeDefinition type)
		{
			type.module = null;
			type.scope = null;
		}
	}
}
