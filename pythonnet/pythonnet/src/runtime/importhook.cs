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

namespace Python.Runtime {

    //========================================================================
    // Implements the "import hook" used to integrate Python with the CLR.
    //========================================================================

    internal class ImportHook {

	static IntPtr py_import;
	static ModuleObject root;
	static MethodWrapper hook;

	//===================================================================
	// Initialization performed on startup of the Python runtime.
	//===================================================================

	internal static void Initialize() {

	    // Initialize the Python <--> CLR module hook. We replace the
	    // built-in Python __import__ with our own. This isn't ideal, 
	    // but it provides the most "Pythonic" way of dealing with CLR
	    // modules (Python doesn't provide a way to emulate packages).

	    IntPtr dict = Runtime.PyImport_GetModuleDict();
	    IntPtr mod = Runtime.PyDict_GetItemString(dict, "__builtin__");
	    py_import = Runtime.PyObject_GetAttrString(mod, "__import__");


  	    hook = new MethodWrapper(typeof(ImportHook), "__import__");

  	    Runtime.PyObject_SetAttrString(mod, "__import__", hook.ptr);

	    Runtime.Decref(hook.ptr);

	    root = new ModuleObject("");

	    Runtime.PyDict_SetItemString(dict, "CLR", root.pyHandle);
	}


	//===================================================================
	// Cleanup resources upon shutdown of the Python runtime.
	//===================================================================

	internal static void Shutdown() {
	    Runtime.Decref(root.pyHandle);
	    Runtime.Decref(py_import);
	}


	//===================================================================
	// The actual import hook that ties Python to the managed world.
	//===================================================================

	[CallConvCdecl()]
	public static IntPtr __import__(IntPtr self, IntPtr args, IntPtr kw) {

	    // Replacement for the builtin __import__. The original import
	    // hook is saved as this.importFunc. This version handles CLR 
	    // import and defers to the normal builtin for everything else.

	    int num_args = Runtime.PyTuple_Size(args);

	    if (num_args < 1) {
		Exceptions.SetError(
			   Exceptions.TypeError, 
			   "__import__() takes at least 1 argument (0 given)"
			   );
		return IntPtr.Zero;
	    }

	    // borrowed reference
	    IntPtr py_mod_name = Runtime.PyTuple_GetItem(args, 0);

	    if ((py_mod_name == IntPtr.Zero) ||
               (!Runtime.IsStringType(py_mod_name))) {
		Exceptions.SetError(Exceptions.TypeError, "string expected");
		return IntPtr.Zero;
	    }

	    // If not a CLR module, defer to the standard Python import.
	    // Could use Python here to avoid a string conversion.

	    string mod_name = Runtime.GetManagedString(py_mod_name);

	    if (!(mod_name.StartsWith("CLR.") || mod_name == "CLR")) {
		return Runtime.PyObject_Call(py_import, args, kw);
	    }

	    // Check whether the import is of the form 'from x import y'.
	    // This determines whether we return the head or tail module.

	    bool from_list = false;
	    if (num_args >= 4) {
		IntPtr fromList = Runtime.PyTuple_GetItem(args, 3);
		if ((fromList != IntPtr.Zero) && 
		    (Runtime.PyObject_IsTrue(fromList) == 1)) {
		    from_list = true;
		}
	    }

	    // See if sys.modules for this interpreter already has the
	    // requested module. If so, just return the exising module.

	    IntPtr sys_modules = Runtime.PyImport_GetModuleDict();
	    IntPtr module = Runtime.PyDict_GetItem(sys_modules, py_mod_name);

	    if (module != IntPtr.Zero) {
		if (from_list) {
		    Runtime.Incref(module);
		    return module;
		}
		Runtime.Incref(root.pyHandle);
		return root.pyHandle;
	    }

	    // Now we know we are looking for a CLR module and are likely
	    // going to have to ask the AssemblyManager. The assembly mgr
	    // tries really hard not to use Python objects or APIs, because 
	    // parts of it can run recursively and on strange threads, etc.
	    // 
	    // It does need an opportunity from time to time to check to 
	    // see if sys.path has changed, in a context that is safe. Here
	    // we know we have the GIL, so we'll let it update if needed.

	    AssemblyManager.UpdatePath();

	    // Special case handling: if the qualified module name would
	    // cause an implicit assembly load, we need to do that first
	    // to make sure that each of the steps in the qualified name
	    // is recognized as a valid namespace. Otherwise the import
	    // process can encounter unknown namespaces before it gets to
	    // load the assembly that would make them valid.

	    if (mod_name.StartsWith("CLR.")) {
		string real_name = mod_name.Substring(4);
		AssemblyManager.LoadImplicit(real_name);
	    }

	    // Traverse the qualified module name to get the requested
	    // module and place references in sys.modules as we go.

	    string[] names = mod_name.Split('.');
	    ModuleObject tail = root;

	    for (int i = 0; i < names.Length; i++) {
		string name = names[i];
		if (name == "CLR") {
		    tail = root;
		}
		else {
		    ManagedType mt = tail.GetAttribute(name);
		    if (!(mt is ModuleObject)) {
			string error = String.Format("No module named {0}",
						     name
						     );
			Exceptions.SetError(Exceptions.ImportError, error); 
			return IntPtr.Zero;
		    }
		    tail = (ModuleObject) mt;
		}

		Runtime.PyDict_SetItemString(
			       sys_modules, tail.ModuleName, tail.pyHandle
			       );
	    }

	    ModuleObject mod = from_list ? tail : root;
	    Runtime.Incref(mod.pyHandle);
	    return mod.pyHandle;
	}

    }


}
