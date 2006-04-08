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

	// overload selection test support 

	public static bool TestOverloadSelection(bool v) {
	    return v;
	}

	public static byte TestOverloadSelection(byte v) {
	    return v;
	}

	public static sbyte TestOverloadSelection(sbyte v) {
	    return v;
	}

	public static char TestOverloadSelection(char v) {
	    return v;
	}

	public static short TestOverloadSelection(short v) {
	    return v;
	}

	public static int TestOverloadSelection(int v) {
	    return v;
	}

	public static long TestOverloadSelection(long v) {
	    return v;
	}

	public static ushort TestOverloadSelection(ushort v) {
	    return v;
	}

	public static uint TestOverloadSelection(uint v) {
	    return v;
	}

	public static ulong TestOverloadSelection(ulong v) {
	    return v;
	}

	public static float TestOverloadSelection(float v) {
	    return v;
	}

	public static double TestOverloadSelection(double v) {
	    return v;
	}

	public static decimal TestOverloadSelection(decimal v) {
	    return v;
	}

	public static string TestOverloadSelection(string v) {
	    return v;
	}

	public static ShortEnum TestOverloadSelection(ShortEnum v) {
	    return v;
	}

	public static object TestOverloadSelection(object v) {
	    return v;
	}

	public static InterfaceTest TestOverloadSelection(InterfaceTest v) {
	    return v;
	}

	public static ISayHello1 TestOverloadSelection(ISayHello1 v) {
	    return v;
	}

	public static bool[] TestOverloadSelection(bool[] v) {
	    return v;
	}

	public static byte[] TestOverloadSelection(byte[] v) {
	    return v;
	}

	public static sbyte[] TestOverloadSelection(sbyte[] v) {
	    return v;
	}

	public static char[] TestOverloadSelection(char[] v) {
	    return v;
	}

	public static short[] TestOverloadSelection(short[] v) {
	    return v;
	}

	public static int[] TestOverloadSelection(int[] v) {
	    return v;
	}

	public static long[] TestOverloadSelection(long[] v) {
	    return v;
	}

	public static ushort[] TestOverloadSelection(ushort[] v) {
	    return v;
	}

	public static uint[] TestOverloadSelection(uint[] v) {
	    return v;
	}

	public static ulong[] TestOverloadSelection(ulong[] v) {
	    return v;
	}

	public static float[] TestOverloadSelection(float[] v) {
	    return v;
	}

	public static double[] TestOverloadSelection(double[] v) {
	    return v;
	}

	public static decimal[] TestOverloadSelection(decimal[] v) {
	    return v;
	}

	public static string[] TestOverloadSelection(string[] v) {
	    return v;
	}

	public static ShortEnum[] TestOverloadSelection(ShortEnum[] v) {
	    return v;
	}

	public static object[] TestOverloadSelection(object[] v) {
	    return v;
	}

	public static InterfaceTest[] TestOverloadSelection(InterfaceTest[] v){
	    return v;
	}

	public static ISayHello1[] TestOverloadSelection(ISayHello1[] v) {
	    return v;
	}

//  	public static GenericWrapper<T> TestOverloadSelection(
//                                          GenericWrapper<T> v) {
//  	    return v;
//  	}

//  	public static GenericWrapper<T>[] TestOverloadSelection(
//                                            GenericWrapper<T>[] v) {
//  	    return v;
//  	}

	public static int TestOverloadSelection(string s, int i, object[] o) {
	    return o.Length;
	}

	public static int TestOverloadSelection(string s, int i) {
	    return i;
	}

	public static int TestOverloadSelection(int i, string s) {
	    return i;
	}

    }


    public class MethodTestSub : MethodTest {

	public MethodTestSub() : base() {}

	public string PublicMethod(string echo) {
	    return echo;
	}

    }


}
