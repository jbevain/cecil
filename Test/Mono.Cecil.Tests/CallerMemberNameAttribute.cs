#if NET_3_5 || NET_4_0
namespace System.Runtime.CompilerServices {
	[AttributeUsage (AttributeTargets.Parameter, Inherited = false)]
	public sealed class CallerMemberNameAttribute : Attribute {
		public CallerMemberNameAttribute ()
		{
		}
	}
}
#endif