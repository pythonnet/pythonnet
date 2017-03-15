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
                ps = Py.CreateScope("test");
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
                Assert.AreEqual(3, result);
            }                
        }

        /// <summary>
        /// Exec Python statements and obtain the variables created.
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
                Assert.AreEqual(113, result);
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
                PyObject script = PythonEngine.Compile("bb + cc + 3", "", RunFlagType.Eval);
                var result = ps.Execute<int>(script);
                Assert.AreEqual(113, result);
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
                PyObject script = PythonEngine.Compile("aa = bb + cc + 3", "", RunFlagType.File);
                ps.Execute(script);
                var result = ps.GetVariable<int>("aa");
                Assert.AreEqual(113, result);
            }
        }

        /// <summary>
        /// Create a function in the scope, then the function can read variables in the scope.
        /// It cannot write the variables unless it uses the 'global' keyword.
        /// </summary>
        [Test]
        public void TestScopeFunction()
        {
            using (Py.GIL())
            {
                ps.SetVariable("bb", 100);
                ps.SetVariable("cc", 10);
                ps.Exec(
                    "def func1():\n" +
                    "    bb = cc + 10\n");
                dynamic func1 = ps.GetVariable("func1");
                func1(); //call the function, it can be called any times
                var result = ps.GetVariable<int>("bb");
                Assert.AreEqual(100, result);

                ps.SetVariable("bb", 100);
                ps.SetVariable("cc", 10);
                ps.Exec(
                    "def func2():\n" +
                    "    global bb\n" +
                    "    bb = cc + 10\n");
                dynamic func2 = ps.GetVariable("func2");
                func2();
                result = ps.GetVariable<int>("bb");
                Assert.AreEqual(20, result);
            }
        }

        /// <summary>
        /// Create a class in the scope, the class can read variables in the scope.
        /// Its methods can write the variables with the help of 'global' keyword.
        /// </summary>
        [Test]
        public void TestScopeClass()
        {
            using (Py.GIL())
            {
                dynamic _ps = ps;
                _ps.bb = 100;
                ps.Exec(
                    "class class1():\n" +
                    "    def __init__(self, value):\n" +
                    "        self.value = value\n" +
                    "    def call(self, arg):\n" +
                    "        return self.value + bb + arg\n" + //use scope variables
                    "    def update(self, arg):\n" +
                    "        global bb\n" +
                    "        bb = self.value + arg\n"  //update scope variable
                );
                dynamic obj1 = _ps.class1(20);
                var result = obj1.call(10).AsManagedObject(typeof(int));
                Assert.AreEqual(130, result);

                obj1.update(10);
                result = ps.GetVariable<int>("bb");
                Assert.AreEqual(30, result);
            }
        }

        /// <summary>
        /// Import a python module into the session.
        /// Equivalent to the Python "import" statement.
        /// </summary>
        [Test]
        public void TestImportModule()
        {
            using (Py.GIL())
            {
                dynamic sys = ps.ImportModule("sys");
                Assert.IsTrue(ps.ContainsVariable("sys"));

                ps.Exec("sys.attr1 = 2");
                var value1 = ps.Eval<int>("sys.attr1");
                var value2 = (int)sys.attr1.AsManagedObject(typeof(int));
                Assert.AreEqual(2, value1);
                Assert.AreEqual(2, value2);

                //import as
                ps.ImportModule("sys", "sys1");
                Assert.IsTrue(ps.ContainsVariable("sys1"));
            }
        }

        /// <summary>
        /// Create a scope and import variables from a scope, 
        /// exec Python statements in the scope then discard it.
        /// </summary>
        [Test]
        public void TestImportScope()
        {
            using (Py.GIL())
            {
                ps.SetVariable("bb", 100);
                ps.SetVariable("cc", 10);

                PyScope scope = ps.CreateScope();

                scope.Exec("aa = bb + cc + 3");
                var result = scope.GetVariable<int>("aa");
                Assert.AreEqual(113, result);
                
                scope.Dispose();

                Assert.IsFalse(ps.ContainsVariable("aa"));
            }
        }

        /// <summary>
        /// Create a scope and import variables from a scope, 
        /// call the function imported.
        /// </summary>
        [Test]
        public void TestImportScopeFunction()
        {
            using (Py.GIL())
            {
                ps.SetVariable("bb", 100);
                ps.SetVariable("cc", 10);
                ps.Exec(
                    "def func1():\n" +
                    "    return cc + bb\n");

                PyScope scope = ps.CreateScope();

                //'func1' is imported from the origion scope
                scope.Exec(
                    "def func2():\n" +
                    "    return func1() - cc - bb\n");
                dynamic func2 = scope.GetVariable("func2");

                var result1 = func2().AsManagedObject(typeof(int));
                Assert.AreEqual(0, result1);

                scope.SetVariable("cc", 20);//it has no effect on the globals of 'func1'
                var result2 = func2().AsManagedObject(typeof(int));
                Assert.AreEqual(-10, result2);
                scope.SetVariable("cc", 10); //rollback

                ps.SetVariable("cc", 20);
                var result3 = func2().AsManagedObject(typeof(int));
                Assert.AreEqual(10, result3);
                ps.SetVariable("cc", 10); //rollback

                scope.Dispose();
            }
        }

        /// <summary>
        /// Import a python module into the session with a new name.
        /// Equivalent to the Python "import .. as .." statement.
        /// </summary>
        [Test]
        public void TestImportScopeByName()
        {
            using (Py.GIL())
            {
                ps.SetVariable("bb", 100);

                var scope = Py.CreateScope();
                scope.ImportScope("test");

                Assert.IsTrue(scope.ContainsVariable("bb"));
            }
        }

        /// <summary>
        /// Use the locals() and globals() method just like in python module
        /// </summary>
        [Test]
        public void TestVariables()
        {
            (ps.Variables() as dynamic)["ee"] = new PyInt(200);
            var a0 = ps.GetVariable<int>("ee");
            Assert.AreEqual(200, a0);

            ps.Exec("locals()['ee'] = 210");
            var a1 = ps.GetVariable<int>("ee");
            Assert.AreEqual(210, a1);

            ps.Exec("globals()['ee'] = 220");
            var a2 = ps.GetVariable<int>("ee");
            Assert.AreEqual(220, a2);

            var item = (ps as dynamic).locals();
            item["ee"] = new PyInt(230);
            item.Dispose();
            var a3 = ps.GetVariable<int>("ee");
            Assert.AreEqual(230, a3);
        }

        /// <summary>
        /// Share a pyscope by multiple threads.
        /// </summary>
        [Test]
        public void TestThread()
        {
            //After the proposal here https://github.com/pythonnet/pythonnet/pull/419 complished,
            //the BeginAllowThreads statement blow and the last EndAllowThreads statement
            //should be removed.
            dynamic _ps = ps;
            var ts = PythonEngine.BeginAllowThreads();
            using (Py.GIL())
            {
                _ps.res = 0;
                _ps.bb = 100;
                _ps.th_cnt = 0;
                //add function to the scope 
                //can be call many times, more efficient than ast 
                ps.Exec(
                    "def update():\n" +
                    "    global res, th_cnt\n" +
                    "    res += bb + 1\n" +
                    "    th_cnt += 1\n"
                );
            }
            int th_cnt = 3;
            for (int i =0; i< th_cnt; i++)
            {
                System.Threading.Thread th = new System.Threading.Thread(()=>
                {
                    using (Py.GIL())
                    {
                        //ps.GetVariable<dynamic>("update")(); //call the scope function dynamicly
                        _ps.update();
                    }
                });
                th.Start();
            }
            //equivalent to Thread.Join, make the main thread join the GIL competition
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
