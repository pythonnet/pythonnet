using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;

namespace Python.EmbeddingPythonTest
{
    public class TestNativeTypeOffset
    {
#if WINDOWS
        // The code for NativeTypeOffset is not generated under Windows because sys.abiflags does not exist (see setup.py)
#else
        /// <summary>
        /// Tests that installation has generated code for NativeTypeOffset and that it can be loaded.
        /// </summary>        
        [Test]
        public void LoadNativeTypeOffsetClass()
        {
            new Python.Runtime.NativeTypeOffset();
        }
#endif
    }
}
