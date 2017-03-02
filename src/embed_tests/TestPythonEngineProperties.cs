using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestPythonEngineProperties
    {
        [Test]
        public static void GetBuildinfoDoesntCrash()
        {
            using (Py.GIL())
            {
                string s = PythonEngine.BuildInfo;

                Assert.IsTrue(s.Length > 5);
                Assert.IsTrue(s.Contains(","));
            }
        }

        [Test]
        public static void GetCompilerDoesntCrash()
        {
            using (Py.GIL())
            {
                string s = PythonEngine.Compiler;

                Assert.IsTrue(s.Length > 0);
                Assert.IsTrue(s.Contains("["));
                Assert.IsTrue(s.Contains("]"));
            }
        }

        [Test]
        public static void GetCopyrightDoesntCrash()
        {
            using (Py.GIL())
            {
                string s = PythonEngine.Copyright;

                Assert.IsTrue(s.Length > 0);
                Assert.IsTrue(s.Contains("Python Software Foundation"));
            }
        }

        [Test]
        public static void GetPlatformDoesntCrash()
        {
            using (Py.GIL())
            {
                string s = PythonEngine.Platform;

                Assert.IsTrue(s.Length > 0);
                Assert.IsTrue(s.Contains("x") || s.Contains("win"));
            }
        }

        [Test]
        public static void GetVersionDoesntCrash()
        {
            using (Py.GIL())
            {
                string s = PythonEngine.Version;

                Assert.IsTrue(s.Length > 0);
                Assert.IsTrue(s.Contains(","));
            }
        }

        [Test]
        public static void GetPythonPathDefault()
        {
            PythonEngine.Initialize();
            string s = PythonEngine.PythonPath;

            StringAssert.Contains("python", s.ToLower());
            PythonEngine.Shutdown();
        }

        [Test]
        public static void GetProgramNameDefault()
        {
            PythonEngine.Initialize();
            string s = PythonEngine.PythonHome;

            Assert.NotNull(s);
            PythonEngine.Shutdown();
        }

        /// <summary>
        /// Test default behavior of PYTHONHOME. If ENVVAR is set it will
        /// return the same value. If not, returns EmptyString.
        /// </summary>
        /// <remarks>
        /// AppVeyor.yml has been update to tests with ENVVAR set.
        /// </remarks>
        [Test]
        public static void GetPythonHomeDefault()
        {
            string envPythonHome = Environment.GetEnvironmentVariable("PYTHONHOME") ?? "";

            PythonEngine.Initialize();
            string enginePythonHome = PythonEngine.PythonHome;

            Assert.AreEqual(envPythonHome, enginePythonHome);
            PythonEngine.Shutdown();
        }
    }
}
