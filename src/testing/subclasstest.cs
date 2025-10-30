using System;
using System.Collections.Generic;
using System.Threading;

using Python.Runtime;

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
    public interface ISimpleInterface2
    {
        int Execute(CancellationToken token);
    }
    public class TestAttributeAttribute: Attribute
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string Z { get; set; }
        public string W { get; set; }
        public TestAttributeAttribute(int x, int y, string z = "x")
        {
            X = x;
            Y = y;
            Z = z;

        }
    }

    public abstract class SimpleClassBase
    {
        private int counter;
            public virtual int IncrementThing()
            {
                return counter++;
            }

    }

    public abstract class SimpleClass : SimpleClassBase
    {
        public bool Initialized;

        public SimpleClass()
        {
            Initialized = true;
        }

        public int CallIncrementThing()
        {
            var x = IncrementThing();
            return x;
        }

        public static void TestObject(object obj)
        {
            if (obj is ISimpleInterface si)
            {
                if (!si.Ok())
                    throw new Exception();

            }else if (obj is ISimpleInterface2 si2)
            {
                si2.Execute(CancellationToken.None);

            }
            else
            {
                throw new Exception();
            }
        }
        public static void TestObjectProperty(object obj, string prop, double newval)
        {
            obj.GetType().GetProperty(prop).SetValue(obj, newval);
            var val = obj.GetType().GetProperty(prop).GetValue(obj);
            if (!Equals(newval, val))
                throw new Exception();
        }

        private static SimpleClass objStore;
        public static void Test1(SimpleClass obj)
        {
            objStore = obj;
            int x = obj.IncrementThing();
        }

        public static void Test2()
        {
            GC.Collect();

            var threads = new Thread[20];
            for(int i = 0; i < threads.Length; i++)
                threads[i] =  new Thread(() => TestObjectProperty(objStore, "X", 10.0));
            for (int i = 0; i < threads.Length; i++)
                threads[i].Start();
            for (int i = 0; i < threads.Length; i++)
                threads[i].Join();
        }

        public static object InvokeCtor(Type t)
        {
            var obj = Activator.CreateInstance(t);
            return obj;
        }

        public object TestObj { get; set; }

        public static object TestOnType(Type t)
        {
            using (Py.GIL())
            {
                var obj = (SimpleClass) Activator.CreateInstance(t);
                //obj = obj.ToPython().As<SimpleClass>();
                obj.TestObj = new object();
                var py = obj.ToPython();
                var man = py.As<SimpleClass>();
                if (!ReferenceEquals(man, obj))
                    throw new Exception("Same object expected");
                var setObj = py.GetAttr("TestObj").As<object>();
                if (setObj == null)
                    throw new NullReferenceException();
                if (ReferenceEquals(setObj, obj.TestObj) == false)
                    throw new Exception("!!");


            return obj;
            }
        }

        public static void Pause()
        {

        }

    }
}
