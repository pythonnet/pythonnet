import clr

clr.AddReference('Python.Test')

import sys, os, string, unittest, types
from Python.Test import EventTest, TestEventHandler
from Python.Test import TestEventArgs


class EventTests(unittest.TestCase):
    """Test CLR event support."""

    def testPublicInstanceEvent(self):
        """Test public instance events."""
        object = EventTest()

        handler = GenericHandler()
        self.assertTrue(handler.value == None)

        object.PublicEvent += handler.handler

        object.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 10)

        object.PublicEvent -= handler.handler

    def testPublicStaticEvent(self):
        """Test public static events."""
        handler = GenericHandler()
        self.assertTrue(handler.value == None)

        EventTest.PublicStaticEvent += handler.handler

        EventTest.OnPublicStaticEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 10)

    def testProtectedInstanceEvent(self):
        """Test protected instance events."""
        object = EventTest()

        handler = GenericHandler()
        self.assertTrue(handler.value == None)

        object.ProtectedEvent += handler.handler

        object.OnProtectedEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 10)

        object.ProtectedEvent -= handler.handler

    def testProtectedStaticEvent(self):
        """Test protected static events."""
        object = EventTest

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
        object = EventTest()

        handler1 = GenericHandler()
        handler2 = GenericHandler()
        handler3 = GenericHandler()

        object.PublicEvent += handler1.handler
        object.PublicEvent += handler2.handler
        object.PublicEvent += handler3.handler

        object.OnPublicEvent(TestEventArgs(10))

        self.assertTrue(handler1.value == 10)
        self.assertTrue(handler2.value == 10)
        self.assertTrue(handler3.value == 10)

        object.OnPublicEvent(TestEventArgs(20))

        self.assertTrue(handler1.value == 20)
        self.assertTrue(handler2.value == 20)
        self.assertTrue(handler3.value == 20)

        object.PublicEvent -= handler1.handler
        object.PublicEvent -= handler2.handler
        object.PublicEvent -= handler3.handler

    def testInstanceMethodHandler(self):
        """Test instance method handlers."""
        object = EventTest()
        handler = GenericHandler()

        object.PublicEvent += handler.handler
        self.assertTrue(handler.value == None)

        object.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 10)

        object.PublicEvent -= handler.handler
        self.assertTrue(handler.value == 10)

        object.OnPublicEvent(TestEventArgs(20))
        self.assertTrue(handler.value == 10)

    def testVarArgsInstanceMethodHandler(self):
        """Test vararg instance method handlers."""
        object = EventTest()
        handler = VariableArgsHandler()

        object.PublicEvent += handler.handler
        self.assertTrue(handler.value == None)

        object.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 10)

        object.PublicEvent -= handler.handler
        self.assertTrue(handler.value == 10)

        object.OnPublicEvent(TestEventArgs(20))
        self.assertTrue(handler.value == 10)

    def testCallableObjectHandler(self):
        """Test callable object handlers."""
        object = EventTest()
        handler = CallableHandler()

        object.PublicEvent += handler
        self.assertTrue(handler.value == None)

        object.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 10)

        object.PublicEvent -= handler
        self.assertTrue(handler.value == 10)

        object.OnPublicEvent(TestEventArgs(20))
        self.assertTrue(handler.value == 10)

    def testVarArgsCallableHandler(self):
        """Test varargs callable handlers."""
        object = EventTest()
        handler = VarCallableHandler()

        object.PublicEvent += handler
        self.assertTrue(handler.value == None)

        object.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 10)

        object.PublicEvent -= handler
        self.assertTrue(handler.value == 10)

        object.OnPublicEvent(TestEventArgs(20))
        self.assertTrue(handler.value == 10)

    def testStaticMethodHandler(self):
        """Test static method handlers."""
        object = EventTest()
        handler = StaticMethodHandler()
        StaticMethodHandler.value = None

        object.PublicEvent += handler.handler
        self.assertTrue(handler.value == None)

        object.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 10)

        object.PublicEvent -= handler.handler
        self.assertTrue(handler.value == 10)

        object.OnPublicEvent(TestEventArgs(20))
        self.assertTrue(handler.value == 10)

    def testClassMethodHandler(self):
        """Test class method handlers."""
        object = EventTest()
        handler = ClassMethodHandler()
        ClassMethodHandler.value = None

        object.PublicEvent += handler.handler
        self.assertTrue(handler.value == None)

        object.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 10)

        object.PublicEvent -= handler.handler
        self.assertTrue(handler.value == 10)

        object.OnPublicEvent(TestEventArgs(20))
        self.assertTrue(handler.value == 10)

    def testManagedInstanceMethodHandler(self):
        """Test managed instance method handlers."""
        object = EventTest()

        object.PublicEvent += object.GenericHandler
        self.assertTrue(object.value == 0)

        object.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(object.value == 10)

        object.PublicEvent -= object.GenericHandler
        self.assertTrue(object.value == 10)

        object.OnPublicEvent(TestEventArgs(20))
        self.assertTrue(object.value == 10)

    def testManagedStaticMethodHandler(self):
        """Test managed static method handlers."""
        object = EventTest()
        EventTest.s_value = 0

        object.PublicEvent += object.StaticHandler
        self.assertTrue(EventTest.s_value == 0)

        object.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(EventTest.s_value == 10)

        object.PublicEvent -= object.StaticHandler
        self.assertTrue(EventTest.s_value == 10)

        object.OnPublicEvent(TestEventArgs(20))
        self.assertTrue(EventTest.s_value == 10)

    def testUnboundMethodHandler(self):
        """Test failure mode for unbound method handlers."""
        object = EventTest()
        object.PublicEvent += GenericHandler.handler
        try:
            object.OnPublicEvent(TestEventArgs(10))
        except TypeError:
            object.PublicEvent -= GenericHandler.handler
            return

        raise TypeError("should have raised a TypeError")

    def testFunctionHandler(self):
        """Test function handlers."""
        object = EventTest()
        dict = {'value': None}

        def handler(sender, args, dict=dict):
            dict['value'] = args.value

        object.PublicEvent += handler
        self.assertTrue(dict['value'] == None)

        object.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(dict['value'] == 10)

        object.PublicEvent -= handler
        self.assertTrue(dict['value'] == 10)

        object.OnPublicEvent(TestEventArgs(20))
        self.assertTrue(dict['value'] == 10)

    def testAddNonCallableHandler(self):
        """Test handling of attempts to add non-callable handlers."""

        def test():
            object = EventTest()
            object.PublicEvent += 10

        self.assertRaises(TypeError, test)

        def test():
            object = EventTest()
            object.PublicEvent += "spam"

        self.assertRaises(TypeError, test)

        def test():
            class spam:
                pass

            object = EventTest()
            object.PublicEvent += spam()

        self.assertRaises(TypeError, test)

    def testRemoveMultipleHandlers(self):
        """Test removing multiple instances of the same handler."""
        object = EventTest()
        handler = MultipleHandler()

        h1 = handler.handler
        object.PublicEvent += h1

        h2 = handler.handler
        object.PublicEvent += h2

        object.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 20)

        object.PublicEvent -= h1

        object.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 30)

        object.PublicEvent -= h2

        object.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 30)

        # try again, removing in a different order.

        object = EventTest()
        handler = MultipleHandler()

        h1 = handler.handler
        object.PublicEvent += h1

        h2 = handler.handler
        object.PublicEvent += h2

        object.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 20)

        object.PublicEvent -= h2

        object.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 30)

        object.PublicEvent -= h1

        object.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 30)

    def testRemoveMultipleStaticHandlers(self):
        """Test removing multiple instances of a static handler."""
        object = EventTest()
        handler = MultipleHandler()

        h1 = handler.handler
        object.PublicStaticEvent += h1

        h2 = handler.handler
        object.PublicStaticEvent += h2

        object.OnPublicStaticEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 20)

        object.PublicStaticEvent -= h1

        object.OnPublicStaticEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 30)

        object.PublicStaticEvent -= h2

        object.OnPublicStaticEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 30)

        # try again, removing in a different order.

        object = EventTest()
        handler = MultipleHandler()

        h1 = handler.handler
        object.PublicStaticEvent += h1

        h2 = handler.handler
        object.PublicStaticEvent += h2

        object.OnPublicStaticEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 20)

        object.PublicStaticEvent -= h2

        object.OnPublicStaticEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 30)

        object.PublicStaticEvent -= h1

        object.OnPublicStaticEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 30)

    def testRandomMultipleHandlers(self):
        """Test random subscribe / unsubscribe of the same handlers."""
        import random
        object = EventTest()
        handler = MultipleHandler()
        handler2 = MultipleHandler()

        object.PublicEvent += handler2.handler
        object.PublicEvent += handler2.handler

        handlers = []
        for i in range(30):
            method = handler.handler
            object.PublicEvent += method
            handlers.append(method)

        object.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 300)
        self.assertTrue(handler2.value == 20)
        handler.value = 0
        handler2.value = 0

        for i in range(30):
            item = random.choice(handlers)
            handlers.remove(item)
            object.PublicEvent -= item
            handler.value = 0
            object.OnPublicEvent(TestEventArgs(10))
            self.assertTrue(handler.value == (len(handlers) * 10))
            self.assertTrue(handler2.value == ((i + 1) * 20))

        handler2.value = 0
        object.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler2.value == 20)

        object.PublicEvent -= handler2.handler

        handler2.value = 0
        object.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler2.value == 10)

        object.PublicEvent -= handler2.handler

        handler2.value = 0
        object.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler2.value == 0)

    def testRemoveInternalCallHandler(self):
        """Test remove on an event sink implemented w/internalcall."""
        object = EventTest()

        def h(sender, args):
            pass

        object.PublicEvent += h
        object.PublicEvent -= h

    def testRemoveUnknownHandler(self):
        """Test removing an event handler that was never added."""

        def test():
            object = EventTest()
            handler = GenericHandler()

            object.PublicEvent -= handler.handler

        self.assertRaises(ValueError, test)

    def testHandlerCallbackFailure(self):
        """Test failure mode for inappropriate handlers."""

        class BadHandler:
            def handler(self, one):
                return 'too many'

        object = EventTest()
        handler = BadHandler()

        def test():
            object.PublicEvent += handler.handler
            object.OnPublicEvent(TestEventArgs(10))

        self.assertRaises(TypeError, test)

        object.PublicEvent -= handler.handler

        class BadHandler:
            def handler(self, one, two, three, four, five):
                return 'not enough'

        object = EventTest()
        handler = BadHandler()

        def test():
            object.PublicEvent += handler.handler
            object.OnPublicEvent(TestEventArgs(10))

        self.assertRaises(TypeError, test)

        object.PublicEvent -= handler.handler

    def testIncorrectInvokation(self):
        """Test incorrect invokation of events."""
        object = EventTest()

        handler = GenericHandler()
        object.PublicEvent += handler.handler

        def test():
            object.OnPublicEvent()

        self.assertRaises(TypeError, test)

        def test():
            object.OnPublicEvent(32)

        self.assertRaises(TypeError, test)

        object.PublicEvent -= handler.handler

    def testExplicitCLSEventRegistration(self):
        """Test explicit CLS event registration."""
        object = EventTest()
        handler = GenericHandler()

        delegate = TestEventHandler(handler.handler)
        object.add_PublicEvent(delegate)
        self.assertTrue(handler.value == None)

        object.OnPublicEvent(TestEventArgs(10))
        self.assertTrue(handler.value == 10)

        object.remove_PublicEvent(delegate)
        self.assertTrue(handler.value == 10)

        object.OnPublicEvent(TestEventArgs(20))
        self.assertTrue(handler.value == 10)

    def testImplicitCLSEventRegistration(self):
        """Test implicit CLS event registration."""

        def test():
            object = EventTest()
            handler = GenericHandler()
            object.add_PublicEvent(handler.handler)

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
            object = EventTest()
            object.PublicEvent = 0

        self.assertRaises(TypeError, test)

        def test():
            EventTest.PublicStaticEvent = 0

        self.assertRaises(TypeError, test)


class GenericHandler:
    """A generic handler to test event callbacks."""

    def __init__(self):
        self.value = None

    def handler(self, sender, args):
        self.value = args.value


class VariableArgsHandler:
    """A variable args handler to test event callbacks."""

    def __init__(self):
        self.value = None

    def handler(self, *args):
        ob, eventargs = args
        self.value = eventargs.value


class CallableHandler:
    """A callable handler to test event callbacks."""

    def __init__(self):
        self.value = None

    def __call__(self, sender, args):
        self.value = args.value


class VarCallableHandler:
    """A variable args callable handler to test event callbacks."""

    def __init__(self):
        self.value = None

    def __call__(self, *args):
        ob, eventargs = args
        self.value = eventargs.value


class StaticMethodHandler(object):
    """A static method handler to test event callbacks."""

    value = None

    def handler(sender, args):
        StaticMethodHandler.value = args.value

    handler = staticmethod(handler)


class ClassMethodHandler(object):
    """A class method handler to test event callbacks."""

    value = None

    def handler(cls, sender, args):
        cls.value = args.value

    handler = classmethod(handler)


class MultipleHandler:
    """A generic handler to test multiple callbacks."""

    def __init__(self):
        self.value = 0

    def handler(self, sender, args):
        self.value += args.value


def test_suite():
    return unittest.makeSuite(EventTests)


def main():
    unittest.TextTestRunner().run(test_suite())


if __name__ == '__main__':
    main()
