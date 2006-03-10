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
using System.Collections;
using System.Reflection;

namespace Python.Runtime {

    //========================================================================
    // A MethodBinder encapsulates information about a (possibly overloaded) 
    // managed method, and is responsible for selecting the right method given
    // a set of Python arguments. This is also used as a base class for the
    // ConstructorBinder, a minor variation used to invoke constructors.
    //========================================================================

    internal class MethodBinder {

	public ArrayList list;
	public MethodBase[] methods;
	public bool init = false;

	internal MethodBinder () {
	    this.list = new ArrayList();
	}
	
	internal MethodBinder(MethodInfo mi) : base () {
	    this.list = new ArrayList();
	    this.list.Add(mi);
	}

	public int Count {
	    get { return this.list.Count; }
	}

	internal void AddMethod(MethodBase m) {
	    this.list.Add(m);
	}


	//====================================================================
	// Return the array of MethodInfo for this method. The result array
	// is arranged in order of precendence (done lazily to avoid doing it
	// at all for methods that are never called).
	//====================================================================

	internal MethodBase[] GetMethods() {
	    if (!init) {
		// I'm sure this could be made more efficient.
		list.Sort(new MethodSorter());
		methods = (MethodBase[])list.ToArray(typeof(MethodBase));
		init = true;
	    }
	    return methods;
	}


	//====================================================================
	// Precedence algorithm largely lifted from jython - the concerns are
	// generally the same so we'll start w/this and tweak as necessary.
	//====================================================================

	internal static int GetPrecedence(MethodBase mi) {
	    ParameterInfo[] pi = mi.GetParameters();
	    int val = mi.IsStatic ? 3000 : 0;
	    int num = pi.Length;

	    for (int i = 0; i < num; i++) {
		val += ArgPrecedence(pi[i].ParameterType);
	    }

	    return val;
	}


	//====================================================================
	// Return a precedence value for a particular Type object.
	//====================================================================

	internal static int ArgPrecedence(Type t) {
	    Type objectType = typeof(Object);
	    if (t == objectType) return 3000;

	    TypeCode tc = Type.GetTypeCode(t);
	    if (tc == TypeCode.Object) return 1;
	    if (tc == TypeCode.UInt64) return 10;
	    if (tc == TypeCode.UInt32) return 11;
	    if (tc == TypeCode.UInt16) return 12;
	    if (tc == TypeCode.Int64) return 13;
	    if (tc == TypeCode.Int32) return 14;
	    if (tc == TypeCode.Int16) return 15;
	    if (tc == TypeCode.Char) return 16;
	    if (tc == TypeCode.SByte) return 17;
	    if (tc == TypeCode.Byte) return 18;
	    if (tc == TypeCode.Single) return 20;
	    if (tc == TypeCode.Double) return 21;
	    if (tc == TypeCode.String) return 30;
	    if (tc == TypeCode.Boolean) return 40;

	    if (t.IsArray) {
		Type e = t.GetElementType();
		if (e == objectType)
		    return 2500;
		return 100 + ArgPrecedence(e);
	    }

	    return 2000;
	}



	//====================================================================
	// Bind the given Python instance and arguments to a particular method
	// overload and return a structure that contains the converted Python
	// instance, converted arguments and the correct method to call.
	//====================================================================

	internal Binding Bind(IntPtr inst, IntPtr args, IntPtr kw) {
	    // loop to find match, return invoker w/ or /wo error
	    int nargs = Runtime.PyTuple_Size(args);
	    object arg;

	    MethodBase[] _methods = GetMethods();

	    for (int i = 0; i < _methods.Length; i++) {
		MethodBase mi = _methods[i];
		ParameterInfo[] pi = mi.GetParameters();
		int count = pi.Length;
		int outs = 0;

		if ( nargs == count ) {
		    Object[] margs = new Object[count];

		    for (int n = 0; n < count; n++) {
			IntPtr op = Runtime.PyTuple_GetItem(args, n);
			Type type = pi[n].ParameterType;
			if (pi[n].IsOut || type.IsByRef) {
			    outs++;
			}

			if (!Converter.ToManaged(op, type, out arg, false)) {
			    Exceptions.Clear();
			    margs = null;
			    break;
			}
			margs[n] = arg;
		    }
		    
		    if (margs == null) {
			continue;
		    }

		    Object target = null;
		    if ((!mi.IsStatic) && (inst != IntPtr.Zero)) {
			CLRObject co = (CLRObject)ManagedType.GetManagedObject(
                                                  inst
						  );
			target = co.inst;
		    }

		    return new Binding(mi, target, margs, outs);
		}

	    }
	    return null;
	}


	internal virtual IntPtr Invoke(IntPtr inst, IntPtr args, IntPtr kw) {
	    Binding binding = this.Bind(inst, args, kw);
	    Object result;

	    if (binding == null) {
		Exceptions.SetError(Exceptions.TypeError, 
				    "no method matches given arguments"
				    );
		return IntPtr.Zero;
	    }

	    IntPtr ts = PythonEngine.BeginAllowThreads();
	    try {
		result = binding.info.Invoke(binding.inst, 
					     BindingFlags.Default, 
					     null, 
					     binding.args, 
					     null);
	    }
	    catch (Exception e) {
		if (e.InnerException != null) {
		    e = e.InnerException;
		}
		PythonEngine.EndAllowThreads(ts);
		Exceptions.SetError(e);
		return IntPtr.Zero;
	    }

	    PythonEngine.EndAllowThreads(ts);

	    // If there are out parameters, we return a tuple containing
	    // the result followed by the out parameters. If there is only
	    // one out parameter and the return type of the method is void,
	    // we return the out parameter as the result to Python (for
	    // code compatibility with ironpython).

	    MethodInfo mi = (MethodInfo)binding.info;

	    if ((binding.outs == 1) && (mi.ReturnType == typeof(void))) {

	    }

	    if (binding.outs > 0) {
		ParameterInfo[] pi = mi.GetParameters();
		int c = pi.Length;
		int n = 0;

		IntPtr t = Runtime.PyTuple_New(binding.outs + 1);
		IntPtr v = Converter.ToPython(result, mi.ReturnType);
		Runtime.PyTuple_SetItem(t, n, v);
		n++;

		for (int i=0; i < c; i++) {
		    Type pt = pi[i].ParameterType;
		    if (pi[i].IsOut || pt.IsByRef) {
			v = Converter.ToPython(binding.args[i], pt);
			Runtime.PyTuple_SetItem(t, n, v);
			n++;
		    }
		}

		if ((binding.outs == 1) && (mi.ReturnType == typeof(void))) {
		    v = Runtime.PyTuple_GetItem(t, 1);
		    Runtime.Incref(v);
		    Runtime.Decref(t);
		    return v;
		}

		return t;
	    }

	    return Converter.ToPython(result, mi.ReturnType);
	}

    }



    //========================================================================
    // Utility class to sort method info by parameter type precedence.
    //========================================================================

    internal class MethodSorter : IComparer {

	int IComparer.Compare(Object m1, Object m2) {
	    int p1 = MethodBinder.GetPrecedence((MethodBase)m1);
	    int p2 = MethodBinder.GetPrecedence((MethodBase)m2);
	    if (p1 < p2) return -1;
	    if (p1 > p2) return 1;
	    return 0;
	}

    }


    //========================================================================
    // A Binding is a utility instance that bundles together a MethodInfo
    // representing a method to call, a (possibly null) target instance for
    // the call, and the arguments for the call (all as managed values).
    //========================================================================

    internal class Binding {

	public MethodBase info;
	public Object[] args;
	public Object inst;
	public int outs;

	internal Binding(MethodBase info, Object inst, Object[] args, 
			 int outs) {
	    this.info = info;
	    this.inst = inst;
	    this.args = args;
	    this.outs = outs;
	}

    }

}
