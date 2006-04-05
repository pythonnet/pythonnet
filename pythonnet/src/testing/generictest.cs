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
using System.Windows.Forms;

namespace Python.Test {

    //========================================================================
    // Supports CLR generics unit tests.
    //========================================================================
    
    public class GenericTypeDefinition<T, U> {
	public T value1;
	public U value2;

	public GenericTypeDefinition(T arg1, U arg2) {
	    this.value1 = arg1;
	    this.value2 = arg2;
	}



    }

    public class DerivedFromOpenGeneric<V, W> : 
	         GenericTypeDefinition<int, V> {

	public W value3;

	public DerivedFromOpenGeneric(int arg1, V arg2, W arg3) : 
               base(arg1, arg2) {
	    this.value3 = arg3;
	}
    }


    public class NameTest {}
    public class NameTest<T,U> {}
    public class NameTest<T> {}


}
