# ===========================================================================
# This software is subject to the provisions of the Zope Public License,
# Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
# THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
# WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
# FOR A PARTICULAR PURPOSE.
# ===========================================================================

import sys, os, string, unittest, types
from Python.Runtime import PythonEngine

# XXX This test module isn't used!

class EngineTests(unittest.TestCase):
    """Test PythonEngine embedding APIs."""

    def testMultipleCallsToInitialize(self):
        """Test that multiple initialize calls are harmless."""
        PythonEngine.Initialize();
        PythonEngine.Initialize();
        PythonEngine.Initialize();

    def testImportModule(self):
        """Test module import."""
        m = PythonEngine.ImportModule("sys")
        n = m.GetAttr("__name__")
        self.failUnless(n.AsManagedObject(System.String) == "sys")


    def testRunString(self):
        """Test the RunString method."""
        PythonEngine.AcquireLock()
        
        code = "import sys; sys.singleline_worked = 1"
        PythonEngine.RunString(code)
        self.failUnless(sys.singleline_worked == 1)
        
        code = "import sys\nsys.multiline_worked = 1"
        PythonEngine.RunString(code)
        self.failUnless(sys.multiline_worked == 1)

        PythonEngine.ReleaseLock()
        

def test_suite():
    return unittest.makeSuite(EngineTests)

def main():
    unittest.TextTestRunner().run(test_suite())

if __name__ == '__main__':
    main()

