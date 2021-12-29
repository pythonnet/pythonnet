using System;
using System.IO;

namespace Python.Test
{
    /// <summary>
    /// These classes support the CLR constructor unit tests.
    /// </summary>
    public class EnumConstructorTest
    {
        public TypeCode value;

        public EnumConstructorTest(TypeCode v)
        {
            value = v;
        }
    }


    public class FlagsConstructorTest
    {
        public FileAccess value;

        public FlagsConstructorTest(FileAccess v)
        {
            value = v;
        }
    }


    public class StructConstructorTest
    {
        public Guid value;

        public StructConstructorTest(Guid v)
        {
            value = v;
        }
    }

    public struct GenericStructConstructorTest<T> where T : struct
    {
        public T Value;

        public GenericStructConstructorTest(T value)
        {
            this.Value = value;
        }
    }


    public class SubclassConstructorTest
    {
        public Exception value;

        public SubclassConstructorTest(Exception v)
        {
            value = v;
        }
    }

    public class MultipleConstructorsTest
    {
        public string value;
        public Type[] type;

        public MultipleConstructorsTest()
        {
            value = "";
            type = new Type[1] { null };
        }

        public MultipleConstructorsTest(string s, params Type[] tp)
        {
            value = s;
            type = tp;
        }
    }

    public class DefaultConstructorMatching
    {
        public double a;
        public DefaultConstructorMatching() { a = 1; }
        public DefaultConstructorMatching(double a) { this.a = a; }
    }
}
