# -*- coding: utf-8 -*-

import unittest

from Python.Test import EventTest, TestEventArgs

from _compat import range
from utils import (CallableHandler, ClassMethodHandler, GenericHandler,
                   MultipleHandler, StaticMethodHandler, VarCallableHandler,
                   VariableArgsHandler)


class EventTests(unittest.TestCase):
    """Test CLR event support."""

    def testPublicInstanceEvent(self):
        """Test public instance events."""
        ob = EventTest()

        handler = GenericHandler()
        self.assertTrue(handler.value == None)

        ob.PublicEvent += handler.handler

        ob.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 10)

        ob.PublicEvent -= handler.handler

    def testPublicStaticEvent(self):
        """Test public static events."""
        handler = GenericHandler()
        self.assertTrue(handler.value == None)

        EventTest.PublicStaticEvent += handler.handler

        EventTest.OnPublicStaticEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 10)

    def testProtectedInstanceEvent(self):
        """Test protected instance events."""
        ob = EventTest()

        handler = GenericHandler()
        self.assertTrue(handler.value == None)

        ob.ProtectedEvent += handler.handler

        ob.OnProtectedEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 10)

        ob.ProtectedEvent -= handler.handler

    def testProtectedStaticEvent(self):
        """Test protected static events."""
        ob = EventTest

        handler = GenericHandler()
        self.assertTrue(handler.value == None)

        EventTest.ProtectedStaticEvent += handler.handler

        EventTest.OnProtectedStaticEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 10)

        EventTest.ProtectedStaticEvent -= handler.handler

    def testInternalEvents(self):
        """Test internal events."""

        def test():
            f = EventTest().InternalEvent

        self.assertRaises(AttributeError, test)

        def test():
            f = EventTest().InternalStaticEvent

        self.assertRaises(AttributeError, test)

        def test():
            f = EventTest.InternalStaticEvent

        self.assertRaises(AttributeError, test)

    def testPrivateEvents(self):
        """Test private events."""

        def test():
            f = EventTest().PrivateEvent

        self.assertRaises(AttributeError, test)

        def test():
            f = EventTest().PrivateStaticEvent

        self.assertRaises(AttributeError, test)

        def test():
            f = EventTest.PrivateStaticEvent

        self.assertRaises(AttributeError, test)

    def testMulticastEvent(self):
        """Test multicast events."""
        ob = EventTest()

        handler1 = GenericHandler()
        handler2 = GenericHandler()
        handler3 = GenericHandler()

        ob.PublicEvent += handler1.handler
        ob.PublicEvent += handler2.handler
        ob.PublicEvent += handler3.handler

        ob.OnPublicEvent(TestEventArgs(10))

        self.assertTrue(handler1.value == 10)
        self.assertTrue(handler2.value == 10)
        self.assertTrue(handler3.value == 10)

        ob.OnPublicEvent(TestEventArgs(20))

        self.assertTrue(handler1.value == 20)
        self.assertTrue(handler2.value == 20)
        self.assertTrue(handler3.value == 20)

        ob.PublicEvent -= handler1.handler
        ob.PublicEvent -= handler2.handler
        ob.PublicEvent -= handler3.handler

    def testInstanceMethodHandler(self):
        """Test instance method handlers."""
        ob = EventTest()
        handler = GenericHandler()

        ob.PublicEvent += handler.handler
        self.assertTrue(handler.value == None)

        ob.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 10)

        ob.PublicEvent -= handler.handler
        self.assertTrue(handler.value == 10)

        ob.OnPublicEvent(TestEventArgs(20))
        self.assertTrue(handler.value == 10)

    def testVarArgsInstanceMethodHandler(self):
        """Test vararg instance method handlers."""
        ob = EventTest()
        handler = VariableArgsHandler()

        ob.PublicEvent += handler.handler
        self.assertTrue(handler.value == None)

        ob.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 10)

        ob.PublicEvent -= handler.handler
        self.assertTrue(handler.value == 10)

        ob.OnPublicEvent(TestEventArgs(20))
        self.assertTrue(handler.value == 10)

    def testCallableobHandler(self):
        """Test callable ob handlers."""
        ob = EventTest()
        handler = CallableHandler()

        ob.PublicEvent += handler
        self.assertTrue(handler.value == None)

        ob.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 10)

        ob.PublicEvent -= handler
        self.assertTrue(handler.value == 10)

        ob.OnPublicEvent(TestEventArgs(20))
        self.assertTrue(handler.value == 10)

    def testVarArgsCallableHandler(self):
        """Test varargs callable handlers."""
        ob = EventTest()
        handler = VarCallableHandler()

        ob.PublicEvent += handler
        self.assertTrue(handler.value == None)

        ob.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 10)

        ob.PublicEvent -= handler
        self.assertTrue(handler.value == 10)

        ob.OnPublicEvent(TestEventArgs(20))
        self.assertTrue(handler.value == 10)

    def testStaticMethodHandler(self):
        """Test static method handlers."""
        ob = EventTest()
        handler = StaticMethodHandler()
        StaticMethodHandler.value = None

        ob.PublicEvent += handler.handler
        self.assertTrue(handler.value == None)

        ob.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 10)

        ob.PublicEvent -= handler.handler
        self.assertTrue(handler.value == 10)

        ob.OnPublicEvent(TestEventArgs(20))
        self.assertTrue(handler.value == 10)

    def testClassMethodHandler(self):
        """Test class method handlers."""
        ob = EventTest()
        handler = ClassMethodHandler()
        ClassMethodHandler.value = None

        ob.PublicEvent += handler.handler
        self.assertTrue(handler.value == None)

        ob.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 10)

        ob.PublicEvent -= handler.handler
        self.assertTrue(handler.value == 10)

        ob.OnPublicEvent(TestEventArgs(20))
        self.assertTrue(handler.value == 10)

    def testManagedInstanceMethodHandler(self):
        """Test managed instance method handlers."""
        ob = EventTest()

        ob.PublicEvent += ob.GenericHandler
        self.assertTrue(ob.value == 0)

        ob.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(ob.value == 10)

        ob.PublicEvent -= ob.GenericHandler
        self.assertTrue(ob.value == 10)

        ob.OnPublicEvent(TestEventArgs(20))
        self.assertTrue(ob.value == 10)

    def testManagedStaticMethodHandler(self):
        """Test managed static method handlers."""
        ob = EventTest()
        EventTest.s_value = 0

        ob.PublicEvent += ob.StaticHandler
        self.assertTrue(EventTest.s_value == 0)

        ob.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(EventTest.s_value == 10)

        ob.PublicEvent -= ob.StaticHandler
        self.assertTrue(EventTest.s_value == 10)

        ob.OnPublicEvent(TestEventArgs(20))
        self.assertTrue(EventTest.s_value == 10)

    def testUnboundMethodHandler(self):
        """Test failure mode for unbound method handlers."""
        ob = EventTest()
        ob.PublicEvent += GenericHandler.handler
        try:
            ob.OnPublicEvent(TestEventArgs(10))
        except TypeError:
            ob.PublicEvent -= GenericHandler.handler
            return

        raise TypeError("should have raised a TypeError")

    def testFunctionHandler(self):
        """Test function handlers."""
        ob = EventTest()
        dict = {'value': None}

        def handler(sender, args, dict=dict):
            dict['value'] = args.value

        ob.PublicEvent += handler
        self.assertTrue(dict['value'] == None)

        ob.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(dict['value'] == 10)

        ob.PublicEvent -= handler
        self.assertTrue(dict['value'] == 10)

        ob.OnPublicEvent(TestEventArgs(20))
        self.assertTrue(dict['value'] == 10)

    def testAddNonCallableHandler(self):
        """Test handling of attempts to add non-callable handlers."""

        def test():
            ob = EventTest()
            ob.PublicEvent += 10

        self.assertRaises(TypeError, test)

        def test():
            ob = EventTest()
            ob.PublicEvent += "spam"

        self.assertRaises(TypeError, test)

        def test():
            class spam(object):
                pass

            ob = EventTest()
            ob.PublicEvent += spam()

        self.assertRaises(TypeError, test)

    def testRemoveMultipleHandlers(self):
        """Test removing multiple instances of the same handler."""
        ob = EventTest()
        handler = MultipleHandler()

        h1 = handler.handler
        ob.PublicEvent += h1

        h2 = handler.handler
        ob.PublicEvent += h2

        ob.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 20)

        ob.PublicEvent -= h1

        ob.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 30)

        ob.PublicEvent -= h2

        ob.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 30)

        # try again, removing in a different order.

        ob = EventTest()
        handler = MultipleHandler()

        h1 = handler.handler
        ob.PublicEvent += h1

        h2 = handler.handler
        ob.PublicEvent += h2

        ob.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 20)

        ob.PublicEvent -= h2

        ob.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 30)

        ob.PublicEvent -= h1

        ob.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 30)

    def testRemoveMultipleStaticHandlers(self):
        """Test removing multiple instances of a static handler."""
        ob = EventTest()
        handler = MultipleHandler()

        h1 = handler.handler
        ob.PublicStaticEvent += h1

        h2 = handler.handler
        ob.PublicStaticEvent += h2

        ob.OnPublicStaticEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 20)

        ob.PublicStaticEvent -= h1

        ob.OnPublicStaticEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 30)

        ob.PublicStaticEvent -= h2

        ob.OnPublicStaticEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 30)

        # try again, removing in a different order.

        ob = EventTest()
        handler = MultipleHandler()

        h1 = handler.handler
        ob.PublicStaticEvent += h1

        h2 = handler.handler
        ob.PublicStaticEvent += h2

        ob.OnPublicStaticEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 20)

        ob.PublicStaticEvent -= h2

        ob.OnPublicStaticEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 30)

        ob.PublicStaticEvent -= h1

        ob.OnPublicStaticEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 30)

    def testRandomMultipleHandlers(self):
        """Test random subscribe / unsubscribe of the same handlers."""
        import random
        ob = EventTest()
        handler = MultipleHandler()
        handler2 = MultipleHandler()

        ob.PublicEvent += handler2.handler
        ob.PublicEvent += handler2.handler

        handlers = []
        for i in range(30):
            method = handler.handler
            ob.PublicEvent += method
            handlers.append(method)

        ob.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 300)
        self.assertTrue(handler2.value == 20)
        handler.value = 0
        handler2.value = 0

        for i in range(30):
            item = random.choice(handlers)
            handlers.remove(item)
            ob.PublicEvent -= item
            handler.value = 0
            ob.OnPublicEvent(TestEventArgs(10))
            self.assertTrue(handler.value == (len(handlers) * 10))
            self.assertTrue(handler2.value == ((i + 1) * 20))

        handler2.value = 0
        ob.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler2.value == 20)

        ob.PublicEvent -= handler2.handler

        handler2.value = 0
        ob.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler2.value == 10)

        ob.PublicEvent -= handler2.handler

        handler2.value = 0
        ob.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler2.value == 0)

    def testRemoveInternalCallHandler(self):
        """Test remove on an event sink implemented w/internalcall."""
        ob = EventTest()

        def h(sender, args):
            pass

        ob.PublicEvent += h
        ob.PublicEvent -= h

    def testRemoveUnknownHandler(self):
        """Test removing an event handler that was never added."""

        def test():
            ob = EventTest()
            handler = GenericHandler()

            ob.PublicEvent -= handler.handler

        self.assertRaises(ValueError, test)

    def testHandlerCallbackFailure(self):
        """Test failure mode for inappropriate handlers."""

        class BadHandler(object):
            def handler(self, one):
                return 'too many'

        ob = EventTest()
        handler = BadHandler()

        def test():
            ob.PublicEvent += handler.handler
            ob.OnPublicEvent(TestEventArgs(10))

        self.assertRaises(TypeError, test)

        ob.PublicEvent -= handler.handler

        class BadHandler(object):
            def handler(self, one, two, three, four, five):
                return 'not enough'

        ob = EventTest()
        handler = BadHandler()

        def test():
            ob.PublicEvent += handler.handler
            ob.OnPublicEvent(TestEventArgs(10))

        self.assertRaises(TypeError, test)

        ob.PublicEvent -= handler.handler

    def testIncorrectInvokation(self):
        """Test incorrect invocation of events."""
        ob = EventTest()

        handler = GenericHandler()
        ob.PublicEvent += handler.handler

        def test():
            ob.OnPublicEvent()

        self.assertRaises(TypeError, test)

        def test():
            ob.OnPublicEvent(32)

        self.assertRaises(TypeError, test)

        ob.PublicEvent -= handler.handler

    def testExplicitCLSEventRegistration(self):
        """Test explicit CLS event registration."""
        from Python.Test import TestEventHandler

        ob = EventTest()
        handler = GenericHandler()

        delegate = TestEventHandler(handler.handler)
        ob.add_PublicEvent(delegate)
        self.assertTrue(handler.value == None)

        ob.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 10)

        ob.remove_PublicEvent(delegate)
        self.assertTrue(handler.value == 10)

        ob.OnPublicEvent(TestEventArgs(20))
        self.assertTrue(handler.value == 10)

    def testImplicitCLSEventRegistration(self):
        """Test implicit CLS event registration."""

        def test():
            ob = EventTest()
            handler = GenericHandler()
            ob.add_PublicEvent(handler.handler)

        self.assertRaises(TypeError, test)

    def testEventDescriptorAbuse(self):
        """Test event descriptor abuse."""

        def test():
            del EventTest.PublicEvent

        self.assertRaises(TypeError, test)

        def test():
            del EventTest.__dict__['PublicEvent']

        self.assertRaises(TypeError, test)

        desc = EventTest.__dict__['PublicEvent']

        def test():
            desc.__get__(0, 0)

        self.assertRaises(TypeError, test)

        def test():
            desc.__set__(0, 0)

        self.assertRaises(TypeError, test)

        def test():
            ob = EventTest()
            ob.PublicEvent = 0

        self.assertRaises(TypeError, test)

        def test():
            EventTest.PublicStaticEvent = 0

        self.assertRaises(TypeError, test)


def test_suite():
    return unittest.makeSuite(EventTests)
