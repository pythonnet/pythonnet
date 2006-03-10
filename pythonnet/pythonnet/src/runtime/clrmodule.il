// Copyright (c) 2003 Zope Corporation and Contributors.
//
// All Rights Reserved.
//
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.

//============================================================================
// This file is a hand-maintained stub - it implements CLR.dll, which can be
// loaded by a standard CPython interpreter as an extension module. When it 
// is loaded, it bootstraps the managed runtime integration layer and defers
// to it do initialization and put the CLR module into sys.modules, etc.
//============================================================================

.assembly extern mscorlib
{
  .publickeytoken = (B7 7A 5C 56 19 34 E0 89 )
  .ver 1:0:5000:0
}
.assembly extern Python.Runtime
{
  .ver 1:0:0:0
}
.assembly CLR
{
  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(bool,
  //                                                                                bool) = ( 01 00 00 01 00 00 ) 
  .hash algorithm 0x00008004
  .ver 0:0:0:0
}

.module CLR.dll
// MVID: {01BEB897-1638-4D9D-B01C-3638714A76B4}
.imagebase 0x00400000
.subsystem 0x00000003
.file alignment 512
.corflags 0x00000002

.vtfixup [1] int32 fromunmanaged at VT_01
.data VT_01 = int32(0)


.class public auto ansi beforefieldinit CLRModule
       extends [mscorlib]System.Object
{
} // end of class CLRModule



.class public auto ansi beforefieldinit CLRModule
       extends [mscorlib]System.Object
{
  .method public hidebysig static void 
   modopt([mscorlib]System.Runtime.CompilerServices.CallConvCdecl) 
   initCLR() cil managed
  {
    .vtentry 1:1
    .export [1] as initCLR

    // Code size       8 (0x8)
    .maxstack  0
    IL_0000:  call void [Python.Runtime]Python.Runtime.PythonEngine::InitExt()
    IL_0005:  br.s       IL_0007

    IL_0007:  ret
  } // end of method CLRModule::initCLR

  .method public hidebysig specialname rtspecialname 
          instance void  .ctor() cil managed
  {
    // Code size       7 (0x7)
    .maxstack  1
    IL_0000:  ldarg.0
    IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
    IL_0006:  ret
  } // end of method CLRModule::.ctor

} // end of class CLRModule


