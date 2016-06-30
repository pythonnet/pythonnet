using System;
using System.Collections;
using System.IO;

namespace Python.Test
{
    //========================================================================
    // These classes support the CLR constructor unit tests.
    //========================================================================
    public class AConstrucorTest
    {
        public string name;
        public AConstrucorTest(string n) { name = n; }
    }
    public class LinkConstructorTest
    {
        public LinkConstructorTest()
        {
            DefaultCtCalled = true;
        }
        public LinkConstructorTest(AConstrucorTest a,double matchMe,AConstrucorTest b)
        {
            MatchMe = matchMe;
            a1 = a;
            a2 = b;
        }
        public bool DefaultCtCalled;
        public double MatchMe;
        public AConstrucorTest a1;
        public AConstrucorTest a2;
    }
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
}