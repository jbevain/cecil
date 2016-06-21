//
// Author:
//   Virgile Bello (virgile.bello@gmail.com)
//
// Copyright (c) 2016 - 2016 Virgile Bello
//
// Licensed under the MIT/X11 license.
//

using System;
using System.Collections.Generic;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;

namespace Mono.Cecil.Pdb {
	public sealed class PdbMethodSymbols : MethodSymbols {
		public PdbMethodSymbols (MethodBody methodBody) : base (methodBody)
		{
		}

		public string IteratorClass { get; set; }

		public Collection<PdbIteratorScope> IteratorScopes { get; set; }

		public Collection<string> UsedNamespaces { get; set; }

		public Collection<ushort> UsingCounts { get; set; }

		public MethodReference MethodWhoseUsingInfoAppliesToThisMethod { get; set; }

		public PdbSynchronizationInformation SynchronizationInformation { get; set; }

		public override MethodSymbols Clone()
		{
			var result = new PdbMethodSymbols(method_body);
			CloneTo (result);
			return result;
		}

		protected void CloneTo (PdbMethodSymbols symbols)
	    {
	        base.CloneTo (symbols);

	        symbols.IteratorClass = IteratorClass;

			if (IteratorScopes != null) {
				symbols.IteratorScopes = new Collection<PdbIteratorScope> ();
				foreach (var iteratorScope in IteratorScopes) {
					symbols.IteratorScopes.Add (new PdbIteratorScope (iteratorScope.Start, iteratorScope.End));
				}
			}

			if (UsedNamespaces != null) {
				symbols.UsedNamespaces = new Collection<string> (UsedNamespaces);
			}

			if (UsingCounts != null) {
				symbols.UsingCounts = new Collection<ushort> (UsingCounts);
			}

			symbols.MethodWhoseUsingInfoAppliesToThisMethod = MethodWhoseUsingInfoAppliesToThisMethod;

			if (SynchronizationInformation != null) {
				symbols.SynchronizationInformation = new PdbSynchronizationInformation {
					KickoffMethod = SynchronizationInformation.KickoffMethod,
					GeneratedCatchHandlerIlOffset = SynchronizationInformation.GeneratedCatchHandlerIlOffset,
				};
				if (SynchronizationInformation.SynchronizationPoints != null) {
					symbols.SynchronizationInformation.SynchronizationPoints = new Collection<PdbSynchronizationPoint> ();
					foreach (var synchronizationPoint in SynchronizationInformation.SynchronizationPoints) {
						symbols.SynchronizationInformation.SynchronizationPoints.Add (new PdbSynchronizationPoint {
							SynchronizeOffset = synchronizationPoint.SynchronizeOffset,
							ContinuationMethod = synchronizationPoint.ContinuationMethod,
							ContinuationOffset = synchronizationPoint.ContinuationOffset,
						});
					}
				}
			}
		}
	}

	public class PdbSynchronizationInformation {
		public MethodReference KickoffMethod { get; set; }
		public uint GeneratedCatchHandlerIlOffset { get; set; }
		public Collection<PdbSynchronizationPoint> SynchronizationPoints { get; set; }
	}

	public class PdbSynchronizationPoint {
		public uint SynchronizeOffset { get; set; }
		public MethodReference ContinuationMethod { get; set; }
		public uint ContinuationOffset { get; set; }
	}
}