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

namespace Python.Runtime {

    /// <summary>
    /// Provides a managed interface to exceptions thrown by the Python 
    /// runtime.
    /// </summary>

    public class PythonException : System.Exception {

	private IntPtr excType = IntPtr.Zero;
	private IntPtr excValue = IntPtr.Zero;
	private IntPtr excTb = IntPtr.Zero;
	private bool disposed = false;

	public PythonException() : base() {
	    Runtime.PyErr_Fetch(ref excType, ref excValue, ref excTb);
	    Runtime.Incref(excType);
	    Runtime.Incref(excValue);
	    Runtime.Incref(excTb);

	}

	// Ensure that encapsulated Python objects are decref'ed appropriately
	// when the managed exception wrapper is garbage-collected.

	~PythonException() {
	    Dispose();
	}


	/// <summary>
	/// Type Property
	/// </summary>
	///
	/// <remarks>
	/// Returns the exception type as a Python object.
	/// </remarks>

	public IntPtr Type {
	    get {
		return excType;
	    }
	}

	/// <summary>
	/// Value Property
	/// </summary>
	///
	/// <remarks>
	/// Returns the exception value as a Python object.
	/// </remarks>

	public IntPtr Value {
	    get {
		return excValue;
	    }
	}

	/// <summary>
	/// Traceback Property
	/// </summary>
	///
	/// <remarks>
	/// Returns the exception traceback as a Python object.
	/// </remarks>

	public IntPtr Traceback {
	    get {
		return excTb;
	    }
	}


	/// <summary>
	/// Dispose Method
	/// </summary>
	///
	/// <remarks>
	/// The Dispose method provides a way to explicitly release the 
	/// Python objects represented by a PythonException.
	/// </remarks>

	public void Dispose() {
	    if (!disposed) {
		if (Runtime.Py_IsInitialized() > 0) {
		    IntPtr gs = PythonEngine.AcquireLock();
		    Runtime.Decref(excType);
		    Runtime.Decref(excValue);
		    Runtime.Decref(excTb);
		    PythonEngine.ReleaseLock(gs);
		}
		GC.SuppressFinalize(this);
		disposed = true;
	    }
	}

	/// <summary>
	/// Matches Method
	/// </summary>
	///
	/// <remarks>
	/// Returns true if the Python exception type represented by the 
	/// PythonException instance matches the given exception type.
	/// </remarks>

	public static bool Matches(IntPtr ob) {
	    return Runtime.PyErr_ExceptionMatches(ob) != 0;
	}

    } 


}
