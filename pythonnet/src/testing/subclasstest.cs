using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Python.Test
{
    public interface IInterfaceTest
    {
        // simple test with no arguments
        string foo();

        // test passing objects and boxing primitives
        string bar(string s, int i);
    }

    public class SubClassTest : IInterfaceTest
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

        public virtual IList<string> return_list()
        {
            return new List<string> { "a", "b", "c" };
        }

        public static IList<string> test_list(SubClassTest x)
        {
            // calls into python if return_list is overriden
            return x.return_list();
        }
    }

    public class TestFunctions
    {
        public static string test_foo(IInterfaceTest x)
        {
            // calls into python if foo is overriden
            return x.foo();
        }

        public static string test_bar(IInterfaceTest x, string s, int i)
        {
            // calls into python if bar is overriden
            return x.bar(s, i);
        }

        // test instances can be constructed in managed code
        public static IInterfaceTest create_instance(Type t)
        {
            return (IInterfaceTest)t.GetConstructor(new Type[] {}).Invoke(new Object[] {});
        }

        // test instances pass through managed code unchanged
        public static IInterfaceTest pass_through(IInterfaceTest s)
        {
            return s;
        }
    }
}
