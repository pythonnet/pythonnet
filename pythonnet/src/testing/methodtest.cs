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
using System.Windows.Forms;

namespace Python.Test {

    //========================================================================
    // Supports units tests for method access.
    //========================================================================

    public class MethodTest {

	public MethodTest() {}

	public string PublicMethod() {
	    return "public";
	}

	public static string PublicStaticMethod() {
	    return "public static";
	}

	protected string ProtectedMethod() {
	    return "protected";
	}

	protected static string ProtectedStaticMethod() {
	    return "protected static";
	}

	internal string InternalMethod() {
	    return "internal";
	}

	internal static string InternalStaticMethod() {
	    return "internal static";
	}

	private string PrivateMethod() {
	    return "private";
	}

	private static string PrivateStaticMethod() {
	    return "private static";
	}


	//===================================================================
	// Methods to support specific argument conversion unit tests
	//===================================================================

	public TypeCode TestEnumConversion(TypeCode v) {
	    return v;
	}

	public FileAccess TestFlagsConversion(FileAccess v) {
	    return v;
	}

	public Guid TestStructConversion(Guid v) {
	    return v;
	}

	public Control TestSubclassConversion(Control v) {
	    return v;
	}

	public Type[] TestNullArrayConversion(Type [] v) {
	    return v;
	}


	public static bool TestStringOutParams (string s, out string s1) {
	    s1 = "output string";
	    return true;
	}

	public static bool TestStringRefParams (string s, ref string s1) {
	    s1 = "output string";
	    return true;
	}

	public static bool TestValueOutParams (string s, out int i1) {
	    i1 = 42;
	    return true;
	}

	public static bool TestValueRefParams (string s, ref int i1) {
	    i1 = 42;
	    return true;
	}

	public static bool TestObjectOutParams (object o, out object o1) {
	    o1 = new System.Exception("test");
	    return true;
	}

	public static bool TestObjectRefParams (object o, ref object o1) {
	    o1 = new System.Exception("test");
	    return true;
	}

	public static bool TestStructOutParams (object o, out Guid o1) {
	    o1 = Guid.NewGuid();
	    return true;
	}

	public static bool TestStructRefParams (object o, ref Guid o1) {
	    o1 = Guid.NewGuid();
	    return true;
	}

	public static void TestVoidSingleOutParam (out int i) {
	    i = 42;
	}

	public static void TestVoidSingleRefParam (ref int i) {
	    i = 42;
	}

    }


    public class MethodTestSub : MethodTest {

	public MethodTestSub() : base() {}

	public string PublicMethod(string echo) {
	    return echo;
	}

    }


}
