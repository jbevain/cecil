//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

using Mono.Collections.Generic;

namespace Mono.Cecil {

	sealed class InterfaceCollection : Collection<TypeReference> {

		readonly TypeDefinition container;
		readonly Collection<InterfaceCustomAttributeProvider> custom_attribute_providers;

		public InterfaceCollection (TypeDefinition container)
		{
			this.container = container;
			this.custom_attribute_providers = new Collection<InterfaceCustomAttributeProvider> ();
		}

		public InterfaceCollection (TypeDefinition container, int capacity)
		{
			this.container = container;
			this.custom_attribute_providers = new Collection<InterfaceCustomAttributeProvider> (capacity);
		}

		public ICustomAttributeProvider GetCustomAttributes (int index)
		{
			return custom_attribute_providers [index];
		}

		public void Add (TypeReference @interface, MetadataToken token)
		{
			Add (@interface);
			custom_attribute_providers [custom_attribute_providers.Count - 1].MetadataToken = token;
		}

		protected override void OnAdd (TypeReference item, int index)
		{
			custom_attribute_providers.Add (new InterfaceCustomAttributeProvider (container.Module));
		}

		protected override void OnSet (TypeReference item, int index)
		{
			custom_attribute_providers [index].Clear ();
		}

		protected override void OnInsert (TypeReference item, int index)
		{
			custom_attribute_providers.Insert (index, new InterfaceCustomAttributeProvider (container.Module));
		}

		protected override void OnRemove (TypeReference item, int index)
		{
			custom_attribute_providers.RemoveAt (index);
		}

		protected override void OnClear ()
		{
			custom_attribute_providers.Clear ();
		}
	}
}
