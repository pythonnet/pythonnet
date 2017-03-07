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
            using (Py.GIL())
            {
                ps = Py.Scope("test");
            }                
        }

        [TearDown]
        public void Dispose()
        {
            using (Py.GIL())
            {
                ps.Dispose();
                ps = null;
            }
        }

        /// <summary>
        /// Eval a Python expression and obtain its return value.
        /// </summary>
        [Test]
        public void TestEval()
        {
            using (Py.GIL())
            {
                ps.SetVariable("a", 1);
                var result = ps.Eval<int>("a + 2");
                Assert.AreEqual(result, 3);
            }                
        }

        /// <summary>
        /// Exec Python statements and obtain the local variables created.
        /// </summary>
        [Test]
        public void TestExec()
        {
            using (Py.GIL())
            {
                ps.SetVariable("bb", 100); //declare a global variable
                ps.SetVariable("cc", 10); //declare a local variable
                ps.Exec("aa = bb + cc + 3");
                var result = ps.GetVariable<int>("aa");
                Assert.AreEqual(result, 113);
            }
        }

        /// <summary>
        /// Compile an expression into an ast object;
        /// Execute the ast and obtain its return value.
        /// </summary>
        [Test]
        public void TestCompileExpression()
        {
            using (Py.GIL())
            {
                ps.SetVariable("bb", 100); //declare a global variable
                ps.SetVariable("cc", 10); //declare a local variable
                PyObject script = ps.Compile("bb + cc + 3", "", RunFlagType.Eval);
                var result = ps.Execute<int>(script);
                Assert.AreEqual(result, 113);
            }
        }

        /// <summary>
        /// Compile Python statements into an ast object;
        /// Execute the ast;
        /// Obtain the local variables created.
        /// </summary>
        [Test]
        public void TestCompileStatements()
        {
            using (Py.GIL())
            {
                ps.SetVariable("bb", 100); //declare a global variable
                ps.SetVariable("cc", 10); //declare a local variable
                PyObject script = ps.Compile("aa = bb + cc + 3", "", RunFlagType.File);
                ps.Execute(script);
                var result = ps.GetVariable<int>("aa");
                Assert.AreEqual(result, 113);
            }
        }

        /// <summary>
        /// Exec Python statements in a SubScope of the session then discard it.
        /// </summary>
        [Test]
        public void TestSubScope()
        {
            using (Py.GIL())
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
        }

        /// <summary>
        /// Import a python module into the session.
        /// Equivalent to the Python "import" statement.
        /// </summary>
        [Test]
        public void TestImport()
        {
            using (Py.GIL())
            {
                dynamic sys = ps.Import("sys");
                Assert.IsTrue(ps.ContainsVariable("sys"));

                ps.Exec("sys.attr1 = 2");
                var value1 = ps.Eval<int>("sys.attr1");
                var value2 = (int)sys.attr1.AsManagedObject(typeof(int));
                Assert.AreEqual(value1, 2);
                Assert.AreEqual(value2, 2);
            }
        }

        /// <summary>
        /// Import a python module into the session with a new name.
        /// Equivalent to the Python "import .. as .." statement.
        /// </summary>
        [Test]
        public void TestImportAs()
        {
            using (Py.GIL())
            {
                ps.ImportAs("sys", "sys1");
                Assert.IsTrue(ps.ContainsVariable("sys1"));
            }
        }

        /// <summary>
        /// Suspend the Session, and reuse it later.
        /// </summary>
        [Test]
        public void TestThread()
        {
            //I open an proposal here https://github.com/pythonnet/pythonnet/pull/419
            //after it merged, the BeginAllowThreads statement blow and the last EndAllowThreads statement
            //should be removed.
            var ts = PythonEngine.BeginAllowThreads();
            using (Py.GIL())
            {
                ps.SetVariable("res", 0);
                ps.SetVariable("bb", 100);
                ps.SetVariable("th_cnt", 0);
            }
            int th_cnt = 3;
            for (int i =0; i< th_cnt; i++)
            {
                System.Threading.Thread th = new System.Threading.Thread(()=>
                {
                    using (Py.GIL())
                    {
                        ps.Exec(
                            "res += bb + 1\n" +
                            "th_cnt += 1\n");
                    }
                });
                th.Start();
            }
            //do not use Thread.Join to make this test more complicate
            int cnt = 0;
            while(cnt != th_cnt)
            {
                using (Py.GIL())
                {
                    cnt = ps.GetVariable<int>("th_cnt");
                }
                System.Threading.Thread.Sleep(10);
            }
            using (Py.GIL())
            {
                var result = ps.GetVariable<int>("res");
                Assert.AreEqual(101* th_cnt, result);
            }
            PythonEngine.EndAllowThreads(ts);
        }
    }
}
