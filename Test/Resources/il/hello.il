.assembly extern mscorlib
{
  .publickeytoken = (B7 7A 5C 56 19 34 E0 89)
  .ver 2:0:0:0
}

.assembly Hello {}

.module Hello.dll

.class private auto ansi Foo {

	.method public specialname rtspecialname instance void .ctor () cil managed
	{
		ldarg.0
		call instance void [mscorlib]System.Object::.ctor ()
		ret
	}

	.method public static void Bar (int32 a, int32 b)
	{
		.locals init (int32 res)
		ldarg.0
		ldarg.1
		mul
		stloc.0
		ldloc.0
		call void Foo::Baz (int32)
		ret
	}

	.method public static void Baz (int32 a)
	{
		ret
	}

	.method public void Gazonk ()
	{
		ldarg 0
		pop
		ret
	}

	.method public static void PrintEmpty ()
	{
		ldsfld string [mscorlib]System.String::Empty
		call void [mscorlib]System.Console::WriteLine(string)
		ret
	}
}
