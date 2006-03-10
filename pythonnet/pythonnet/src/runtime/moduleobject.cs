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
using System.Runtime.InteropServices;
using System.Collections.Specialized;
using System.Collections;
using System.Reflection;

namespace Python.Runtime {

    // TODO: decide whether to support __all__ and / or whether to impl
    // a 'preload' method to force names to be pre-loaded.

    //========================================================================
    // Implements a Python type that provides access to CLR namespaces. The 
    // type behaves like a Python module, and can contain other sub-modules.
    //========================================================================

    internal class ModuleObject : ExtensionType {

	string moduleName;
	string _namespace;
	Hashtable cache;
	static bool hacked;
	IntPtr dict;

	public ModuleObject(string name) : base() {
	    moduleName = (name == String.Empty) ? "CLR" : "CLR." + name;
	    cache = new Hashtable();
	    _namespace = name;

	    dict = Runtime.PyDict_New();
	    IntPtr pyname = Runtime.PyString_FromString(moduleName);
	    Runtime.PyDict_SetItemString(dict, "__name__", pyname);
	    Runtime.PyDict_SetItemString(dict, "__file__", Runtime.PyNone);
	    Runtime.PyDict_SetItemString(dict, "__doc__", Runtime.PyNone);
	    Runtime.Decref(pyname);

	    Marshal.WriteIntPtr(this.pyHandle, ObjectOffset.ob_dict, dict);

	    // This hackery is required in order to allow a plain Python to
	    // import the managed runtime via the CLR bootstrapper module. 
	    // The standard Python machinery in control at the time of the
	    // import requires the module to pass PyModule_Check. :(

	    if (!hacked) {
		IntPtr type = this.tpHandle;
		IntPtr mro = Marshal.ReadIntPtr(type, TypeOffset.tp_mro);
		IntPtr ext = Runtime.ExtendTuple(mro, Runtime.PyModuleType);
		Marshal.WriteIntPtr(type, TypeOffset.tp_mro, ext);
		Runtime.Decref(mro);
		hacked = true;
	    }

	}

	public string ModuleName {
	    get { 
		return moduleName; 
	    }
	}


	//===================================================================
	// Returns a ClassBase object representing a type that appears in
	// this module's namespace or a ModuleObject representing a child 
	// namespace (or null if the name is not found). This method does
	// not increment the Python refcount of the returned object.
	//===================================================================

	public ManagedType GetAttribute(string name) {
	    Object ob = this.cache[name];
	    if (ob != null) {
		return (ManagedType) ob;
	    }

	    string qname = (_namespace == String.Empty) ? name : 
		            _namespace + "." + name;

	    ModuleObject m;
	    ClassBase c;

	    // If the fully-qualified name of the requested attribute is 
	    // a namespace exported by a currently loaded assembly, return 
	    // a new ModuleObject representing that namespace.

	    if (AssemblyManager.IsValidNamespace(qname)) {
		m = new ModuleObject(qname);
		StoreAttribute(name, m);
		return (ManagedType) m;
	    }

	    // Look for a type in the current namespace. Note that this 
	    // includes types, delegates, enums, interfaces and structs.
	    // Only public namespace members are exposed to Python.

	    Type type = AssemblyManager.LookupType(qname);
	    if (type != null) {
		if (!type.IsPublic) {
		    return null;
		}
		c = ClassManager.GetClass(type);
		StoreAttribute(name, c);
		return (ManagedType) c;
	    }

	    // This is a little repetitive, but it ensures that the right
	    // thing happens with implicit assembly loading at a reasonable
	    // cost. Ask the AssemblyManager to do implicit loading for each 
	    // of the steps in the qualified name, then try it again.

	    if (AssemblyManager.LoadImplicit(qname)) {
		if (AssemblyManager.IsValidNamespace(qname)) {
		    m = new ModuleObject(qname);
		    StoreAttribute(name, m);
		    return (ManagedType) m;
		}

		type = AssemblyManager.LookupType(qname);
		if (type != null) {
		    if (!type.IsPublic) {
			return null;
		    }
		    c = ClassManager.GetClass(type);
		    StoreAttribute(name, c);
		    return (ManagedType) c;
		}
	    }

	    return null;
	}


	//===================================================================
	// Stores an attribute in the instance dict for future lookups.
 	//===================================================================

	private void StoreAttribute(string name, ManagedType ob) {
	    Runtime.PyDict_SetItemString(dict, name, ob.pyHandle);
	    cache[name] = ob;
	}


	[PythonMethod]
	public static IntPtr _preload(IntPtr ob, IntPtr args, IntPtr kw) {
	    ModuleObject self = (ModuleObject)GetManagedObject(ob);

	    string module_ns = self._namespace;
	    AppDomain domain = AppDomain.CurrentDomain;
	    Assembly[] assemblies = domain.GetAssemblies();
	    for (int i = 0; i < assemblies.Length; i++) {
		Assembly assembly = assemblies[i];
		Type[] types = assembly.GetTypes();
		for (int n = 0; n < types.Length; n++) {
		    Type type = types[n];

		    string ns = type.Namespace;
		    if ((ns != null) && (ns == module_ns)) {
			if (type.IsPublic) {
			    ClassBase c = ClassManager.GetClass(type);
			    self.StoreAttribute(type.Name, c);
			}
		    }
		}
	    }
	    Runtime.Incref(Runtime.PyNone);
	    return Runtime.PyNone;
	}


	//====================================================================
	// ModuleObject __getattribute__ implementation. Module attributes
	// are always either classes or sub-modules representing subordinate 
	// namespaces. CLR modules implement a lazy pattern - the sub-modules
	// and classes are created when accessed and cached for future use.
	//====================================================================

	[CallConvCdecl()]
	public static IntPtr tp_getattro(IntPtr ob, IntPtr key) {
	    ModuleObject self = (ModuleObject)GetManagedObject(ob);

	    if (!Runtime.PyString_Check(key)) {
		Exceptions.SetError(Exceptions.TypeError, "string expected");
		return IntPtr.Zero;
	    }

	    IntPtr op = Runtime.PyDict_GetItem(self.dict, key);
	    if (op != IntPtr.Zero) {
		Runtime.Incref(op);
		return op;
	    }
 
	    string name = Runtime.GetManagedString(key);
	    if (name == "__dict__") {
		Runtime.Incref(self.dict);
		return self.dict;
	    }

	    ManagedType attr = self.GetAttribute(name);

	    if (attr == null) {
		Exceptions.SetError(Exceptions.AttributeError, name);
		return IntPtr.Zero;		
	    }

	    // XXX - hack required to recognize exception types. These types
	    // may need to be wrapped in old-style class wrappers in versions
	    // of Python where new-style classes cannot be used as exceptions.

	    if (Runtime.wrap_exceptions) {
		if (attr is ClassBase) {
		    ClassBase c = attr as ClassBase;
		    if (c.is_exception) {
			IntPtr p = attr.pyHandle;
			IntPtr r = Exceptions.GetExceptionClassWrapper(p);
			Runtime.PyDict_SetItemString(self.dict, name, r);
			Runtime.Incref(r);
			return r;
		    }
		}
	    }

	    Runtime.Incref(attr.pyHandle);
	    return attr.pyHandle;
	}

	//====================================================================
	// ModuleObject __repr__ implementation.
	//====================================================================

	[CallConvCdecl()]
	public static IntPtr tp_repr(IntPtr ob) {
	    ModuleObject self = (ModuleObject)GetManagedObject(ob);
	    string s = String.Format("<module '{0}'>", self.moduleName);
	    return Runtime.PyString_FromString(s);
	}

    }


}
