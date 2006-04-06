// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using System;
using System.IO;
using System.Collections;
using System.Collections.Specialized;
using System.Reflection;
using System.Reflection.Emit;

namespace Python.Runtime {

    /// <summary>
    /// The AssemblyManager maintains information about the assemblies and 
    /// namespaces that have been loaded, and provides a simplified internal 
    /// interface for finding and obtaining Type objects by qualified names.
    /// </summary>

    internal class AssemblyManager {

	static StringCollection pypath;
	static AssemblyLoadEventHandler lh;
	static ResolveEventHandler rh;
	static Hashtable namespaces;
	static ArrayList assemblies;	
	static Hashtable probed;

	private AssemblyManager() {}

	//===================================================================
	// Initialization performed on startup of the Python runtime.
	//===================================================================

	internal static void Initialize() {
	    pypath = new StringCollection();
	    namespaces = new Hashtable();
	    assemblies = new ArrayList();
	    probed = new Hashtable();

	    AppDomain domain = AppDomain.CurrentDomain;

	    lh = new AssemblyLoadEventHandler(AssemblyLoadHandler);
	    domain.AssemblyLoad += lh;

	    rh = new ResolveEventHandler(ResolveHandler);	
	    domain.AssemblyResolve += rh;

	    Assembly[] items = domain.GetAssemblies();
	    for (int i = 0; i < items.Length; i++) {
		Assembly a = items[i];
		assemblies.Add(a);
		ScanAssembly(a);
	    }
	}


	//===================================================================
	// Cleanup resources upon shutdown of the Python runtime.
	//===================================================================

	internal static void Shutdown() {
	    AppDomain domain = AppDomain.CurrentDomain;
	    domain.AssemblyLoad -= lh;
	    domain.AssemblyResolve -= rh;
	}


	//===================================================================
	// Event handler for assembly load events. At the time the Python 
	// runtime loads, we scan the app domain to map the assemblies that
	// are loaded at the time. We also have to register this event handler
	// so that we can know about assemblies that get loaded after the 
	// Python runtime is initialized.
	//===================================================================

	static void AssemblyLoadHandler(Object ob, AssemblyLoadEventArgs args){
	    Assembly assembly = args.LoadedAssembly;
	    assemblies.Add(assembly);
	    ScanAssembly(assembly);
	}


	//===================================================================
	// Event handler for assembly resolve events. This is needed because
	// we augment the assembly search path with the PYTHONPATH when we
	// load an assembly from Python. Because of that, we need to listen
	// for failed loads, because they might be dependencies of something
	// we loaded from Python which also needs to be found on PYTHONPATH.
	//===================================================================

	static Assembly ResolveHandler(Object ob, ResolveEventArgs args){
	    string name = args.Name.ToLower();
	    for (int i = 0; i < assemblies.Count; i++) {
		Assembly a = (Assembly)assemblies[i];
		string full = a.FullName.ToLower();
		if (full.StartsWith(name)) {
		    return a;
		}
	    }
	    Assembly ao = LoadAssemblyPath(args.Name);
	    return ao;
	}


	//===================================================================
	// We __really__ want to avoid using Python objects or APIs when
	// probing for assemblies to load, since our ResolveHandler may be 
	// called in contexts where we don't have the Python GIL and can't
	// even safely try to get it without risking a deadlock ;(
	//
	// To work around that, we update a managed copy of sys.path (which 
	// is the main thing we care about) when UpdatePath is called. The
	// import hook calls this whenever it knows its about to use the
	// assembly manager, which lets us keep up with changes to sys.path
	// in a relatively lightweight and low-overhead way.
	//===================================================================

	internal static void UpdatePath() {
	    IntPtr list = Runtime.PySys_GetObject("path");
	    int count = Runtime.PyList_Size(list);
	    if (count != pypath.Count) {
		pypath.Clear();
		probed.Clear();
		for (int i = 0; i < count; i++) {
		    IntPtr item = Runtime.PyList_GetItem(list, i);
		    string path = Runtime.GetManagedString(item);
		    if (path != null) {
			pypath.Add(path);
		    }
		}
	    }
	}


	//===================================================================
	// Given an assembly name, try to find this assembly file using the
	// PYTHONPATH. If not found, return null to indicate implicit load
	// using standard load semantics (app base directory then GAC, etc.)
	//===================================================================

	static string FindAssembly(string name) {
	    char sep = Path.DirectorySeparatorChar;
	    string path;
	    string temp;

	    for (int i = 0; i < pypath.Count; i++) {
		string head = pypath[i];
		if (head == null || head.Length == 0) {
		    path = name;
		}
		else {
		    path = head + sep + name;
		}

		temp = path + ".dll";
		if (File.Exists(temp)) {
		    return temp;
		}
		temp = path + ".exe";
		if (File.Exists(temp)) {
		    return temp;
		}
	    }
	    return null;
	}


	//===================================================================
	// Loads an assembly from the application directory or the GAC
	// given a simple assembly name. Returns the assembly if loaded.
	//===================================================================

	public static Assembly LoadAssembly(string name) {
	    Assembly assembly = null;
	    try {
		assembly = Assembly.LoadWithPartialName(name);
	    }
	    catch {
	    }
	    return assembly;
	}


	//===================================================================
	// Loads an assembly using an augmented search path (the python path).
	//===================================================================

	public static Assembly LoadAssemblyPath(string name) {
	    string path = FindAssembly(name);
	    Assembly assembly = null;
	    if (path != null) {
		try   { assembly = Assembly.LoadFrom(path); }
		catch {}
	    }
	    return assembly;
	}


	//===================================================================
	// Given a qualified name of the form A.B.C.D, attempt to load 
	// an assembly named after each of A.B.C.D, A.B.C, A.B, A. This
	// will only actually probe for the assembly once for each unique
	// namespace. Returns true if any assemblies were loaded.
	//===================================================================

	public static bool LoadImplicit(string name) {
	    string[] names = name.Split('.');
	    bool loaded = false;
	    string s = "";
	    for (int i = 0; i < names.Length; i++) {
		s = (i == 0) ? names[0] : s + "." + names[i];
		if (probed[s] == null) {
		    if (LoadAssemblyPath(s) != null){
			loaded = true;
		    }
		    else if (LoadAssembly(s) != null) {
			loaded = true;
		    }
		    probed[s] = 1;
		}
	    }
	    return loaded;
	}


	//===================================================================
	// Scans an assembly for exported namespaces, adding them to the
	// mapping of valid namespaces. Note that for a given namespace
	// a.b.c.d, each of a, a.b, a.b.c and a.b.c.d are considered to 
	// be valid namespaces (to better match Python import semantics).
	//===================================================================

	static void ScanAssembly(Assembly assembly) {

	    // A couple of things we want to do here: first, we want to
	    // gather a list of all of the namespaces contributed to by
	    // the assembly. Since we have to rifle through all of the
	    // types in the assembly anyway, we also build up a running
	    // list of 'odd names' like generic names so that we can map
	    // them appropriately later while still being lazy about 
	    // type lookup and instantiation.

	    Type[] types = assembly.GetTypes();
	    for (int i = 0; i < types.Length; i++) {
		Type t = types[i];
		string ns = t.Namespace != null ? t.Namespace : "";
		if ((ns != null) && (!namespaces.ContainsKey(ns))) {
		    string[] names = ns.Split('.');
		    string s = "";
		    for (int n = 0; n < names.Length; n++) {
			s = (n == 0) ? names[0] : s + "." + names[n];
			if (!namespaces.ContainsKey(s)) {
			    namespaces.Add(s, new Hashtable());
			}
		    }
		}

		Hashtable asm = namespaces[ns] as Hashtable;
		if (ns != null && !asm.ContainsKey(assembly)) {
		    asm.Add(assembly, String.Empty);
		}	    
	    }
	}


	//===================================================================
	// Returns true if the given qualified name matches a namespace
	// exported by an assembly loaded in the current app domain.
	//===================================================================

	public static bool IsValidNamespace(string name) {
	    return namespaces.ContainsKey(name);
	}


	//===================================================================
	// Returns the current list of valid names for the input namespace.
	//===================================================================

	public static StringCollection GetNames(string nsname) {
	    StringCollection names = new StringCollection();
	    if (namespaces.ContainsKey(nsname)) {
		Hashtable asm = namespaces[nsname] as Hashtable;
		foreach (Object o in asm.Keys) {
		    Assembly a = o as Assembly;
		    Type[] types = a.GetTypes();
		    for (int i = 0; i < types.Length; i++) {
			Type t = types[i];
			if (t.Namespace == nsname) {
			    names.Add(t.Name);
			}
		    }
		}
		int nslen = nsname.Length;
		foreach (object n in namespaces.Keys) {
		    string key = n as string;
		    if (key.Length > nslen && key.StartsWith(nsname)) {
			string tail = key.Substring(nslen);
			if (key.IndexOf('.') == -1) {
			    names.Add(key);
			} 
		    }
		}
	    }
	    return names;
	}

	//===================================================================
	// Returns the System.Type object for a given qualified name,
	// looking in the currently loaded assemblies for the named
	// type. Returns null if the named type cannot be found.
	//===================================================================

	public static Type LookupType(string qualifiedName) {
	    for (int i = 0; i < assemblies.Count; i++) {
		Assembly assembly = (Assembly)assemblies[i];
		Type type = assembly.GetType(qualifiedName);
		if (type != null) {
		    return type;
		}
	    }
	    return null;
	}

    }


}
