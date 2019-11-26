using System.Collections;

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
}
