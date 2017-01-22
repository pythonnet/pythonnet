# -*- coding: utf-8 -*-

import unittest

from Python.Test import EventTest, EventArgsTest

from _compat import range
from utils import (CallableHandler, ClassMethodHandler, GenericHandler,
                   MultipleHandler, StaticMethodHandler, VarCallableHandler,
                   VariableArgsHandler)


class EventTests(unittest.TestCase):
    """Test CLR event support."""

    def test_public_instance_event(self):
        """Test public instance events."""
        ob = EventTest()

        handler = GenericHandler()
        self.assertTrue(handler.value is None)

        ob.PublicEvent += handler.handler

        ob.OnPublicEvent(EventArgsTest(10))
        self.assertTrue(handler.value == 10)

        ob.PublicEvent -= handler.handler

    def test_public_static_event(self):
        """Test public static events."""
        handler = GenericHandler()
        self.assertTrue(handler.value is None)

        EventTest.PublicStaticEvent += handler.handler

        EventTest.OnPublicStaticEvent(EventArgsTest(10))
        self.assertTrue(handler.value == 10)

    def test_protected_instance_event(self):
        """Test protected instance events."""
        ob = EventTest()

        handler = GenericHandler()
        self.assertTrue(handler.value is None)

        ob.ProtectedEvent += handler.handler

        ob.OnProtectedEvent(EventArgsTest(10))
        self.assertTrue(handler.value == 10)

        ob.ProtectedEvent -= handler.handler

    def test_protected_static_event(self):
        """Test protected static events."""
        handler = GenericHandler()
        self.assertTrue(handler.value is None)

        EventTest.ProtectedStaticEvent += handler.handler

        EventTest.OnProtectedStaticEvent(EventArgsTest(10))
        self.assertTrue(handler.value == 10)

        EventTest.ProtectedStaticEvent -= handler.handler

    def test_internal_events(self):
        """Test internal events."""

        with self.assertRaises(AttributeError):
            _ = EventTest().InternalEvent

        with self.assertRaises(AttributeError):
            _ = EventTest().InternalStaticEvent

        with self.assertRaises(AttributeError):
            _ = EventTest.InternalStaticEvent

    def test_private_events(self):
        """Test private events."""

        with self.assertRaises(AttributeError):
            _ = EventTest().PrivateEvent

        with self.assertRaises(AttributeError):
            _ = EventTest().PrivateStaticEvent

        with self.assertRaises(AttributeError):
            _ = EventTest.PrivateStaticEvent

    def test_multicast_event(self):
        """Test multicast events."""
        ob = EventTest()

        handler1 = GenericHandler()
        handler2 = GenericHandler()
        handler3 = GenericHandler()

        ob.PublicEvent += handler1.handler
        ob.PublicEvent += handler2.handler
        ob.PublicEvent += handler3.handler

        ob.OnPublicEvent(EventArgsTest(10))

        self.assertTrue(handler1.value == 10)
        self.assertTrue(handler2.value == 10)
        self.assertTrue(handler3.value == 10)

        ob.OnPublicEvent(EventArgsTest(20))

        self.assertTrue(handler1.value == 20)
        self.assertTrue(handler2.value == 20)
        self.assertTrue(handler3.value == 20)

        ob.PublicEvent -= handler1.handler
        ob.PublicEvent -= handler2.handler
        ob.PublicEvent -= handler3.handler

    def test_instance_method_handler(self):
        """Test instance method handlers."""
        ob = EventTest()
        handler = GenericHandler()

        ob.PublicEvent += handler.handler
        self.assertTrue(handler.value is None)

        ob.OnPublicEvent(EventArgsTest(10))
        self.assertTrue(handler.value == 10)

        ob.PublicEvent -= handler.handler
        self.assertTrue(handler.value == 10)

        ob.OnPublicEvent(EventArgsTest(20))
        self.assertTrue(handler.value == 10)

    def test_var_args_instance_method_handler(self):
        """Test vararg instance method handlers."""
        ob = EventTest()
        handler = VariableArgsHandler()

        ob.PublicEvent += handler.handler
        self.assertTrue(handler.value is None)

        ob.OnPublicEvent(EventArgsTest(10))
        self.assertTrue(handler.value == 10)

        ob.PublicEvent -= handler.handler
        self.assertTrue(handler.value == 10)

        ob.OnPublicEvent(EventArgsTest(20))
        self.assertTrue(handler.value == 10)

    def test_callableob_handler(self):
        """Test callable ob handlers."""
        ob = EventTest()
        handler = CallableHandler()

        ob.PublicEvent += handler
        self.assertTrue(handler.value is None)

        ob.OnPublicEvent(EventArgsTest(10))
        self.assertTrue(handler.value == 10)

        ob.PublicEvent -= handler
        self.assertTrue(handler.value == 10)

        ob.OnPublicEvent(EventArgsTest(20))
        self.assertTrue(handler.value == 10)

    def test_var_args_callable_handler(self):
        """Test varargs callable handlers."""
        ob = EventTest()
        handler = VarCallableHandler()

        ob.PublicEvent += handler
        self.assertTrue(handler.value is None)

        ob.OnPublicEvent(EventArgsTest(10))
        self.assertTrue(handler.value == 10)

        ob.PublicEvent -= handler
        self.assertTrue(handler.value == 10)

        ob.OnPublicEvent(EventArgsTest(20))
        self.assertTrue(handler.value == 10)

    def test_static_method_handler(self):
        """Test static method handlers."""
        ob = EventTest()
        handler = StaticMethodHandler()
        StaticMethodHandler.value = None

        ob.PublicEvent += handler.handler
        self.assertTrue(handler.value is None)

        ob.OnPublicEvent(EventArgsTest(10))
        self.assertTrue(handler.value == 10)

        ob.PublicEvent -= handler.handler
        self.assertTrue(handler.value == 10)

        ob.OnPublicEvent(EventArgsTest(20))
        self.assertTrue(handler.value == 10)

    def test_class_method_handler(self):
        """Test class method handlers."""
        ob = EventTest()
        handler = ClassMethodHandler()
        ClassMethodHandler.value = None

        ob.PublicEvent += handler.handler
        self.assertTrue(handler.value is None)

        ob.OnPublicEvent(EventArgsTest(10))
        self.assertTrue(handler.value == 10)

        ob.PublicEvent -= handler.handler
        self.assertTrue(handler.value == 10)

        ob.OnPublicEvent(EventArgsTest(20))
        self.assertTrue(handler.value == 10)

    def test_managed_instance_method_handler(self):
        """Test managed instance method handlers."""
        ob = EventTest()

        ob.PublicEvent += ob.GenericHandler
        self.assertTrue(ob.value == 0)

        ob.OnPublicEvent(EventArgsTest(10))
        self.assertTrue(ob.value == 10)

        ob.PublicEvent -= ob.GenericHandler
        self.assertTrue(ob.value == 10)

        ob.OnPublicEvent(EventArgsTest(20))
        self.assertTrue(ob.value == 10)

    def test_managed_static_method_handler(self):
        """Test managed static method handlers."""
        ob = EventTest()
        EventTest.s_value = 0

        ob.PublicEvent += ob.StaticHandler
        self.assertTrue(EventTest.s_value == 0)

        ob.OnPublicEvent(EventArgsTest(10))
        self.assertTrue(EventTest.s_value == 10)

        ob.PublicEvent -= ob.StaticHandler
        self.assertTrue(EventTest.s_value == 10)

        ob.OnPublicEvent(EventArgsTest(20))
        self.assertTrue(EventTest.s_value == 10)

    def test_unbound_method_handler(self):
        """Test failure mode for unbound method handlers."""
        ob = EventTest()
        ob.PublicEvent += GenericHandler.handler

        with self.assertRaises(TypeError):
            ob.OnPublicEvent(EventArgsTest(10))

        ob.PublicEvent -= GenericHandler.handler

    def test_function_handler(self):
        """Test function handlers."""
        ob = EventTest()
        dict_ = {'value': None}

        def handler(sender, args, dict_=dict_):
            dict_['value'] = args.value

        ob.PublicEvent += handler
        self.assertTrue(dict_['value'] is None)

        ob.OnPublicEvent(EventArgsTest(10))
        self.assertTrue(dict_['value'] == 10)

        ob.PublicEvent -= handler
        self.assertTrue(dict_['value'] == 10)

        ob.OnPublicEvent(EventArgsTest(20))
        self.assertTrue(dict_['value'] == 10)

    def test_add_non_callable_handler(self):
        """Test handling of attempts to add non-callable handlers."""

        with self.assertRaises(TypeError):
            ob = EventTest()
            ob.PublicEvent += 10

        with self.assertRaises(TypeError):
            ob = EventTest()
            ob.PublicEvent += "spam"

        with self.assertRaises(TypeError):
            class Spam(object):
                pass

            ob = EventTest()
            ob.PublicEvent += Spam()

    def test_remove_multiple_handlers(self):
        """Test removing multiple instances of the same handler."""
        ob = EventTest()
        handler = MultipleHandler()

        h1 = handler.handler
        ob.PublicEvent += h1

        h2 = handler.handler
        ob.PublicEvent += h2

        ob.OnPublicEvent(EventArgsTest(10))
        self.assertTrue(handler.value == 20)

        ob.PublicEvent -= h1

        ob.OnPublicEvent(EventArgsTest(10))
        self.assertTrue(handler.value == 30)

        ob.PublicEvent -= h2

        ob.OnPublicEvent(EventArgsTest(10))
        self.assertTrue(handler.value == 30)

        # try again, removing in a different order.

        ob = EventTest()
        handler = MultipleHandler()

        h1 = handler.handler
        ob.PublicEvent += h1

        h2 = handler.handler
        ob.PublicEvent += h2

        ob.OnPublicEvent(EventArgsTest(10))
        self.assertTrue(handler.value == 20)

        ob.PublicEvent -= h2

        ob.OnPublicEvent(EventArgsTest(10))
        self.assertTrue(handler.value == 30)

        ob.PublicEvent -= h1

        ob.OnPublicEvent(EventArgsTest(10))
        self.assertTrue(handler.value == 30)

    def test_remove_multiple_static_handlers(self):
        """Test removing multiple instances of a static handler."""
        ob = EventTest()
        handler = MultipleHandler()

        h1 = handler.handler
        ob.PublicStaticEvent += h1

        h2 = handler.handler
        ob.PublicStaticEvent += h2

        ob.OnPublicStaticEvent(EventArgsTest(10))
        self.assertTrue(handler.value == 20)

        ob.PublicStaticEvent -= h1

        ob.OnPublicStaticEvent(EventArgsTest(10))
        self.assertTrue(handler.value == 30)

        ob.PublicStaticEvent -= h2

        ob.OnPublicStaticEvent(EventArgsTest(10))
        self.assertTrue(handler.value == 30)

        # try again, removing in a different order.

        ob = EventTest()
        handler = MultipleHandler()

        h1 = handler.handler
        ob.PublicStaticEvent += h1

        h2 = handler.handler
        ob.PublicStaticEvent += h2

        ob.OnPublicStaticEvent(EventArgsTest(10))
        self.assertTrue(handler.value == 20)

        ob.PublicStaticEvent -= h2

        ob.OnPublicStaticEvent(EventArgsTest(10))
        self.assertTrue(handler.value == 30)

        ob.PublicStaticEvent -= h1

        ob.OnPublicStaticEvent(EventArgsTest(10))
        self.assertTrue(handler.value == 30)

    def test_random_multiple_handlers(self):
        """Test random subscribe / unsubscribe of the same handlers."""
        import random
        ob = EventTest()
        handler = MultipleHandler()
        handler2 = MultipleHandler()

        ob.PublicEvent += handler2.handler
        ob.PublicEvent += handler2.handler

        handlers = []
        for _ in range(30):
            method = handler.handler
            ob.PublicEvent += method
            handlers.append(method)

        ob.OnPublicEvent(EventArgsTest(10))
        self.assertTrue(handler.value == 300)
        self.assertTrue(handler2.value == 20)
        handler.value = 0
        handler2.value = 0

        for i in range(30):
            item = random.choice(handlers)
            handlers.remove(item)
            ob.PublicEvent -= item
            handler.value = 0
            ob.OnPublicEvent(EventArgsTest(10))
            self.assertTrue(handler.value == (len(handlers) * 10))
            self.assertTrue(handler2.value == ((i + 1) * 20))

        handler2.value = 0
        ob.OnPublicEvent(EventArgsTest(10))
        self.assertTrue(handler2.value == 20)

        ob.PublicEvent -= handler2.handler

        handler2.value = 0
        ob.OnPublicEvent(EventArgsTest(10))
        self.assertTrue(handler2.value == 10)

        ob.PublicEvent -= handler2.handler

        handler2.value = 0
        ob.OnPublicEvent(EventArgsTest(10))
        self.assertTrue(handler2.value == 0)

    def test_remove_internal_call_handler(self):
        """Test remove on an event sink implemented w/internalcall."""
        ob = EventTest()

        def h(sender, args):
            pass

        ob.PublicEvent += h
        ob.PublicEvent -= h

    def test_remove_unknown_handler(self):
        """Test removing an event handler that was never added."""

        with self.assertRaises(ValueError):
            ob = EventTest()
            handler = GenericHandler()

            ob.PublicEvent -= handler.handler

    def test_handler_callback_failure(self):
        """Test failure mode for inappropriate handlers."""

        class BadHandler(object):
            def handler(self, one):
                return 'too many'

        ob = EventTest()
        handler = BadHandler()

        with self.assertRaises(TypeError):
            ob.PublicEvent += handler.handler
            ob.OnPublicEvent(EventArgsTest(10))

        ob.PublicEvent -= handler.handler

        class BadHandler(object):
            def handler(self, one, two, three, four, five):
                return 'not enough'

        ob = EventTest()
        handler = BadHandler()

        with self.assertRaises(TypeError):
            ob.PublicEvent += handler.handler
            ob.OnPublicEvent(EventArgsTest(10))

        ob.PublicEvent -= handler.handler

    def test_incorrect_invokation(self):
        """Test incorrect invocation of events."""
        ob = EventTest()

        handler = GenericHandler()
        ob.PublicEvent += handler.handler

        with self.assertRaises(TypeError):
            ob.OnPublicEvent()

        with self.assertRaises(TypeError):
            ob.OnPublicEvent(32)

        ob.PublicEvent -= handler.handler

    def test_explicit_cls_event_registration(self):
        """Test explicit CLS event registration."""
        from Python.Test import EventHandlerTest

        ob = EventTest()
        handler = GenericHandler()

        delegate = EventHandlerTest(handler.handler)
        ob.add_PublicEvent(delegate)
        self.assertTrue(handler.value is None)

        ob.OnPublicEvent(EventArgsTest(10))
        self.assertTrue(handler.value == 10)

        ob.remove_PublicEvent(delegate)
        self.assertTrue(handler.value == 10)

        ob.OnPublicEvent(EventArgsTest(20))
        self.assertTrue(handler.value == 10)

    def test_implicit_cls_event_registration(self):
        """Test implicit CLS event registration."""

        with self.assertRaises(TypeError):
            ob = EventTest()
            handler = GenericHandler()
            ob.add_PublicEvent(handler.handler)

    def test_event_descriptor_abuse(self):
        """Test event descriptor abuse."""

        with self.assertRaises(TypeError):
            del EventTest.PublicEvent

        with self.assertRaises(TypeError):
            del EventTest.__dict__['PublicEvent']

        desc = EventTest.__dict__['PublicEvent']

        with self.assertRaises(TypeError):
            desc.__get__(0, 0)

        with self.assertRaises(TypeError):
            desc.__set__(0, 0)

        with self.assertRaises(TypeError):
            ob = EventTest()
            ob.PublicEvent = 0

        with self.assertRaises(TypeError):
            EventTest.PublicStaticEvent = 0


def test_suite():
    return unittest.makeSuite(EventTests)
