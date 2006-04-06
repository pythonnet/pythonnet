// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

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
	static int preload;

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
	    preload = -1;
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
	    // hook is saved as this.py_import. This version handles CLR 
	    // import and defers to the normal builtin for everything else.

	    int num_args = Runtime.PyTuple_Size(args);
	    if (num_args < 1) {
		return Exceptions.RaiseTypeError(
		       "__import__() takes at least 1 argument (0 given)"
		       );
	    }

	    // borrowed reference
	    IntPtr py_mod_name = Runtime.PyTuple_GetItem(args, 0);
	    if ((py_mod_name == IntPtr.Zero) ||
               (!Runtime.IsStringType(py_mod_name))) {
	        return Exceptions.RaiseTypeError("string expected");
	    }

	    // Check whether the import is of the form 'from x import y'.
	    // This determines whether we return the head or tail module.

	    IntPtr fromList = IntPtr.Zero;
	    bool fromlist = false;
	    if (num_args >= 4) {
		fromList = Runtime.PyTuple_GetItem(args, 3);
		if ((fromList != IntPtr.Zero) && 
		    (Runtime.PyObject_IsTrue(fromList) == 1)) {
		    fromlist = true;
		}
	    }

	    string mod_name = Runtime.GetManagedString(py_mod_name);

	    if (mod_name == "CLR" || mod_name == "clr") {
		Runtime.Incref(root.pyHandle);
		return root.pyHandle;
	    }

	    string realname = mod_name;
	    if (mod_name.StartsWith("CLR.")) {
		realname = mod_name.Substring(4);
	    }

	    string[] names = realname.Split('.');

	    // Now we need to decide if the name refers to a CLR module,
	    // and may have to do an implicit load (for b/w compatibility)
	    // using the AssemblyManager. The assembly manager tries
	    // really hard not to use Python objects or APIs, because 
	    // parts of it can run recursively and on strange threads.
	    // 
	    // It does need an opportunity from time to time to check to 
	    // see if sys.path has changed, in a context that is safe. Here
	    // we know we have the GIL, so we'll let it update if needed.

	    AssemblyManager.UpdatePath();
	    AssemblyManager.LoadImplicit(realname);
	    if (!AssemblyManager.IsValidNamespace(realname)) {
		return Runtime.PyObject_Call(py_import, args, kw);
	    }

	    // See if sys.modules for this interpreter already has the
	    // requested module. If so, just return the exising module.

	    IntPtr modules = Runtime.PyImport_GetModuleDict();
	    IntPtr module = Runtime.PyDict_GetItem(modules, py_mod_name);

	    if (module != IntPtr.Zero) {
		if (fromlist) {
		    Runtime.Incref(module);
		    return module;
		}
		module = Runtime.PyDict_GetItemString(modules, names[0]);
		Runtime.Incref(module);
		return module;
	    }
	    Exceptions.Clear();

	    // Traverse the qualified module name to get the named module
	    // and place references in sys.modules as we go. Note that if
	    // we are running in interactive mode we pre-load the names in 
	    // each module, which is often useful for introspection. If we
	    // are not interactive, we stick to just-in-time creation of
	    // objects at lookup time, which is much more efficient.

	    if (preload < 0) {
		if (Runtime.PySys_GetObject("ps1") != IntPtr.Zero) {
		    preload = 1;
		}
		else {
		    Exceptions.Clear();
		    preload = 0;
		}
	    }

	    ModuleObject head = (mod_name == realname) ? null : root;
	    ModuleObject tail = root;

	    for (int i = 0; i < names.Length; i++) {
		string name = names[i];
		ManagedType mt = tail.GetAttribute(name);
		if (!(mt is ModuleObject)) {
		    string error = String.Format("No module named {0}", name);
		    Exceptions.SetError(Exceptions.ImportError, error); 
		    return IntPtr.Zero;
		}
		if (head == null) {
		    head = (ModuleObject)mt;
		}
		tail = (ModuleObject) mt;
		if (preload == 1) {
		    tail.LoadNames();
		}
		Runtime.PyDict_SetItemString(modules, tail.moduleName, 
					     tail.pyHandle
					     );
	    }

	    ModuleObject mod = fromlist ? tail : head;

	    if (fromlist && Runtime.PySequence_Size(fromList) == 1) {
		IntPtr fp = Runtime.PySequence_GetItem(fromList, 0);
		if ((preload < 1) && Runtime.GetManagedString(fp) == "*") {
		    mod.LoadNames();
		}
		Runtime.Decref(fp);
	    }

	    Runtime.Incref(mod.pyHandle);
	    return mod.pyHandle;
	}

    }


}
