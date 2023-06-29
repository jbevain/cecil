.assembly extern mscorlib
{
  .publickeytoken = (B7 7A 5C 56 19 34 E0 89)
  .ver 2:0:0:0
}

.assembly PrivateScope {}

.module PrivateScope.dll

.class private auto ansi Foo {

  .method public specialname rtspecialname instance void .ctor () cil managed
  {
    ldarg.0
    call instance void [mscorlib]System.Object::.ctor ()
    ret
  }

  .method public instance void  CallSameNameMethods() cil managed
  {
    ldarg.0
    call      instance void Foo::'SameName'()
    ldarg.0
    call      instance void Foo::'SameName$PST0600A3BA'()
    ldarg.0
    call      instance void Foo::'SameName$PST0600A3BC'()
    ret
  }

 .method public hidebysig
    instance void 'SameName' () cil managed
  {
    ret
  }

  .method privatescope
    instance void 'SameName$PST0600A3BA' () cil managed
  {
    ret
  }

  .method privatescope
    instance void 'SameName$PST0600A3BC' () cil managed
  {
    ret
  }
  
  .method public instance void  CallSameNameMethodsGeneric() cil managed
  {
    ldarg.0
    call      instance void Foo::'SameNameGeneric'<int32>()
    ldarg.0
    call      instance void Foo::'SameNameGeneric$PST0600A3BD'<int32>()
    ldarg.0
    call      instance void Foo::'SameNameGeneric$PST0600A3BE'<int32>()
    ret
  }

 .method public hidebysig
    instance void 'SameNameGeneric'<T> () cil managed
  {
    ret
  }

  .method privatescope
    instance void 'SameNameGeneric$PST0600A3BD'<T> () cil managed
  {
    ret
  }

  .method privatescope
    instance void 'SameNameGeneric$PST0600A3BE'<T> () cil managed
  {
    ret
  }
}