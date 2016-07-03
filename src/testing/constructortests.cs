using System;
using System.Collections;
using System.IO;

namespace Python.Test
{
    //========================================================================
    // These classes support the CLR constructor unit tests.
    //========================================================================

    public class EnumConstructorTest
    {
        public TypeCode value;

        public EnumConstructorTest(TypeCode v)
        {
            this.value = v;
        }
    }


    public class FlagsConstructorTest
    {
        public FileAccess value;

        public FlagsConstructorTest(FileAccess v)
        {
            this.value = v;
        }
    }


    public class StructConstructorTest
    {
        public Guid value;

        public StructConstructorTest(Guid v)
        {
            this.value = v;
        }
    }


    public class SubclassConstructorTest
    {
        public Exception value;

        public SubclassConstructorTest(Exception v)
        {
            this.value = v;
        }
    }
    public class ToDoubleConstructorTest
    {
        public ToDoubleConstructorTest()
        {
            //Just default values
        }
        public ToDoubleConstructorTest(string a, double b,string c)
        {
            this.a = a;
            this.b = b;
            this.c = c;
        }
        public string a;
        public double b;
        public string c;
    }
    public class ToFloatConstructorTest
    {
        public ToFloatConstructorTest()
        {
            // just default values.
        }
        public ToFloatConstructorTest(string a, float b, string c)
        {
            this.a = a;
            this.b = b;
            this.c = c;
        }
        public string a;
        public float b;
        public string c;
    }
}