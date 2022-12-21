.assembly extern mscorlib
{
  .publickeytoken = (B7 7A 5C 56 19 34 E0 89 )                         // .z\V.4..
  .ver 4:0:0:0
}
.assembly FieldRVAAlignment {}

.module FieldRVAAlignment.dll

.class private auto ansi '<PrivateImplementationDetails>{9B33BB20-87EF-4094-9948-34882DB2F001}'
       extends [mscorlib]System.Object
{
  .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
  .class explicit ansi sealed nested private '__StaticArrayInitTypeSize=3'
         extends [mscorlib]System.ValueType
  {
    .pack 1
    .size 3
  } // end of class '__StaticArrayInitTypeSize=3'

  .class explicit ansi sealed nested private '__StaticArrayInitTypeSize=16'
         extends [mscorlib]System.ValueType
  {
    .pack 8
    .size 16
  } // end of class '__StaticArrayInitTypeSize=16'

  .class explicit ansi sealed nested private '__StaticArrayInitTypeSize=20'
         extends [mscorlib]System.ValueType
  {
    .pack 4
    .size 20
  } // end of class '__StaticArrayInitTypeSize=20'

  .class explicit ansi sealed nested private '__StaticArrayInitTypeSize=5'
         extends [mscorlib]System.ValueType
  {
    .pack 1
    .size 5
  } // end of class '__StaticArrayInitTypeSize=5'

  .class explicit ansi sealed nested private '__StaticArrayInitTypeSize=6'
         extends [mscorlib]System.ValueType
  {
    .pack 2
    .size 6
  } // end of class '__StaticArrayInitTypeSize=6'

  .class explicit ansi sealed nested private '__StaticArrayInitTypeSize=4Align=32'
        extends [mscorlib]System.ValueType
  {
    .pack 32
    .size 4
  }

  .field static assembly valuetype '<PrivateImplementationDetails>{9B33BB20-87EF-4094-9948-34882DB2F001}'/'__StaticArrayInitTypeSize=3' '$$method0x6000001-1' at I_000020F0
  .field static assembly valuetype '<PrivateImplementationDetails>{9B33BB20-87EF-4094-9948-34882DB2F001}'/'__StaticArrayInitTypeSize=20' '$$method0x6000001-2' at I_000020F8
  .field static assembly valuetype '<PrivateImplementationDetails>{9B33BB20-87EF-4094-9948-34882DB2F001}'/'__StaticArrayInitTypeSize=5' '$$method0x6000001-3' at I_00002108
  .field static assembly valuetype '<PrivateImplementationDetails>{9B33BB20-87EF-4094-9948-34882DB2F001}'/'__StaticArrayInitTypeSize=20' '$$method0x6000001-4' at I_00002110
  .field static assembly valuetype '<PrivateImplementationDetails>{9B33BB20-87EF-4094-9948-34882DB2F001}'/'__StaticArrayInitTypeSize=6' '$$method0x6000001-5' at I_00002120
  .field static assembly valuetype '<PrivateImplementationDetails>{9B33BB20-87EF-4094-9948-34882DB2F001}'/'__StaticArrayInitTypeSize=16' '$$method0x6000001-6' at I_00002130
  .field static assembly valuetype '<PrivateImplementationDetails>{9B33BB20-87EF-4094-9948-34882DB2F001}'/'__StaticArrayInitTypeSize=3' 'separator' at I_00002140
  .field static assembly valuetype '<PrivateImplementationDetails>{9B33BB20-87EF-4094-9948-34882DB2F001}'/'__StaticArrayInitTypeSize=4Align=32' '32_align' at I_00002150

} // end of class '<PrivateImplementationDetails>{9B33BB20-87EF-4094-9948-34882DB2F001}'


// =============================================================

.data cil I_000020F0 = bytearray (
                 01 02 03)
.data cil I_000020F8 = bytearray (
                 01 00 00 00 02 00 00 00 03 00 00 00 04 00 00 00 05 00 00 00) 
.data cil I_00002108 = bytearray (
                 04 05 06 07 08)
.data cil I_00002110 = bytearray (
                 01 00 00 00 02 00 00 00 03 00 00 00 05 00 00 00 06 00 00 00) 
.data cil I_00002120 = bytearray (
                 08 00 0C 00 0D 00) 
.data cil I_00002130 = bytearray (
                 01 00 00 00 02 00 00 00 03 00 00 00 05 00 00 00) 
.data cil I_00002140 = bytearray (
                 01 02 03)
.data cil I_00002150 = bytearray (
                 01 02 03 04)
