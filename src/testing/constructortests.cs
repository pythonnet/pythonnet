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


    public class SubclassConstructorTest
    {
        public Exception value;

        public SubclassConstructorTest(Exception v)
        {
            value = v;
        }
    }
}
