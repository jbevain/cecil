#if !NET_CORE

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Mono.Cecil.Tests {

	[TestFixture]
	public abstract class BaseWindowsRuntimeProjectionsTests : BaseTestFixture {

		protected abstract string ModuleName { get; }
		protected abstract MetadataKind ExpectedMetadataKind { get; }
		protected abstract string [] ManagedClassTypeNames { get; }
		protected abstract string [] CustomListTypeNames { get; }

		[Test]
		public void CanReadMetadataType ()
		{
			if (Platform.OnMono)
				return;

			TestModule (ModuleName, (module) => {
				Assert.AreEqual (ExpectedMetadataKind, module.MetadataKind);
			}, verify: false, assemblyResolver: WindowsRuntimeAssemblyResolver.CreateInstance (), applyWindowsRuntimeProjections: true);
		}

		[Test]
		public void CanProjectParametersAndReturnTypes ()
		{
			if (Platform.OnMono)
				return;

			TestModule (ModuleName, (module) => {
				var types = ManagedClassTypeNames.Select (typeName => module.Types.Single (t => t.Name == typeName));

				foreach (var type in types) {
					var listGetter = type.Properties.Single (p => p.Name == "List").GetMethod;
					var listSetter = type.Properties.Single (p => p.Name == "List").SetMethod;

					Assert.IsNotNull (listGetter);
					Assert.IsNotNull (listSetter);

					Assert.AreEqual (listGetter.ReturnType.FullName, "System.Collections.Generic.IList`1<System.Int32>");
					Assert.AreEqual (listSetter.Parameters.Count, 1);
					Assert.AreEqual (listSetter.Parameters [0].ParameterType.FullName, "System.Collections.Generic.IList`1<System.Int32>");
				}
			}, verify: false, assemblyResolver: WindowsRuntimeAssemblyResolver.CreateInstance (), applyWindowsRuntimeProjections: true);
		}

		[Test]
		public void CanProjectInterfaces ()
		{
			if (Platform.OnMono)
				return;

			TestModule (ModuleName, (module) => {
				var types = CustomListTypeNames.Select (typeName => module.Types.Single (t => t.Name == typeName));

				foreach (var type in types) {
					Assert.IsNotNull (type.Interfaces.SingleOrDefault (i => i.InterfaceType.FullName == "System.Collections.Generic.IList`1<System.Int32>"));
					Assert.IsNotNull (type.Interfaces.SingleOrDefault (i => i.InterfaceType.FullName == "System.Collections.Generic.IEnumerable`1<System.Int32>"));
				}
			}, verify: false, assemblyResolver: WindowsRuntimeAssemblyResolver.CreateInstance (), applyWindowsRuntimeProjections: true);
		}

		[Test]
		public void CanStripType ()
		{
			if (Platform.OnMono)
				return;

			var assemblyResolver = WindowsRuntimeAssemblyResolver.CreateInstance ();

			TestModule (ModuleName, (originalModule) => {
				var types = CustomListTypeNames.Select (typeName => originalModule.Types.Single (t => t.Name == typeName)).ToArray ();

				foreach (var type in types)
					originalModule.Types.Remove (type);

				var tmpPath = Path.GetTempFileName ();
				originalModule.Write (tmpPath);

				try {
					TestModule (tmpPath, (modifiedModule) => {
						foreach (var type in types)
							Assert.IsTrue (!modifiedModule.Types.Any (t => t.FullName == type.FullName));
					}, verify: false, assemblyResolver: assemblyResolver, applyWindowsRuntimeProjections: true);
				} finally {
					File.Delete (tmpPath);
				}
			}, readOnly: true, verify: false, assemblyResolver: assemblyResolver, applyWindowsRuntimeProjections: true);
		}
	}

	[TestFixture]
	public class ManagedWindowsRuntimeProjectionsTests : BaseWindowsRuntimeProjectionsTests {

		protected override string ModuleName { get { return "ManagedWinmd.winmd"; } }

		protected override MetadataKind ExpectedMetadataKind { get { return MetadataKind.ManagedWindowsMetadata; } }

		protected override string [] ManagedClassTypeNames { get { return new [] { "ManagedClass", "<WinRT>ManagedClass" }; } }

		protected override string [] CustomListTypeNames { get { return new [] { "CustomList", "<WinRT>CustomList" }; } }

		[Test]
		public void CanProjectClasses ()
		{
			if (Platform.OnMono)
				return;

			TestModule (ModuleName, (module) => {
				var managedClassType = module.Types.Single (t => t.Name == "ManagedClass");
				Assert.AreEqual ("<CLR>ManagedClass", managedClassType.WindowsRuntimeProjection.Name);
				Assert.AreEqual (TypeDefinitionTreatment.UnmangleWindowsRuntimeName, managedClassType.WindowsRuntimeProjection.Treatment);

				var someOtherClassType = module.Types.Single (t => t.Name == "SomeOtherClass");
				Assert.AreEqual ("<CLR>SomeOtherClass", someOtherClassType.WindowsRuntimeProjection.Name);
				Assert.AreEqual (TypeDefinitionTreatment.UnmangleWindowsRuntimeName, someOtherClassType.WindowsRuntimeProjection.Treatment);

				var winrtManagedClassType = module.Types.Single (t => t.Name == "<WinRT>ManagedClass");
				Assert.AreEqual ("ManagedClass", winrtManagedClassType.WindowsRuntimeProjection.Name);
				Assert.AreEqual (TypeDefinitionTreatment.PrefixWindowsRuntimeName, winrtManagedClassType.WindowsRuntimeProjection.Treatment);

				var winrtSomeOtherClassType = module.Types.Single (t => t.Name == "<WinRT>SomeOtherClass");
				Assert.AreEqual ("SomeOtherClass", winrtSomeOtherClassType.WindowsRuntimeProjection.Name);
				Assert.AreEqual (TypeDefinitionTreatment.PrefixWindowsRuntimeName, winrtSomeOtherClassType.WindowsRuntimeProjection.Treatment);
			}, verify: false, assemblyResolver: WindowsRuntimeAssemblyResolver.CreateInstance (), applyWindowsRuntimeProjections: true);
		}

		[Test]
		public void VerifyTypeReferenceToProjectedTypeInAttributeArgumentReferencesUnmangledTypeName()
		{
			if (Platform.OnMono)
				return;

			TestModule(ModuleName, (module) =>
			{
				var type = module.Types.Single(t => t.Name == "ClassWithAsyncMethod");
				var method = type.Methods.Single(m => m.Name == "DoStuffAsync");

				var attribute = method.CustomAttributes.Single(a => a.AttributeType.Name == "AsyncStateMachineAttribute");
				var attributeArgument = (TypeReference)attribute.ConstructorArguments[0].Value;

				Assert.AreEqual("ManagedWinmd.ClassWithAsyncMethod/<DoStuffAsync>d__0", attributeArgument.FullName);
			}, verify: false, assemblyResolver: WindowsRuntimeAssemblyResolver.CreateInstance(), applyWindowsRuntimeProjections: true);
		}
	}

	[TestFixture]
	public class NativeWindowsRuntimeProjectionsTests : BaseWindowsRuntimeProjectionsTests {

		protected override string ModuleName { get { return "NativeWinmd.winmd"; } }

		protected override MetadataKind ExpectedMetadataKind { get { return MetadataKind.WindowsMetadata; } }

		protected override string [] ManagedClassTypeNames { get { return new [] { "ManagedClass" }; } }

		protected override string [] CustomListTypeNames { get { return new [] { "CustomList" }; } }

		[Test]
		public void CanProjectAndRedirectInterfaces ()
		{
			if (Platform.OnMono)
				return;

			TestModule (ModuleName, (module) => {
				var customListClass = module.Types.Single (t => t.Name == "CustomList");
				Assert.AreEqual (5, customListClass.Interfaces.Count);

				Assert.AreEqual (1, customListClass.Interfaces[0].CustomAttributes.Count);
				Assert.AreEqual ("Windows.Foundation.Metadata.DefaultAttribute", customListClass.Interfaces[0].CustomAttributes[0].AttributeType.FullName);
				Assert.AreEqual ("NativeWinmd.__ICustomListPublicNonVirtuals", customListClass.Interfaces[0].InterfaceType.FullName);

				Assert.AreEqual (0, customListClass.Interfaces[1].CustomAttributes.Count);
				Assert.AreEqual ("System.Collections.Generic.IList`1<System.Int32>", customListClass.Interfaces[1].InterfaceType.FullName);

				Assert.AreEqual (0, customListClass.Interfaces[2].CustomAttributes.Count);
				Assert.AreEqual ("System.Collections.Generic.IEnumerable`1<System.Int32>", customListClass.Interfaces[2].InterfaceType.FullName);

				Assert.AreEqual (0, customListClass.Interfaces[3].CustomAttributes.Count);
				Assert.AreEqual ("Windows.Foundation.Collections.IVector`1<System.Int32>", customListClass.Interfaces[3].InterfaceType.FullName);

				Assert.AreEqual (0, customListClass.Interfaces[4].CustomAttributes.Count);
				Assert.AreEqual ("Windows.Foundation.Collections.IIterable`1<System.Int32>", customListClass.Interfaces[4].InterfaceType.FullName);

				var customPropertySetClass = module.Types.Single (t => t.Name == "CustomPropertySet");
				Assert.AreEqual (7, customPropertySetClass.Interfaces.Count);

				Assert.AreEqual (0, customPropertySetClass.Interfaces[0].CustomAttributes.Count);
				Assert.AreEqual ("Windows.Foundation.Collections.IPropertySet", customPropertySetClass.Interfaces[0].InterfaceType.FullName);

				Assert.AreEqual (1, customPropertySetClass.Interfaces[1].CustomAttributes.Count);
				Assert.AreEqual ("Windows.Foundation.Metadata.DefaultAttribute", customPropertySetClass.Interfaces[1].CustomAttributes[0].AttributeType.FullName);
				Assert.AreEqual ("NativeWinmd.__ICustomPropertySetPublicNonVirtuals", customPropertySetClass.Interfaces[1].InterfaceType.FullName);

				Assert.AreEqual (0, customPropertySetClass.Interfaces[2].CustomAttributes.Count);
				Assert.AreEqual ("Windows.Foundation.Collections.IObservableMap`2<System.String,System.Object>", customPropertySetClass.Interfaces[2].InterfaceType.FullName);

				Assert.AreEqual (0, customPropertySetClass.Interfaces[3].CustomAttributes.Count);
				Assert.AreEqual ("System.Collections.Generic.IDictionary`2<System.String,System.Object>", customPropertySetClass.Interfaces[3].InterfaceType.FullName);

				Assert.AreEqual (0, customPropertySetClass.Interfaces[4].CustomAttributes.Count);
				Assert.AreEqual ("System.Collections.Generic.IEnumerable`1<System.Collections.Generic.KeyValuePair`2<System.String,System.Object>>", customPropertySetClass.Interfaces[4].InterfaceType.FullName);

				Assert.AreEqual (0, customPropertySetClass.Interfaces[5].CustomAttributes.Count);
				Assert.AreEqual ("Windows.Foundation.Collections.IMap`2<System.String,System.Object>", customPropertySetClass.Interfaces[5].InterfaceType.FullName);

				Assert.AreEqual (0, customPropertySetClass.Interfaces[6].CustomAttributes.Count);
				Assert.AreEqual ("Windows.Foundation.Collections.IIterable`1<System.Collections.Generic.KeyValuePair`2<System.String,System.Object>>", customPropertySetClass.Interfaces[6].InterfaceType.FullName);

			}, verify: false, assemblyResolver: WindowsRuntimeAssemblyResolver.CreateInstance (), applyWindowsRuntimeProjections: true);
		}

		[Test]
		public void CanProjectInterfaceMethods ()
		{
			if (Platform.OnMono)
				return;

			TestModule (ModuleName, (module) => {
				var customListClass = module.Types.Single (t => t.Name == "CustomList");
				Assert.AreEqual (28, customListClass.Methods.Count);
				Assert.AreEqual (TypeDefinitionTreatment.RedirectImplementedMethods, customListClass.WindowsRuntimeProjection.Treatment);

				// Verify that projections add implementations for all projected interfaces methods
				Assert.AreEqual (customListClass.Methods[0].FullName, "System.Void NativeWinmd.CustomList::.ctor()");
				Assert.AreEqual (customListClass.Methods[1].FullName, "Windows.Foundation.Collections.IIterator`1<System.Int32> NativeWinmd.CustomList::First()");
				Assert.AreEqual (customListClass.Methods[2].FullName, "System.UInt32 NativeWinmd.CustomList::get_Size()");
				Assert.AreEqual (customListClass.Methods[3].FullName, "System.Int32 NativeWinmd.CustomList::GetAt(System.UInt32)");
				Assert.AreEqual (customListClass.Methods[4].FullName, "System.Collections.Generic.IReadOnlyList`1<System.Int32> NativeWinmd.CustomList::GetView()");
				Assert.AreEqual (customListClass.Methods[5].FullName, "System.Boolean NativeWinmd.CustomList::IndexOf(System.Int32,System.UInt32&)");
				Assert.AreEqual (customListClass.Methods[6].FullName, "System.Void NativeWinmd.CustomList::SetAt(System.UInt32,System.Int32)");
				Assert.AreEqual (customListClass.Methods[7].FullName, "System.Void NativeWinmd.CustomList::InsertAt(System.UInt32,System.Int32)");
				Assert.AreEqual (customListClass.Methods[8].FullName, "System.Void NativeWinmd.CustomList::RemoveAt(System.UInt32)");
				Assert.AreEqual (customListClass.Methods[9].FullName, "System.Void NativeWinmd.CustomList::Append(System.Int32)");
				Assert.AreEqual (customListClass.Methods[10].FullName, "System.Void NativeWinmd.CustomList::RemoveAtEnd()");
				Assert.AreEqual (customListClass.Methods[11].FullName, "System.Void NativeWinmd.CustomList::Clear()");
				Assert.AreEqual (customListClass.Methods[12].FullName, "System.UInt32 NativeWinmd.CustomList::GetMany(System.UInt32,System.Int32[])");
				Assert.AreEqual (customListClass.Methods[13].FullName, "System.Void NativeWinmd.CustomList::ReplaceAll(System.Int32[])");
				Assert.AreEqual (customListClass.Methods[14].FullName, "System.Int32 NativeWinmd.CustomList::get_Item(System.Int32)");
				Assert.AreEqual (customListClass.Methods[15].FullName, "System.Void NativeWinmd.CustomList::set_Item(System.Int32,System.Int32)");
				Assert.AreEqual (customListClass.Methods[16].FullName, "System.Int32 NativeWinmd.CustomList::IndexOf(System.Int32)");
				Assert.AreEqual (customListClass.Methods[17].FullName, "System.Void NativeWinmd.CustomList::Insert(System.Int32,System.Int32)");
				Assert.AreEqual (customListClass.Methods[18].FullName, "System.Void NativeWinmd.CustomList::RemoveAt(System.Int32)");
				Assert.AreEqual (customListClass.Methods[19].FullName, "System.Int32 NativeWinmd.CustomList::get_Count()");
				Assert.AreEqual (customListClass.Methods[20].FullName, "System.Boolean NativeWinmd.CustomList::get_IsReadOnly()");
				Assert.AreEqual (customListClass.Methods[21].FullName, "System.Void NativeWinmd.CustomList::Add(System.Int32)");
				Assert.AreEqual (customListClass.Methods[22].FullName, "System.Void NativeWinmd.CustomList::Clear()");
				Assert.AreEqual (customListClass.Methods[23].FullName, "System.Boolean NativeWinmd.CustomList::Contains(System.Int32)");
				Assert.AreEqual (customListClass.Methods[24].FullName, "System.Void NativeWinmd.CustomList::CopyTo(System.Int32[],System.Int32)");
				Assert.AreEqual (customListClass.Methods[25].FullName, "System.Boolean NativeWinmd.CustomList::Remove(System.Int32)");
				Assert.AreEqual (customListClass.Methods[26].FullName, "System.Collections.Generic.IEnumerator`1<System.Int32> NativeWinmd.CustomList::GetEnumerator()");
				Assert.AreEqual (customListClass.Methods[27].FullName, "System.Collections.IEnumerator NativeWinmd.CustomList::GetEnumerator()");
			}, verify: false, assemblyResolver: WindowsRuntimeAssemblyResolver.CreateInstance (), applyWindowsRuntimeProjections: true);
		}

		[Test]
		public void CanProjectMethodOverrides ()
		{
			if (Platform.OnMono)
				return;

			TestModule (ModuleName, (module) => {
				var customListClass = module.Types.Single (t => t.Name == "CustomList");

				for (int i = 1; i < customListClass.Methods.Count; i++)
					Assert.AreEqual (1, customListClass.Methods[i].Overrides.Count);

				Assert.AreEqual (customListClass.Methods[1].Overrides[0].FullName, "Windows.Foundation.Collections.IIterator`1<!0> Windows.Foundation.Collections.IIterable`1<System.Int32>::First()");
				Assert.AreEqual (customListClass.Methods[2].Overrides[0].FullName, "System.UInt32 Windows.Foundation.Collections.IVector`1<System.Int32>::get_Size()");
				Assert.AreEqual (customListClass.Methods[3].Overrides[0].FullName, "!0 Windows.Foundation.Collections.IVector`1<System.Int32>::GetAt(System.UInt32)");
				Assert.AreEqual (customListClass.Methods[4].Overrides[0].FullName, "System.Collections.Generic.IReadOnlyList`1<!0> Windows.Foundation.Collections.IVector`1<System.Int32>::GetView()");
				Assert.AreEqual (customListClass.Methods[5].Overrides[0].FullName, "System.Boolean Windows.Foundation.Collections.IVector`1<System.Int32>::IndexOf(!0,System.UInt32&)");
				Assert.AreEqual (customListClass.Methods[6].Overrides[0].FullName, "System.Void Windows.Foundation.Collections.IVector`1<System.Int32>::SetAt(System.UInt32,!0)");
				Assert.AreEqual (customListClass.Methods[7].Overrides[0].FullName, "System.Void Windows.Foundation.Collections.IVector`1<System.Int32>::InsertAt(System.UInt32,!0)");
				Assert.AreEqual (customListClass.Methods[8].Overrides[0].FullName, "System.Void Windows.Foundation.Collections.IVector`1<System.Int32>::RemoveAt(System.UInt32)");
				Assert.AreEqual (customListClass.Methods[9].Overrides[0].FullName, "System.Void Windows.Foundation.Collections.IVector`1<System.Int32>::Append(!0)");
				Assert.AreEqual (customListClass.Methods[10].Overrides[0].FullName, "System.Void Windows.Foundation.Collections.IVector`1<System.Int32>::RemoveAtEnd()");
				Assert.AreEqual (customListClass.Methods[11].Overrides[0].FullName, "System.Void Windows.Foundation.Collections.IVector`1<System.Int32>::Clear()");
				Assert.AreEqual (customListClass.Methods[12].Overrides[0].FullName, "System.UInt32 Windows.Foundation.Collections.IVector`1<System.Int32>::GetMany(System.UInt32,!0[])");
				Assert.AreEqual (customListClass.Methods[13].Overrides[0].FullName, "System.Void Windows.Foundation.Collections.IVector`1<System.Int32>::ReplaceAll(!0[])");
				Assert.AreEqual (customListClass.Methods[14].Overrides[0].FullName, "T System.Collections.Generic.IList`1<System.Int32>::get_Item(System.Int32)");
				Assert.AreEqual (customListClass.Methods[15].Overrides[0].FullName, "System.Void System.Collections.Generic.IList`1<System.Int32>::set_Item(System.Int32,T)");
				Assert.AreEqual (customListClass.Methods[16].Overrides[0].FullName, "System.Int32 System.Collections.Generic.IList`1<System.Int32>::IndexOf(T)");
				Assert.AreEqual (customListClass.Methods[17].Overrides[0].FullName, "System.Void System.Collections.Generic.IList`1<System.Int32>::Insert(System.Int32,T)");
				Assert.AreEqual (customListClass.Methods[18].Overrides[0].FullName, "System.Void System.Collections.Generic.IList`1<System.Int32>::RemoveAt(System.Int32)");
				Assert.AreEqual (customListClass.Methods[19].Overrides[0].FullName, "System.Int32 System.Collections.Generic.ICollection`1<System.Int32>::get_Count()");
				Assert.AreEqual (customListClass.Methods[20].Overrides[0].FullName, "System.Boolean System.Collections.Generic.ICollection`1<System.Int32>::get_IsReadOnly()");
				Assert.AreEqual (customListClass.Methods[21].Overrides[0].FullName, "System.Void System.Collections.Generic.ICollection`1<System.Int32>::Add(T)");
				Assert.AreEqual (customListClass.Methods[22].Overrides[0].FullName, "System.Void System.Collections.Generic.ICollection`1<System.Int32>::Clear()");
				Assert.AreEqual (customListClass.Methods[23].Overrides[0].FullName, "System.Boolean System.Collections.Generic.ICollection`1<System.Int32>::Contains(T)");
				Assert.AreEqual (customListClass.Methods[24].Overrides[0].FullName, "System.Void System.Collections.Generic.ICollection`1<System.Int32>::CopyTo(T[],System.Int32)");
				Assert.AreEqual (customListClass.Methods[25].Overrides[0].FullName, "System.Boolean System.Collections.Generic.ICollection`1<System.Int32>::Remove(T)");
				Assert.AreEqual (customListClass.Methods[26].Overrides[0].FullName, "System.Collections.Generic.IEnumerator`1<T> System.Collections.Generic.IEnumerable`1<System.Int32>::GetEnumerator()");
				Assert.AreEqual (customListClass.Methods[27].Overrides[0].FullName, "System.Collections.IEnumerator System.Collections.IEnumerable::GetEnumerator()");

			}, verify: false, assemblyResolver: WindowsRuntimeAssemblyResolver.CreateInstance (), applyWindowsRuntimeProjections: true);
		}
	}
}
#endif
