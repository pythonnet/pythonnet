using System;
using System.IO;

namespace Python.Test
{
    /// <summary>
    /// Supports units tests for method access.
    /// </summary>
    public class MethodTest
    {
        public MethodTest()
        {
        }

        public string PublicMethod()
        {
            return "public";
        }

        public static string PublicStaticMethod()
        {
            return "public static";
        }

        protected string ProtectedMethod()
        {
            return "protected";
        }

        protected static string ProtectedStaticMethod()
        {
            return "protected static";
        }

        internal string InternalMethod()
        {
            return "internal";
        }

        internal static string InternalStaticMethod()
        {
            return "internal static";
        }

        private string PrivateMethod()
        {
            return "private";
        }

        private static string PrivateStaticMethod()
        {
            return "private static";
        }


        /// <summary>
        /// Methods to support specific argument conversion unit tests
        /// </summary>
        public TypeCode TestEnumConversion(TypeCode v)
        {
            return v;
        }

        public FileAccess TestFlagsConversion(FileAccess v)
        {
            return v;
        }

        public Guid TestStructConversion(Guid v)
        {
            return v;
        }

        public Exception TestSubclassConversion(Exception v)
        {
            return v;
        }

        public Type[] TestNullArrayConversion(Type[] v)
        {
            return v;
        }

        public static string[] TestStringParamsArg(params string[] args)
        {
            return args;
        }

        public static object[] TestObjectParamsArg(params object[] args)
        {
            return args;
        }

        public static int[] TestValueParamsArg(params int[] args)
        {
            return args;
        }

        public static int[] TestOneArgWithParams(string s, params int[] args)
        {
            return args;
        }

        public static int[] TestTwoArgWithParams(string s, string x, params int[] args)
        {
            return args;
        }

        public static int[] TestOverloadedParams(string v, params int[] args)
        {
            return args;
        }

        public static int[] TestOverloadedParams(int v, int[] args)
        {
            return args;
        }

        public static string TestOverloadedNoObject(int i)
        {
            return "Got int";
        }

        public static string TestOverloadedObject(int i)
        {
            return "Got int";
        }

        public static string TestOverloadedObject(object o)
        {
            return "Got object";
        }

        public static string TestOverloadedObjectTwo(int a, int b)
        {
            return "Got int-int";
        }

        public static string TestOverloadedObjectTwo(string a, string b)
        {
            return "Got string-string";
        }

        public static string TestOverloadedObjectTwo(string a, int b)
        {
            return "Got string-int";
        }

        public static string TestOverloadedObjectTwo(string a, object b)
        {
            return "Got string-object";
        }

        public static string TestOverloadedObjectTwo(int a, object b)
        {
            return "Got int-object";
        }

        public static string TestOverloadedObjectTwo(object a, int b)
        {
            return "Got object-int";
        }

        public static string TestOverloadedObjectTwo(object a, object b)
        {
            return "Got object-object";
        }

        public static string TestOverloadedObjectTwo(int a, string b)
        {
            return "Got int-string";
        }

        public static string TestOverloadedObjectThree(object a, int b)
        {
            return "Got object-int";
        }

        public static string TestOverloadedObjectThree(int a, object b)
        {
            return "Got int-object";
        }

        public static bool TestStringOutParams(string s, out string s1)
        {
            s1 = "output string";
            return true;
        }

        public static bool TestStringRefParams(string s, ref string s1)
        {
            s1 = "output string";
            return true;
        }

        public static bool TestNonParamsArrayInLastPlace(int i1, int[] i2)
        {
            return false;
        }

        public static bool TestNonParamsArrayInLastPlace(int i1, int i2, int i3)
        {
            return true;
        }

        public static bool TestValueOutParams(string s, out int i1)
        {
            i1 = 42;
            return true;
        }

        public static bool TestValueRefParams(string s, ref int i1)
        {
            i1 = 42;
            return true;
        }

        public static bool TestObjectOutParams(object o, out object o1)
        {
            o1 = new Exception("test");
            return true;
        }

        public static bool TestObjectRefParams(object o, ref object o1)
        {
            o1 = new Exception("test");
            return true;
        }

        public static bool TestStructOutParams(object o, out Guid o1)
        {
            o1 = Guid.NewGuid();
            return true;
        }

        public static bool TestStructRefParams(object o, ref Guid o1)
        {
            o1 = Guid.NewGuid();
            return true;
        }

        public static void TestVoidSingleOutParam(out int i)
        {
            i = 42;
        }

        public static void TestVoidSingleRefParam(ref int i)
        {
            i = 42;
        }

        public static int TestSingleDefaultParam(int i = 5)
        {
            return i;
        }

        public static int TestTwoDefaultParam(int i = 5, int j = 6)
        {
            return i + j;
        }

        public static int TestOneArgAndTwoDefaultParam(int z, int i = 5, int j = 6)
        {
            return i + j + z;
        }


        // overload selection test support

        public static bool Overloaded(bool v)
        {
            return v;
        }

        public static byte Overloaded(byte v)
        {
            return v;
        }

        public static sbyte Overloaded(sbyte v)
        {
            return v;
        }

        public static char Overloaded(char v)
        {
            return v;
        }

        public static short Overloaded(short v)
        {
            return v;
        }

        public static int Overloaded(int v)
        {
            return v;
        }

        public static long Overloaded(long v)
        {
            return v;
        }

        public static ushort Overloaded(ushort v)
        {
            return v;
        }

        public static uint Overloaded(uint v)
        {
            return v;
        }

        public static ulong Overloaded(ulong v)
        {
            return v;
        }

        public static float Overloaded(float v)
        {
            return v;
        }

        public static double Overloaded(double v)
        {
            return v;
        }

        public static decimal Overloaded(decimal v)
        {
            return v;
        }

        public static string Overloaded(string v)
        {
            return v;
        }

        public static ShortEnum Overloaded(ShortEnum v)
        {
            return v;
        }

        public static object Overloaded(object v)
        {
            return v;
        }

        public static InterfaceTest Overloaded(InterfaceTest v)
        {
            return v;
        }

        public static ISayHello1 Overloaded(ISayHello1 v)
        {
            return v;
        }

        public static bool[] Overloaded(bool[] v)
        {
            return v;
        }

        public static byte[] Overloaded(byte[] v)
        {
            return v;
        }

        public static sbyte[] Overloaded(sbyte[] v)
        {
            return v;
        }

        public static char[] Overloaded(char[] v)
        {
            return v;
        }

        public static short[] Overloaded(short[] v)
        {
            return v;
        }

        public static int[] Overloaded(int[] v)
        {
            return v;
        }

        public static long[] Overloaded(long[] v)
        {
            return v;
        }

        public static ushort[] Overloaded(ushort[] v)
        {
            return v;
        }

        public static uint[] Overloaded(uint[] v)
        {
            return v;
        }

        public static ulong[] Overloaded(ulong[] v)
        {
            return v;
        }

        public static float[] Overloaded(float[] v)
        {
            return v;
        }

        public static double[] Overloaded(double[] v)
        {
            return v;
        }

        public static decimal[] Overloaded(decimal[] v)
        {
            return v;
        }

        public static string[] Overloaded(string[] v)
        {
            return v;
        }

        public static ShortEnum[] Overloaded(ShortEnum[] v)
        {
            return v;
        }

        public static object[] Overloaded(object[] v)
        {
            return v;
        }

        public static InterfaceTest[] Overloaded(InterfaceTest[] v)
        {
            return v;
        }

        public static ISayHello1[] Overloaded(ISayHello1[] v)
        {
            return v;
        }

        public static GenericWrapper<bool> Overloaded(GenericWrapper<bool> v)
        {
            return v;
        }

        public static GenericWrapper<byte> Overloaded(GenericWrapper<byte> v)
        {
            return v;
        }

        public static GenericWrapper<sbyte> Overloaded(GenericWrapper<sbyte> v)
        {
            return v;
        }

        public static GenericWrapper<char> Overloaded(GenericWrapper<char> v)
        {
            return v;
        }

        public static GenericWrapper<short> Overloaded(GenericWrapper<short> v)
        {
            return v;
        }

        public static GenericWrapper<int> Overloaded(GenericWrapper<int> v)
        {
            return v;
        }

        public static GenericWrapper<long> Overloaded(GenericWrapper<long> v)
        {
            return v;
        }

        public static GenericWrapper<ushort> Overloaded(GenericWrapper<ushort> v)
        {
            return v;
        }

        public static GenericWrapper<uint> Overloaded(GenericWrapper<uint> v)
        {
            return v;
        }

        public static GenericWrapper<ulong> Overloaded(GenericWrapper<ulong> v)
        {
            return v;
        }

        public static GenericWrapper<float> Overloaded(GenericWrapper<float> v)
        {
            return v;
        }

        public static GenericWrapper<double> Overloaded(GenericWrapper<double> v)
        {
            return v;
        }

        public static GenericWrapper<decimal> Overloaded(GenericWrapper<decimal> v)
        {
            return v;
        }

        public static GenericWrapper<string> Overloaded(GenericWrapper<string> v)
        {
            return v;
        }

        public static GenericWrapper<ShortEnum> Overloaded(GenericWrapper<ShortEnum> v)
        {
            return v;
        }

        public static GenericWrapper<object> Overloaded(GenericWrapper<object> v)
        {
            return v;
        }

        public static GenericWrapper<InterfaceTest> Overloaded(GenericWrapper<InterfaceTest> v)
        {
            return v;
        }

        public static GenericWrapper<ISayHello1> Overloaded(GenericWrapper<ISayHello1> v)
        {
            return v;
        }

        public static GenericWrapper<bool>[] Overloaded(GenericWrapper<bool>[] v)
        {
            return v;
        }

        public static GenericWrapper<byte>[] Overloaded(GenericWrapper<byte>[] v)
        {
            return v;
        }

        public static GenericWrapper<sbyte>[] Overloaded(GenericWrapper<sbyte>[] v)
        {
            return v;
        }

        public static GenericWrapper<char>[] Overloaded(GenericWrapper<char>[] v)
        {
            return v;
        }

        public static GenericWrapper<short>[] Overloaded(GenericWrapper<short>[] v)
        {
            return v;
        }

        public static GenericWrapper<int>[] Overloaded(GenericWrapper<int>[] v)
        {
            return v;
        }

        public static GenericWrapper<long>[] Overloaded(GenericWrapper<long>[] v)
        {
            return v;
        }

        public static GenericWrapper<ushort>[] Overloaded(GenericWrapper<ushort>[] v)
        {
            return v;
        }

        public static GenericWrapper<uint>[] Overloaded(GenericWrapper<uint>[] v)
        {
            return v;
        }

        public static GenericWrapper<ulong>[] Overloaded(GenericWrapper<ulong>[] v)
        {
            return v;
        }

        public static GenericWrapper<float>[] Overloaded(GenericWrapper<float>[] v)
        {
            return v;
        }

        public static GenericWrapper<double>[] Overloaded(GenericWrapper<double>[] v)
        {
            return v;
        }

        public static GenericWrapper<decimal>[] Overloaded(GenericWrapper<decimal>[] v)
        {
            return v;
        }

        public static GenericWrapper<string>[] Overloaded(GenericWrapper<string>[] v)
        {
            return v;
        }

        public static GenericWrapper<ShortEnum>[] Overloaded(GenericWrapper<ShortEnum>[] v)
        {
            return v;
        }

        public static GenericWrapper<object>[] Overloaded(GenericWrapper<object>[] v)
        {
            return v;
        }

        public static GenericWrapper<InterfaceTest>[] Overloaded(GenericWrapper<InterfaceTest>[] v)
        {
            return v;
        }

        public static GenericWrapper<ISayHello1>[] Overloaded(GenericWrapper<ISayHello1>[] v)
        {
            return v;
        }

        public static int Overloaded(string s, int i, object[] o)
        {
            return o.Length;
        }

        public static int Overloaded(string s, int i)
        {
            return i;
        }

        public static int Overloaded(int i, string s)
        {
            return i;
        }

        public static string CaseSensitive()
        {
            return "CaseSensitive";
        }

        public static string Casesensitive()
        {
            return "Casesensitive";
        }
    }


    public class MethodTestSub : MethodTest
    {
        public MethodTestSub() : base()
        {
        }

        public string PublicMethod(string echo)
        {
            return echo;
        }
    }
}

namespace PlainOldNamespace
{
    public class PlainOldClass
    {
        public PlainOldClass() { }

        public PlainOldClass(int param) { }

        private readonly byte[] payload = new byte[(int)Math.Pow(2, 20)]; //1 MB

        public void NonGenericMethod() { }

        public void GenericMethod<T>() { }

        public void OverloadedMethod() { }

        public void OverloadedMethod(int param) { }
    }
}
