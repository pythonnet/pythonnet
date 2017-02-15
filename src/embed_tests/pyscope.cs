using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    [TestFixture]
    public class PyScopeTest
    {
        PyScope ps;
        
        [SetUp]
        public void SetUp()
        {
            ps = Py.Session("test");
        }

        [TearDown]
        public void TearDown()
        {
            ps.Dispose();
        }

        /// <summary>
        /// Eval a Python expression and obtain its return value.
        /// </summary>
        [Test]
        public void TestEval()
        {
            ps.SetLocal("a", 1);
            int result = ps.Eval<int>("a+2");
            Assert.AreEqual(result, 3);
        }

        /// <summary>
        /// Exec Python statements and obtain the local variables created.
        /// </summary>
        [Test]
        public void TestExec()
        {
            ps.SetGlobal("bb", 100); //declare a global variable
            ps.SetLocal("cc", 10); //declare a local variable
            ps.Exec("aa=bb+cc+3");
            int result = ps.Get<System.Int32>("aa");
            Assert.AreEqual(result, 113);
        }

        /// <summary>
        /// Exec Python statements in a subscope of the session then discard it.
        /// </summary>
        [Test]
        public void TestSubScope()
        {
            ps.SetGlobal("bb", 100); //declare a global variable
            ps.SetLocal("cc", 10); //declare a local variable

            PyScope scope = ps.SubScope();
            scope.Exec("aa=bb+cc+3");
            int result = scope.Get<System.Int32>("aa");
            Assert.AreEqual(result, 113); //
            scope.Dispose();

            Assert.IsFalse(ps.Exists("aa"));
        }

        /// <summary>
        /// Import a python module into the session.
        /// Equivalent to the Python "import" statement.
        /// </summary>
        [Test]
        public void TestImport()
        {
            dynamic sys = ps.Import("sys");
            Assert.IsTrue(ps.Exists("sys"));

            ps.Exec("sys.attr1 = 2");
            int value1 = ps.Eval<int>("sys.attr1");
            int value2 = (int)sys.attr1.AsManagedObject(typeof(int));
            Assert.AreEqual(value1, 2);
            Assert.AreEqual(value2, 2);
        }

        /// <summary>
        /// Import a python module into the session with a new name.
        /// Equivalent to the Python "import .. as .." statement.
        /// </summary>
        [Test]
        public void TestImportAs()
        {
            ps.ImportAs("sys", "sys1");
            Assert.IsTrue(ps.Exists("sys1"));
        }

        /// <summary>
        /// Suspend the Session, and reuse it later.
        /// </summary>
        [Test]
        public void TestSuspend()
        {
            ps.SetGlobal("bb", 100);
            ps.SetLocal("cc", 10);
            ps.Suspend();

            using (Py.GIL())
            {
                PythonEngine.RunSimpleString("import sys;");
            }

            ps.Exec("aa=bb+cc+3");
            int result = ps.Get<System.Int32>("aa");
            Assert.AreEqual(result, 113);
        }
    }
}
