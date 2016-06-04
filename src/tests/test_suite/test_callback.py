import unittest, sys
import clr

this_module = sys.modules[__name__]
clr.AddReference("Python.Test")
import Python.Test as Test
from Python.Test import CallbackTest
test_instance = CallbackTest()

def simpleDefaultArg(arg = 'test'):
    return arg

class CallbackTests(unittest.TestCase):
    """Test that callbacks from C# into python work."""

    def testDefaultForNull(self):
        """Test that C# can use null for an optional python argument"""
        retVal = test_instance.Call_simpleDefaultArg_WithNull(__name__)
        pythonRetVal = simpleDefaultArg(None)
        self.assertEquals(retVal, pythonRetVal)

    def testDefaultForNone(self):
        """Test that C# can use no argument for an optional python argument"""
        retVal = test_instance.Call_simpleDefaultArg_WithEmptyArgs(__name__)
        pythonRetVal = simpleDefaultArg()
        self.assertEquals(retVal, pythonRetVal)

def test_suite():
    return unittest.makeSuite(CallbackTests)

