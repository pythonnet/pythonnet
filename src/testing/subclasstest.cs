using System;
using System.Collections.Generic;

namespace Python.Test
{
    public interface IInterfaceTest
    {
        // simple test with no arguments
        string foo();

        // test passing objects and boxing primitives
        string bar(string s, int i);

        // test events on interfaces
        event EventHandlerTest TestEvent;

        void OnTestEvent(int value);
    }

    public class SubClassTest : IInterfaceTest
    {
        public event EventHandlerTest TestEvent;

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

        // raise the test event
        public virtual void OnTestEvent(int value)
        {
            if (null != TestEvent)
            {
                TestEvent(this, new EventArgsTest(value));
            }
        }
    }

    public abstract class RecursiveInheritance
    {
        public class SubClass : RecursiveInheritance
        {
            public void SomeMethod()
            {
            }
        }
    }

    public class FunctionsTest
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
        public static SubClassTest create_instance(Type t)
        {
            return (SubClassTest)t.GetConstructor(new Type[] { }).Invoke(new object[] { });
        }

        public static IInterfaceTest create_instance_interface(Type t)
        {
            return (IInterfaceTest)t.GetConstructor(new Type[] { }).Invoke(new object[] { });
        }

        // test instances pass through managed code unchanged ...
        public static SubClassTest pass_through(SubClassTest s)
        {
            return s;
        }

        // ... but the return type is an interface type, objects get wrapped
        public static IInterfaceTest pass_through_interface(IInterfaceTest s)
        {
            return s;
        }

        public static int test_event(IInterfaceTest x, int value)
        {
            // reuse the event handler from eventtest.cs
            var et = new EventTest();
            x.TestEvent += et.GenericHandler;

            // raise the event (should trigger both python and managed handlers)
            x.OnTestEvent(value);

            x.TestEvent -= et.GenericHandler;
            return et.value;
        }
    }

    public interface ISimpleInterface
    {
        bool Ok();
    }

    public class SimpleClass
    {

        public static void TestObject(object obj)
        {
            if (obj is ISimpleInterface)
            {

            }
            else
            {
                throw new Exception();
            }
        }
    }
}
