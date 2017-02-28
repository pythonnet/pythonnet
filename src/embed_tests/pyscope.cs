using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class PyScopeTest
    {
        private PyScope ps;

        [SetUp]
        public void SetUp()
        {
            ps = Py.Session("test");
        }

        [TearDown]
        public void Dispose()
        {
            ps.Dispose();
        }

        /// <summary>
        /// Eval a Python expression and obtain its return value.
        /// </summary>
        [Test]
        public void TestEval()
        {
            ps.SetVariable("a", 1);
            var result = ps.Eval<int>("a + 2");
            Assert.AreEqual(result, 3);
        }

        /// <summary>
        /// Exec Python statements and obtain the local variables created.
        /// </summary>
        [Test]
        public void TestExec()
        {
            ps.SetVariable("bb", 100); //declare a global variable
            ps.SetVariable("cc", 10); //declare a local variable
            ps.Exec("aa = bb + cc + 3");
            var result = ps.GetVariable<int>("aa");
            Assert.AreEqual(result, 113);
        }

        /// <summary>
        /// Compile an expression into an ast object;
        /// Execute the ast and obtain its return value.
        /// </summary>
        [Test]
        public void TestCompileExpression()
        {
            ps.SetVariable("bb", 100); //declare a global variable
            ps.SetVariable("cc", 10); //declare a local variable
            PyObject script = ps.Compile("bb + cc + 3", "", RunFlagType.Eval);
            var result = ps.Execute<int>(script);
            Assert.AreEqual(result, 113);
        }

        /// <summary>
        /// Compile Python statements into an ast object;
        /// Execute the ast;
        /// Obtain the local variables created.
        /// </summary>
        [Test]
        public void TestCompileStatements()
        {
            ps.SetVariable("bb", 100); //declare a global variable
            ps.SetVariable("cc", 10); //declare a local variable
            PyObject script = ps.Compile("aa = bb + cc + 3", "", RunFlagType.File);
            ps.Execute(script);
            var result = ps.GetVariable<int>("aa");
            Assert.AreEqual(result, 113);
        }

        /// <summary>
        /// Exec Python statements in a SubScope of the session then discard it.
        /// </summary>
        [Test]
        public void TestSubScope()
        {
            ps.SetVariable("bb", 100); //declare a global variable
            ps.SetVariable("cc", 10); //declare a local variable

            PyScope scope = ps.SubScope();
            scope.Exec("aa = bb + cc + 3");
            var result = scope.GetVariable<int>("aa");
            Assert.AreEqual(result, 113); //
            scope.Dispose();

            Assert.IsFalse(ps.ContainsVariable("aa"));
        }

        /// <summary>
        /// Import a python module into the session.
        /// Equivalent to the Python "import" statement.
        /// </summary>
        [Test]
        public void TestImport()
        {
            dynamic sys = ps.Import("sys");
            Assert.IsTrue(ps.ContainsVariable("sys"));

            ps.Exec("sys.attr1 = 2");
            var value1 = ps.Eval<int>("sys.attr1");
            var value2 = (int)sys.attr1.AsManagedObject(typeof(int));
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
            Assert.IsTrue(ps.ContainsVariable("sys1"));
        }

        /// <summary>
        /// Suspend the Session, and reuse it later.
        /// </summary>
        [Test]
        public void TestSuspend()
        {
            ps.SetVariable("bb", 100);
            ps.SetVariable("cc", 10);
            ps.Suspend();

            using (Py.GIL())
            {
                PythonEngine.RunSimpleString("import sys;");
            }

            ps.Exec("aa = bb + cc + 3");
            var result = ps.GetVariable<int>("aa");
            Assert.AreEqual(result, 113);
        }
    }
}
