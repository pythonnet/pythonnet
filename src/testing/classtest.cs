using System;
using System.Collections;
using Python.Runtime;

namespace Python.Test
{
    /// <summary>
    /// Supports CLR class unit tests.
    /// </summary>
    public class ClassTest
    {
        public static ArrayList GetArrayList()
        {
            var list = new ArrayList();
            for (var i = 0; i < 10; i++)
            {
                list.Add(i);
            }
            return list;
        }

        public static Hashtable GetHashtable()
        {
            var dict = new Hashtable();
            dict.Add("one", 1);
            dict.Add("two", 2);
            dict.Add("three", 3);
            dict.Add("four", 4);
            dict.Add("five", 5);
            return dict;
        }

        public static IEnumerator GetEnumerator()
        {
            var temp = "test string";
            return temp.GetEnumerator();
        }
    }


    public class ClassCtorTest1
    {
        public string value;

        public ClassCtorTest1()
        {
            value = "default";
        }
    }

    public class ClassCtorTest2
    {
        public string value;

        public ClassCtorTest2(string v)
        {
            value = v;
        }
    }

    internal class InternalClass
    {
    }

    public class TestAttributeAttribute : Attribute
    {
        public int Arg1 { get; }
        public string Arg2 { get;}
        public int Arg3 { get; set; }
        public IntEnum Arg4 { get; set; }
        public TestAttributeAttribute()
        {

        }
        public TestAttributeAttribute(int arg1)
        {
            Arg1 = arg1;
        }
        public TestAttributeAttribute(int arg1, string arg2)
        {
            Arg1 = arg1;
            Arg2 = arg2;
        }

        public TestAttributeAttribute(int arg1, int arg2)
        {
            Arg1 = arg1;
            Arg2 = arg2.ToString();
        }

        public static void Verify(object x, int arg1 = 1, string arg2 = "2")
        {
            var attr = x.GetType().GetCustomAttribute<TestAttributeAttribute>();
            if (attr.Arg1 != arg1) throw new Exception("Verification failed");
            if (attr.Arg2 != arg2) throw new Exception("Verification failed");
            if (attr.Arg3 != 3) throw new Exception("Verification failed");
            if (attr.Arg4 != IntEnum.Four) throw new Exception("Verification failed");
        }
    }
}
