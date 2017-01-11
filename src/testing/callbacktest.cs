using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Python.Test
{
    //========================================================================
    // Tests callbacks into python code.
    //========================================================================

    public class CallbackTest
    {
        public string Call_simpleDefaultArg_WithNull(string moduleName)
        {
            using (Runtime.Py.GIL())
            {
                dynamic module = Runtime.Py.Import(moduleName);
                return module.simpleDefaultArg(null);
            }
        }
        public string Call_simpleDefaultArg_WithEmptyArgs(string moduleName)
        {
            using (Runtime.Py.GIL())
            {
                dynamic module = Runtime.Py.Import(moduleName);
                return module.simpleDefaultArg();
            }
        }
    }

    //==========================================================================
    // Tests calling from Python into C# and back into Python using a PyObject.
    // SelfCallbackTest should be inherited by a Python class. 
    // Used in test_class.py / testCallback
    //==========================================================================
    public class SelfCallbackTest
    {
        public void Callback(Runtime.PyObject self)
        {
            using (Runtime.Py.GIL())
                ((dynamic)self).PyCallback(self);
        }
    }
}
