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

                Assert.True(s.Length > 5);
                Assert.True(s.Contains(","));
            }
        }

        [Test]
        public static void GetCompilerDoesntCrash()
        {
            using (Py.GIL())
            {
                string s = PythonEngine.Compiler;

                Assert.True(s.Length > 0);
                Assert.True(s.Contains("["));
                Assert.True(s.Contains("]"));
            }
        }

        [Test]
        public static void GetCopyrightDoesntCrash()
        {
            using (Py.GIL())
            {
                string s = PythonEngine.Copyright;

                Assert.True(s.Length > 0);
                Assert.True(s.Contains("Python Software Foundation"));
            }
        }

        [Test]
        public static void GetPlatformDoesntCrash()
        {
            using (Py.GIL())
            {
                string s = PythonEngine.Platform;

                Assert.True(s.Length > 0);
                Assert.True(s.Contains("x") || s.Contains("win"));
            }
        }

        [Test]
        public static void GetVersionDoesntCrash()
        {
            using (Py.GIL())
            {
                string s = PythonEngine.Version;

                Assert.True(s.Length > 0);
                Assert.True(s.Contains(","));
            }
        }

        [Test]
        public static void GetPythonPathDefault()
        {
            string s = PythonEngine.PythonPath;

            StringAssert.Contains("python", s.ToLower());
        }

        [Test]
        public static void GetProgramNameDefault()
        {
            string s = PythonEngine.ProgramName;

            Assert.NotNull(s);
        }

        /// <summary>
        /// Test default behavior of PYTHONHOME. If ENVVAR is set it will
        /// return the same value. If not, returns EmptyString.
        /// </summary>
        [Test]
        public static void GetPythonHomeDefault()
        {
            string envPythonHome = Environment.GetEnvironmentVariable("PYTHONHOME") ?? "";

            string enginePythonHome = PythonEngine.PythonHome;

            Assert.AreEqual(envPythonHome, enginePythonHome);
        }
    }
}
