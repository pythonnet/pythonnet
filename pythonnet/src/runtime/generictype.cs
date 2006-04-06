// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using System;
using System.Reflection;

namespace Python.Runtime {

    /// <summary>
    /// Implements reflected generic types. Note that the Python behavior
    /// is the same for both generic type definitions and constructed open
    /// generic types. Both are essentially factories for creating closed
    /// types based on the required generic type parameters.
    /// </summary>

    internal class GenericType : ClassBase {

	internal GenericType(Type tp) : base(tp) {}

	//====================================================================
	// Implements __new__ for reflected generic types.
	//====================================================================

	public static IntPtr tp_new(IntPtr tp, IntPtr args, IntPtr kw) {
	    Exceptions.SetError(Exceptions.TypeError, 
			       "cannot instantiate an open generic type"
			       );
	    return IntPtr.Zero;
	}


	//====================================================================
	// Implements __call__ for reflected generic types.
	//====================================================================

	public static IntPtr tp_call(IntPtr ob, IntPtr args, IntPtr kw) {
	    Exceptions.SetError(Exceptions.TypeError, 
				"object is not callable");
	    return IntPtr.Zero;
	}

	//====================================================================
	// Implements __getitem__ for reflected open generic types. A closed 
        // type is created by binding the generic type via subscript syntax:
        // inst = List[str]()
	//====================================================================

	public static IntPtr bind(IntPtr ob, IntPtr idx) {
	    ClassBase cls = (ClassBase)GetManagedObject(ob) as ClassBase;

	    // Ensure that the reflected class is a generic type definition,
	    // or that
	    if (!cls.type.IsGenericTypeDefinition) {
		return Exceptions.RaiseTypeError(
		       "type is not a generic type definition"
		       );
	    }

	    // The index argument will often be a tuple, for generic types
	    // that have more than one generic binding parameter.

	    IntPtr args = idx;
	    bool free = false;

	    if (!Runtime.PyTuple_Check(idx)) {
		args = Runtime.PyTuple_New(1);
		Runtime.Incref(idx);
		Runtime.PyTuple_SetItem(args, 0, idx);
		free = true;
	    }

	    int n = Runtime.PyTuple_Size(args);
	    Type[] types = new Type[n];
	    Type t = null;

	    for (int i = 0; i < n; i++) {
		IntPtr op = Runtime.PyTuple_GetItem(args, i);
		ClassBase cb = GetManagedObject(op) as ClassBase;
		t = (cb != null) ? cb.type : Converter.GetTypeByAlias(op);
		if (t == null) {
		    if (free) Runtime.Decref(args); 
		    return Exceptions.RaiseTypeError("type expected");
		}
		types[i] = t;
	    }

	    if (free) {
		Runtime.Decref(args);
	    }

	    t =cls.type.MakeGenericType(types);
	    ManagedType c = (ManagedType)ClassManager.GetClass(t);
	    Runtime.Incref(c.pyHandle);
	    return c.pyHandle;
	}

    }

}
