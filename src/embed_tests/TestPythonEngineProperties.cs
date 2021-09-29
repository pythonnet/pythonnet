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
            PythonEngine.Initialize();
            string s = PythonEngine.PythonPath;

            StringAssert.Contains("python", s.ToLower());
            PythonEngine.Shutdown();
        }

        [Test]
        public static void GetProgramNameDefault()
        {
            PythonEngine.Initialize();
            string s = PythonEngine.ProgramName;

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

        [Test]
        public void SetPythonHome()
        {
            // We needs to ensure that engine was started and shutdown at least once before setting dummy home.
            // Otherwise engine will not run with dummy path with random problem.
            if (!PythonEngine.IsInitialized)
            {
                PythonEngine.Initialize();
            }

            PythonEngine.Shutdown();

            var pythonHomeBackup = PythonEngine.PythonHome;

            var pythonHome = "/dummypath/";

            PythonEngine.PythonHome = pythonHome;
            PythonEngine.Initialize();

            PythonEngine.Shutdown();

            // Restoring valid pythonhome.
            PythonEngine.PythonHome = pythonHomeBackup;
        }

        [Test]
        public void SetPythonHomeTwice()
        {
            // We needs to ensure that engine was started and shutdown at least once before setting dummy home.
            // Otherwise engine will not run with dummy path with random problem.
            if (!PythonEngine.IsInitialized)
            {
                PythonEngine.Initialize();
            }
            PythonEngine.Shutdown();

            var pythonHomeBackup = PythonEngine.PythonHome;

            var pythonHome = "/dummypath/";

            PythonEngine.PythonHome = "/dummypath2/";
            PythonEngine.PythonHome = pythonHome;
            PythonEngine.Initialize();

            Assert.AreEqual(pythonHome, PythonEngine.PythonHome);
            PythonEngine.Shutdown();

            PythonEngine.PythonHome = pythonHomeBackup;
        }

        [Test]
        public void SetProgramName()
        {
            if (PythonEngine.IsInitialized)
            {
                PythonEngine.Shutdown();
            }

            var programNameBackup = PythonEngine.ProgramName;

            var programName = "FooBar";

            PythonEngine.ProgramName = programName;
            PythonEngine.Initialize();

            Assert.AreEqual(programName, PythonEngine.ProgramName);
            PythonEngine.Shutdown();

            PythonEngine.ProgramName = programNameBackup;
        }

        [Test]
        public void SetPythonPath()
        {
            PythonEngine.Initialize();

            const string moduleName = "pytest";
            bool importShouldSucceed;
            try
            {
                Py.Import(moduleName);
                importShouldSucceed = true;
            }
            catch
            {
                importShouldSucceed = false;
            }

            string[] paths = Py.Import("sys").GetAttr("path").As<string[]>();
            string path = string.Join(System.IO.Path.PathSeparator.ToString(), paths);

            // path should not be set to PythonEngine.PythonPath here.
            // PythonEngine.PythonPath gets the default module search path, not the full search path.
            // The list sys.path is initialized with this value on interpreter startup;
            // it can be (and usually is) modified later to change the search path for loading modules.
            // See https://docs.python.org/3/c-api/init.html#c.Py_GetPath
            // After PythonPath is set, then PythonEngine.PythonPath will correctly return the full search path. 

            PythonEngine.Shutdown();

            PythonEngine.PythonPath = path;
            PythonEngine.Initialize();

            Assert.AreEqual(path, PythonEngine.PythonPath);
            if (importShouldSucceed) Py.Import(moduleName);

            PythonEngine.Shutdown();
        }
    }
}
