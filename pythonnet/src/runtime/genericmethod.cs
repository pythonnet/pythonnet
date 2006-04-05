// Copyright (c) 2001, 2002 Zope Corporation and Contributors.
//
// All Rights Reserved.
//
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.

using System;
using System.Collections;
using System.Reflection;

namespace Python.Runtime {

    //========================================================================
    // Implements a Python type that represents a CLR generic method.
    //========================================================================

    internal class GenericMethod : MethodObject {

	public GenericMethod(string name, MethodInfo[] info) : 
	       base(name, info) {

	}

	//====================================================================
	// Implement [] semantics for method objects. This operation binds the
	// method creating a closed generic method which behaves the same as
	// a normal CLR method from a Python point of view.
	//====================================================================

	public static new IntPtr mp_subscript(IntPtr op, IntPtr idx) {
//  	    GenericMethod self = GetManagedObject(op) as GenericMethod;
//  	    for (int i = 0; i < info.Length; i++) {
//  		MethodInfo item = (MethodInfo)info[i];
//  		if (item.IsGenericMethodDefinition || ) {
//  		}
//  	    }

//  	    GenericType gt = GetManagedObject(op) as GenericType;
//  	    if (gt != null) {
//  		return GenericType.bind(tp, idx);
//  	    }
	    return Exceptions.RaiseTypeError("unsubscriptable object");
	}

	//====================================================================
	// Descriptor __repr__ implementation.
	//====================================================================

	public static new IntPtr tp_repr(IntPtr ob) {
	    GenericMethod self = GetManagedObject(ob) as GenericMethod;
	    string s = String.Format("<generic method '{0}'>", self.name);
	    return Runtime.PyString_FromStringAndSize(s, s.Length);
	}

    }


}
