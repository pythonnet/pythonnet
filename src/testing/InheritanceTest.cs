using System;

namespace Python.Test
{
    public class BaseClass
    {
        public bool IsBase() => true;
    }

    public class DerivedClass : BaseClass
    {
        public new bool IsBase() => false;
    }
}
