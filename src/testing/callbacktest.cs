using Python.Runtime;

namespace Python.Test
{
    /// <summary>
    /// Tests callbacks into python code.
    /// </summary>
    public class CallbackTest
    {
        public string Call_simpleDefaultArg_WithNull(string moduleName)
        {
            using (Py.GIL())
            {
                dynamic module = Py.Import(moduleName);
                return module.simpleDefaultArg(null);
            }
        }

        public string Call_simpleDefaultArg_WithEmptyArgs(string moduleName)
        {
            using (Py.GIL())
            {
                dynamic module = Py.Import(moduleName);
                return module.simpleDefaultArg();
            }
        }
    }

    /// <summary>
    /// Tests calling from Python into C# and back into Python using a PyObject.
    /// SelfCallbackTest should be inherited by a Python class.
    /// Used in test_class.py / testCallback
    /// </summary>
    public class SelfCallbackTest
    {
        public void Callback(PyObject self)
        {
            using (Py.GIL())
            {
                ((dynamic)self).PyCallback(self);
            }
        }
    }
}
