using System.Collections.Generic;

namespace Mono.Cecil {
	internal sealed class MethodReferenceComparer : EqualityComparer<MethodReference> {
		public override bool Equals (MethodReference x, MethodReference y)
		{
			return AreEqual (x, y);
		}

		public override int GetHashCode (MethodReference obj)
		{
			return GetHashCodeFor (obj);
		}

		public static bool AreEqual (MethodReference x, MethodReference y)
		{
			if (ReferenceEquals (x, y))
				return true;

			if (x.HasThis != y.HasThis)
				return false;

			if (x.HasParameters != y.HasParameters)
				return false;

			if (x.HasGenericParameters != y.HasGenericParameters)
				return false;

			if (x.Parameters.Count != y.Parameters.Count)
				return false;

			if (x.Name != y.Name)
				return false;

			if (!TypeReferenceEqualityComparer.AreEqual (x.DeclaringType, y.DeclaringType))
				return false;

			var xGeneric = x as GenericInstanceMethod;
			var yGeneric = y as GenericInstanceMethod;
			if (xGeneric != null || yGeneric != null) {
				if (xGeneric == null || yGeneric == null)
					return false;

				if (xGeneric.GenericArguments.Count != yGeneric.GenericArguments.Count)
					return false;

				for (int i = 0; i < xGeneric.GenericArguments.Count; i++)
					if (!TypeReferenceEqualityComparer.AreEqual (xGeneric.GenericArguments[i], yGeneric.GenericArguments[i]))
						return false;
			}

			if (x.Resolve () != y.Resolve ())
				return false;

			return true;
		}

		public static bool AreSignaturesEqual (MethodReference x, MethodReference y)
		{
			if (x.HasThis != y.HasThis)
				return false;

			if (x.Parameters.Count != y.Parameters.Count)
				return false;

			if (x.GenericParameters.Count != y.GenericParameters.Count)
				return false;

			for (var i = 0; i < x.Parameters.Count; i++)
				if (!TypeReferenceEqualityComparer.AreEqual (x.Parameters[i].ParameterType, y.Parameters[i].ParameterType))
					return false;

			if (!TypeReferenceEqualityComparer.AreEqual (x.ReturnType, y.ReturnType))
				return false;

			return true;
		}

		public static int GetHashCodeFor (MethodReference obj)
		{
			// a very good prime number
			const int hashCodeMultiplier = 486187739;

			var genericInstanceMethod = obj as GenericInstanceMethod;
			if (genericInstanceMethod != null) {
				var hashCode = GetHashCodeFor (genericInstanceMethod.ElementMethod);
				for (var i = 0; i < genericInstanceMethod.GenericArguments.Count; i++)
					hashCode = hashCode * hashCodeMultiplier + TypeReferenceEqualityComparer.GetHashCodeFor (genericInstanceMethod.GenericArguments[i]);
				return hashCode;
			}

			return TypeReferenceEqualityComparer.GetHashCodeFor (obj.DeclaringType) * hashCodeMultiplier + obj.Name.GetHashCode ();
		}
	}
}
