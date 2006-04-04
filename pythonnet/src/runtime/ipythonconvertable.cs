// ===========================================================================
//
// Copyright (c) 2005 Zope Corporation and Contributors.
//
// All Rights Reserved.
//
// This software is subject to the provisions of the Zope Public License,
// Version 2.1 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
//
// ===========================================================================

using System;

namespace Python.Runtime {

    //========================================================================
    // Internal interface for objects that are convertable to a Python object
    // representation. The outputs of this interface are generally expected to
    // be platform-compatible pointers meaningful to the CPython runtime.
    //========================================================================

    internal interface IPythonConvertable {

       //=====================================================================
       // Given an arbitrary managed object, return a valid PyObject pointer 
       // or null if the given object cannot be converted to a Python object. 
       //=====================================================================

	IntPtr GetPythonObjectPtr(object o);

       //=====================================================================
       // Given an arbitrary managed object, return a valid PyType_Object 
       // pointer representing the Python version of the type of that object, 
       // or null if the object has no meaningful Python type.
       //=====================================================================

	IntPtr GetPythonTypePtr(object o);

       //=====================================================================
       // Given an arbitrary managed type object, return a valid PyType_Object
       // pointer representing the Python version of that type,  or null if the
       // given type cannot be converted to a meaningful Python type. 
       //=====================================================================

	IntPtr GetPythonTypePtr(Type t);


       //=====================================================================
       // Given an arbitrary python object, return the  managed object that 
       // the python object wraps, or null if the python object does not wrap
       // a valid managed object.
       //=====================================================================

	object GetManagedObject(IntPtr op);

       //=====================================================================
       // Given an arbitrary python object, return a valid managed Type object
       // representing the type of the python wrapper object, or null if the 
       // python object does not wrap a managed type.
       //=====================================================================

	Type GetManagedType(IntPtr op);


    }

}
