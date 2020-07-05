using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class RunStringTest
    {
        private Py.GILState _gs;

        [SetUp]
        public void SetUp()
        {
            _gs = Py.GIL();
        }

        [TearDown]
        public void Dispose()
        {
            _gs.Dispose();
        }

        [Test]
        public void TestRunSimpleString()
        {
            int aa = PythonEngine.RunSimpleString("import sys");
            Assert.AreEqual(0, aa);

            int bb = PythonEngine.RunSimpleString("import 1234");
            Assert.AreEqual(-1, bb);
        }

        [Test]
        public void TestEval()
        {
            dynamic sys = Py.Import("sys");
            sys.attr1 = 100;
            var locals = new PyDict();
            locals.SetItem("sys", sys);
            locals.SetItem("a", new PyInt(10));

            object b = PythonEngine.Eval("sys.attr1 + a + 1", null, locals.Handle)
                .AsManagedObject(typeof(int));
            Assert.AreEqual(111, b);
        }

        [Test]
        public void TestExec()
        {
            dynamic sys = Py.Import("sys");
            sys.attr1 = 100;
            var locals = new PyDict();
            locals.SetItem("sys", sys);
            locals.SetItem("a", new PyInt(10));

            PythonEngine.Exec("c = sys.attr1 + a + 1", null, locals.Handle);
            object c = locals.GetItem("c").AsManagedObject(typeof(int));
            Assert.AreEqual(111, c);
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
