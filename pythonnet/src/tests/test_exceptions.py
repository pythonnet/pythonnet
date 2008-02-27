# ===========================================================================
# This software is subject to the provisions of the Zope Public License,
# Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
# THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
# WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
# FOR A PARTICULAR PURPOSE.
# ===========================================================================

import sys, os, string, unittest, types
import System

# Note: all of these tests are known to fail because Python currently
# doesn't allow new-style classes to be used as exceptions. I'm leaving
# the tests in place in to document 'how it ought to work' in the hopes
# that they'll all pass one day...

class ExceptionTests(unittest.TestCase):
    """Test exception support."""

    def testUnifiedExceptionSemantics(self):
        """Test unified exception semantics."""
        from System import Exception, Object
        import exceptions
        
        e = Exception('Something bad happened')
        self.failUnless(isinstance(e, exceptions.Exception))
        self.failUnless(isinstance(e, Exception))


    def testStandardExceptionAttributes(self):
        """Test accessing standard exception attributes."""
        from System import OverflowException
        from Python.Test import ExceptionTest

        e = ExceptionTest.GetExplicitException()
        self.failUnless(isinstance(e, OverflowException))

        self.failUnless(e.Message == 'error')

        e.Source = 'Test Suite'
        self.failUnless(e.Source == 'Test Suite')

        v = e.ToString()
        self.failUnless(len(v) > 0)
                       

    def testExtendedExceptionAttributes(self):
        """Test accessing extended exception attributes."""
        from Python.Test import ExceptionTest, ExtendedException
        from System import Exception, OverflowException
        import exceptions
        
        e = ExceptionTest.GetExtendedException()
        self.failUnless(isinstance(e, ExtendedException))
        self.failUnless(isinstance(e, OverflowException))
        self.failUnless(isinstance(e, Exception))
        
        self.failUnless(e.Message == 'error')

        e.Source = 'Test Suite'
        self.failUnless(e.Source == 'Test Suite')

        v = e.ToString()
        self.failUnless(len(v) > 0)

        self.failUnless(e.ExtraProperty == 'extra')
        e.ExtraProperty = 'changed'
        self.failUnless(e.ExtraProperty == 'changed')
                       
        self.failUnless(e.GetExtraInfo() == 'changed')
        
                        
    def testRaiseClassException(self):
        """Test class exception propagation."""
        from System import NullReferenceException

        def test():
            raise NullReferenceException

        self.failUnlessRaises(NullReferenceException, test)

        try:
            raise NullReferenceException
        except:
            type, value, tb = sys.exc_info()
            self.failUnless(type is NullReferenceException)
            self.failUnless(isinstance(value, NullReferenceException))


    def testRaiseClassExceptionWithValue(self):
        """Test class exception propagation with associated value."""
        from System import NullReferenceException

        def test():
            raise NullReferenceException, 'Aiiieee!'

        self.failUnlessRaises(NullReferenceException, test)

        try:
            raise NullReferenceException('Aiiieee!')
        except:
            type, value, tb = sys.exc_info()
            self.failUnless(type is NullReferenceException)
            self.failUnless(isinstance(value, NullReferenceException))
            self.failUnless(value.Message == 'Aiiieee!')


    def testRaiseInstanceException(self):
        """Test instance exception propagation."""
        from System import NullReferenceException

        def test():
            raise NullReferenceException()

        self.failUnlessRaises(NullReferenceException, test)

        try:
            raise NullReferenceException()
        except:
            type, value, tb = sys.exc_info()
            self.failUnless(type is NullReferenceException)
            self.failUnless(isinstance(value, NullReferenceException))
            self.failUnless(len(value.Message) > 0)


    def testRaiseInstanceExceptionWithArgs(self):
        """Test instance exception propagation with args."""
        from System import NullReferenceException

        def test():
            raise NullReferenceException("Aiieeee!")

        self.failUnlessRaises(NullReferenceException, test)

        try:
            raise NullReferenceException('Aiiieee!')
        except:
            type, value, tb = sys.exc_info()
            self.failUnless(type is NullReferenceException)
            self.failUnless(isinstance(value, NullReferenceException))
            self.failUnless(value.Message == 'Aiiieee!')


    def testManagedExceptionPropagation(self):
        """Test propagation of exceptions raised in managed code."""
        from System import Decimal, OverflowException

        def test():
            l = Decimal.ToInt64(Decimal.MaxValue)

        self.failUnlessRaises(OverflowException, test)


    def testManagedExceptionConversion(self):
        """Test conversion of managed exceptions."""
        from System import Exception, OverflowException
        from Python.Test import ExceptionTest

        e = ExceptionTest.GetBaseException()
        self.failUnless(isinstance(e, Exception))

        e = ExceptionTest.GetExplicitException()
        self.failUnless(isinstance(e, OverflowException))
        self.failUnless(isinstance(e, Exception))

        e = ExceptionTest.GetWidenedException()
        self.failUnless(isinstance(e, OverflowException))
        self.failUnless(isinstance(e, Exception))

        v = ExceptionTest.SetBaseException(Exception('error'))
        self.failUnless(v)

        v = ExceptionTest.SetExplicitException(OverflowException('error'))
        self.failUnless(v)

        v = ExceptionTest.SetWidenedException(OverflowException('error'))
        self.failUnless(v)     


    def testCatchExceptionFromManagedMethod(self):
        """Test catching an exception from a managed method."""
        from Python.Test import ExceptionTest
        from System import OverflowException

        try:
            ExceptionTest().ThrowException()
        except OverflowException, e:
            self.failUnless(isinstance(e, OverflowException))
            return

        raise SystemError('failed to catch exception from managed method')


    def testCatchExceptionFromManagedProperty(self):
        """Test catching an exception from a managed property."""
        from Python.Test import ExceptionTest
        from System import OverflowException

        try:
            v = ExceptionTest().ThrowProperty
        except OverflowException, e:
            self.failUnless(isinstance(e, OverflowException))
            return

        try:
            ExceptionTest().ThrowProperty = 1
        except OverflowException, e:
            self.failUnless(isinstance(e, OverflowException))
            return

        raise SystemError('failed to catch exception from managed property')


    def testCatchExceptionManagedClass(self):
        """Test catching the managed class of an exception."""
        from System import OverflowException

        try:
            raise OverflowException('overflow')
        except OverflowException:
            return

        raise SystemError('failed to catch managed class exception')


    def testCatchExceptionPythonClass(self):
        """Test catching the python class of an exception."""
        from System import OverflowException
        from exceptions import Exception

        try:
            raise OverflowException('overflow')
        except Exception:
            return

        raise SystemError('failed to catch python class exception')


    def testCatchExceptionBaseClass(self):
        """Test catching the base of an exception."""
        from System import OverflowException, ArithmeticException

        try:
            raise OverflowException('overflow')
        except ArithmeticException:
            return

        raise SystemError('failed to catch base exception')


    def testCatchExceptionNestedBaseClass(self):
        """Test catching the nested base of an exception."""
        from System import OverflowException, SystemException

        try:
            raise OverflowException('overflow')
        except SystemException:
            return

        raise SystemError('failed to catch nested base exception')


    def testCatchExceptionWithAssignment(self):
        """Test catching an exception with assignment."""
        from System import OverflowException

        try:
            raise OverflowException('overflow')
        except OverflowException, e:
            self.failUnless(isinstance(e, OverflowException))


    def testCatchExceptionUnqualified(self):
        """Test catching an unqualified exception."""
        from System import OverflowException

        try:
            raise OverflowException('overflow')
        except:
            return

        raise SystemError('failed to catch unqualified exception')


    def testApparentModuleOfException(self):
        """Test the apparent module of an exception."""
        from System import Exception, OverflowException

        self.failUnless(Exception.__module__ == 'System')
        self.failUnless(OverflowException.__module__ == 'System')


    def testStrOfException(self):
        """Test the str() representation of an exception."""
        from System import NullReferenceException
        from System import Convert, FormatException
        e = NullReferenceException('')
        self.failUnlessEqual(str(e), '')

        e = NullReferenceException('Something bad happened')
        self.failUnless(str(e).startswith('Something bad happened'))

        try:
            Convert.ToDateTime('this will fail')
        except FormatException, e:
            msg = unicode(e).encode("utf8") # fix for international installation
            self.failUnless(msg.find('System.Convert.ToDateTime') > -1, msg)

            
    def testPythonCompatOfManagedExceptions(self):
        """Test if managed exceptions are compatible with Python's implementation
        """
        from System import OverflowException
        msg = "A simple message"
        
        e = OverflowException(msg)
        self.failUnlessEqual(e.message, msg)
        self.failUnless(isinstance(e.message, unicode)) # ???
        self.failUnlessEqual(str(e), msg)
        self.failUnlessEqual(unicode(e), msg)
        
        self.failUnlessEqual(e.args, (msg,))
        self.failUnless(isinstance(e.args, tuple))
        self.failUnlessEqual(repr(e), "OverflowException('A simple message',)")

    def testExceptionIsInstanceOfSystemObject(self):
        """Test behavior of isinstance(<managed exception>, System.Object)."""
        # This is an anti-test, in that this is a caveat of the current
        # implementation. Because exceptions are not allowed to be new-style
        # classes, we wrap managed exceptions in a general-purpose old-style
        # class that delegates to the wrapped object. This makes _almost_
        # everything work as expected, except that an isinstance check against
        # CLR.System.Object will fail for a managed exception (because a new
        # style class cannot appear in the __bases__ of an old-style class
        # without causing a crash in the CPython interpreter). This test is
        # here mainly to remind me to update the caveat in the documentation
        # one day when when exceptions can be new-style classes.
        from System import OverflowException
        from System import Object
        
        o = OverflowException('error')
        self.failIf(isinstance(o, Object))
    
        

def test_suite():
    return unittest.makeSuite(ExceptionTests)

def main():
    unittest.TextTestRunner().run(test_suite())

if __name__ == '__main__':
    main()
