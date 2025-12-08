using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class RunStringTest
    {
        [Test]
        public void TestRunSimpleString()
        {
            int aa = PythonEngine.RunSimpleString("import sys");
            Assert.That(aa, Is.EqualTo(0));

            int bb = PythonEngine.RunSimpleString("import 1234");
            Assert.That(bb, Is.EqualTo(-1));
        }

        [Test]
        public void TestEval()
        {
            dynamic sys = Py.Import("sys");
            sys.attr1 = 100;
            var locals = new PyDict();
            locals.SetItem("sys", sys);
            locals.SetItem("a", new PyInt(10));

            object b = PythonEngine.Eval("sys.attr1 + a + 1", null, locals)
                .AsManagedObject(typeof(int));
            Assert.That(b, Is.EqualTo(111));
        }

        [Test]
        public void TestExec()
        {
            dynamic sys = Py.Import("sys");
            sys.attr1 = 100;
            var locals = new PyDict();
            locals.SetItem("sys", sys);
            locals.SetItem("a", new PyInt(10));

            PythonEngine.Exec("c = sys.attr1 + a + 1", null, locals);
            object c = locals.GetItem("c").AsManagedObject(typeof(int));
            Assert.That(c, Is.EqualTo(111));
        }

        [Test]
        public void TestExec2()
        {
            string code = @"
class Test1():
   pass

class Test2():
   def __init__(self):
       Test1()

Test2()";
            PythonEngine.Exec(code);
        }
    }
}
