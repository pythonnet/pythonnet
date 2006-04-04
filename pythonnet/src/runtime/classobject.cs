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

using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Python.Runtime {

    /// <summary>
    /// Managed class that provides the implementation for reflected types.
    /// Managed classes and value types are represented in Python by actual 
    /// Python type objects. Each of those type objects is associated with 
    /// an instance of ClassObject, which provides its implementation.
    /// </summary>

    internal class ClassObject : ClassBase {

	ConstructorBinder binder;

	internal ClassObject(Type tp) : base(tp) {
	    ConstructorInfo[] ctors = type.GetConstructors();
	    binder = new ConstructorBinder();

	    for (int i = 0; i < ctors.Length; i++) {
		binder.AddMethod(ctors[i]);
	    }
	}


	//====================================================================
	// Helper to get docstring from reflected constructor info.
	//====================================================================

	internal IntPtr GetDocString() {
	    MethodBase[] methods = binder.GetMethods();
	    string str = "";
	    for (int i = 0; i < methods.Length; i++) {
		if (str.Length > 0)
		    str += Environment.NewLine;
		str += methods[i].ToString();
	    }
	    return Runtime.PyString_FromString(str);
	}


	//====================================================================
	// Implements __new__ for reflected classes and value types.
	//====================================================================

	[CallConvCdecl()]
	public static IntPtr tp_new(IntPtr tp, IntPtr args, IntPtr kw) {

	    ClassObject self = GetManagedObject(tp) as ClassObject;

	    // Sanity check: this ensures a graceful error if someone does
	    // something intentially wrong like use the managed metatype for
	    // a class that is not really derived from a managed class.

	    if (self == null) {
		return Exceptions.RaiseTypeError("invalid object");
	    }
	    
	    Type type = self.type;

	    // Primitive types do not have constructors, but they look like
	    // they do from Python. If the ClassObject represents one of the 
	    // convertible primitive types, just convert the arg directly.

	    if (type.IsPrimitive || type == typeof(String)) {
		if (Runtime.PyTuple_Size(args) != 1) {
		    Exceptions.SetError(Exceptions.TypeError, 
			       "no constructors match given arguments"
			       );
		    return IntPtr.Zero;
		}

		IntPtr op = Runtime.PyTuple_GetItem(args, 0);
		Object result;

		if (!Converter.ToManaged(op, type, out result, true)) {
		    return IntPtr.Zero;
		}

		return CLRObject.GetInstHandle(result, tp);
	    }

	    if (type.IsAbstract) {
		Exceptions.SetError(Exceptions.TypeError, 
			   "cannot instantiate abstract class"
			   );
		return IntPtr.Zero;
	    }

	    if (type.IsEnum) {
		Exceptions.SetError(Exceptions.TypeError, 
			   "cannot instantiate enumeration"
			   );
		return IntPtr.Zero;
	    }

	    Object obj = self.binder.InvokeRaw(IntPtr.Zero, args, kw);
	    if (obj == null) {
		// It is possible for __new__ to be invoked on construction
		// of a Python subclass of a managed class, so args may
		// reflect more args than are required to instantiate the
		// class. So if we cant find a ctor that matches, we'll see
		// if there is a default constructor and, if so, assume that
		// any extra args are intended for the subclass' __init__.

		IntPtr eargs = Runtime.PyTuple_New(0);
		obj = self.binder.InvokeRaw(IntPtr.Zero, eargs, kw);
		Runtime.Decref(eargs);
		if (obj == null) {
		    return IntPtr.Zero;
		}
	    }

	    return CLRObject.GetInstHandle(obj, tp);
	}


	//====================================================================
	// Implements __getitem__ for reflected classes and value types.
	//====================================================================

	[CallConvCdecl()]
	public static IntPtr mp_subscript(IntPtr ob, IntPtr idx) {
	    ManagedType self = GetManagedObject(ob);
	    IntPtr tp = Runtime.PyObject_TYPE(ob);
	    ClassBase cls = (ClassBase)GetManagedObject(tp);

	    if (cls.indexer == null || !cls.indexer.CanGet) {
		Exceptions.SetError(Exceptions.TypeError, 
				    "unindexable object"
				    );
		return IntPtr.Zero;
	    }

	    // Arg may be a tuple in the case of an indexer with multiple
	    // parameters. If so, use it directly, else make a new tuple
	    // with the index arg (method binders expect arg tuples).

	    IntPtr args = idx;
	    bool free = false;

	    if (!Runtime.PyTuple_Check(idx)) {
		args = Runtime.PyTuple_New(1);
		Runtime.Incref(idx);
		Runtime.PyTuple_SetItem(args, 0, idx);
		free = true;
	    }

	    IntPtr value = IntPtr.Zero;

	    try {
		value = cls.indexer.GetItem(ob, args);
	    }
	    finally {
		if (free) {
		    Runtime.Decref(args);
		}
	    }
	    return value;
	}


	//====================================================================
	// Implements __setitem__ for reflected classes and value types.
	//====================================================================

	[CallConvCdecl()]
	public static int mp_ass_subscript(IntPtr ob, IntPtr idx, IntPtr v) {
	    ManagedType self = GetManagedObject(ob);
	    IntPtr tp = Runtime.PyObject_TYPE(ob);
	    ClassBase cls = (ClassBase)GetManagedObject(tp);

	    if (cls.indexer == null || !cls.indexer.CanSet) {
		Exceptions.SetError(Exceptions.TypeError, 
				    "object doesn't support item assignment x"
				    );
		return -1;
	    }

	    // Arg may be a tuple in the case of an indexer with multiple
	    // parameters. If so, use it directly, else make a new tuple
	    // with the index arg (method binders expect arg tuples).

	    IntPtr args = idx;
	    bool free = false;

	    if (!Runtime.PyTuple_Check(idx)) {
		args = Runtime.PyTuple_New(1);
		Runtime.Incref(idx);
		Runtime.PyTuple_SetItem(args, 0, idx);
		free = true;
	    }

	    int i = Runtime.PyTuple_Size(args);
	    IntPtr real = Runtime.PyTuple_New(i + 1);
	    for (int n = 0; n < i; n++) {
		IntPtr item = Runtime.PyTuple_GetItem(args, n);
		Runtime.Incref(item);
		Runtime.PyTuple_SetItem(real, n, item);
	    }
	    Runtime.Incref(v);
	    Runtime.PyTuple_SetItem(real, i, v);

	    try {
		cls.indexer.SetItem(ob, real);
	    }
	    finally {
		Runtime.Decref(real);

		if (free) {
		    Runtime.Decref(args);
		}
	    }

	    if (Exceptions.ErrorOccurred()) {
		return -1;
	    }

	    return 0;
	}


	//====================================================================
	// This is a hack. Generally, no managed class is considered callable
	// from Python - with the exception of System.Delegate. It is useful
	// to be able to call a System.Delegate instance directly, especially
	// when working with multicast delegates.
	//====================================================================

	[CallConvCdecl()]
	public static IntPtr tp_call(IntPtr ob, IntPtr args, IntPtr kw) {
	    ManagedType self = GetManagedObject(ob);
	    IntPtr tp = Runtime.PyObject_TYPE(ob);
	    ClassBase cb = (ClassBase)GetManagedObject(tp);

	    if (cb.type != typeof(System.Delegate)) {
		Exceptions.SetError(Exceptions.TypeError, 
				    "object is not callable");
		return IntPtr.Zero;
	    }

	    CLRObject co = (CLRObject)ManagedType.GetManagedObject(ob);
	    Delegate d = co.inst as Delegate;
	    BindingFlags flags = BindingFlags.Public | 
		                 BindingFlags.NonPublic |
		                 BindingFlags.Instance |
		                 BindingFlags.Static;

	    MethodInfo method = d.GetType().GetMethod("Invoke", flags);
  	    MethodBinder binder = new MethodBinder(method);
 	    return binder.Invoke(ob, args, kw);
	}


    }	

}
