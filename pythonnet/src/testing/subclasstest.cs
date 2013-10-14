using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Python.Test
{
    public class SubClassTest
    {
        public SubClassTest()
        {
        }

        // simple test with no arguments
        public virtual string foo()
        {
            return "foo";
        }

        // test passing objects and boxing primitives
        public virtual string bar(string s, int i)
        {
            return s;
        }

        // virtual methods that aren't overriden in python still work
        public virtual string not_overriden()
        {
            return "not_overriden";
        }

        public static string test_foo(SubClassTest x)
        {
            // calls into python if foo is overriden
            return x.foo();
        }

        public static string test_bar(SubClassTest x, string s, int i)
        {
            // calls into python if bar is overriden
            return x.bar(s, i);
        }
    }
}
