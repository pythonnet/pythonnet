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
}
