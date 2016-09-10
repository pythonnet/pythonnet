using System;
using System.Collections;
using Python.Runtime;

namespace Python.Test
{
    //========================================================================
    // Supports CLR threading / reentrancy unit tests.
    //========================================================================

    public class ThreadTest
    {
        private static PyObject module;

        private static string testmod =
            "import CLR\n" +
            "from CLR.Python.Test import ThreadTest\n" +
            "\n" +
            "def echostring(value):\n" +
            "    return value\n" +
            "\n" +
            "def echostring2(value):\n" +
            "    return ThreadTest.CallEchoString(value)\n" +
            "\n";


        // This method calls back into the CPython runtime - tests
        // call this from Python to check that we don't hang on
        // nested transitions from managed to Python code and back.

        public static string CallEchoString(string arg)
        {
            IntPtr gs = PythonEngine.AcquireLock();
            try
            {
                if (module == null)
                {
                    module = PythonEngine.ModuleFromString("tt", testmod);
                }
                PyObject func = module.GetAttr("echostring");
                PyString parg = new PyString(arg);
                PyObject temp = func.Invoke(parg);
                string result = (string)temp.AsManagedObject(typeof(String));
                func.Dispose();
                parg.Dispose();
                temp.Dispose();
                return result;
            }
            finally
            {
                PythonEngine.ReleaseLock(gs);
            }
        }

        public static string CallEchoString2(string arg)
        {
            IntPtr gs = PythonEngine.AcquireLock();
            try
            {
                if (module == null)
                {
                    module = PythonEngine.ModuleFromString("tt", testmod);
                }

                PyObject func = module.GetAttr("echostring2");
                PyString parg = new PyString(arg);
                PyObject temp = func.Invoke(parg);
                string result = (string)temp.AsManagedObject(typeof(String));
                func.Dispose();
                parg.Dispose();
                temp.Dispose();
                return result;
            }
            finally
            {
                PythonEngine.ReleaseLock(gs);
            }
        }
    }
}