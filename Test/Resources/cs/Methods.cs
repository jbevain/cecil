using System;
using System.Runtime.InteropServices;

abstract class Foo {
	public abstract void Bar (int a);
}

class Bar {

	[DllImport ("foo.dll")]
	public extern static void Pan ([MarshalAs (UnmanagedType.I4)] int i);

	[DllImport ("foo.dll")]
	[return: MarshalAs (UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_DISPATCH)]
	public extern static object PanPan (
		[MarshalAs (UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof (Foo), MarshalCookie = "nomnom")] object f,
		[MarshalAs (UnmanagedType.LPArray, ArraySubType = UnmanagedType.I8, SizeParamIndex = 2, SizeConst = 66)] object c,
		int i,
		[MarshalAs (UnmanagedType.LPArray, ArraySubType = UnmanagedType.I2)] object d);
}

public class Baz {

	public void PrintAnswer ()
	{
		Console.WriteLine ("answer: {0}", 42);
	}
}
