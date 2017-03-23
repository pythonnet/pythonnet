# -*- coding: utf-8 -*-

"""Test CLR event support."""

import pytest
from Python.Test import EventTest, EventArgsTest

from ._compat import range
from .utils import (CallableHandler, ClassMethodHandler, GenericHandler,
                    MultipleHandler, StaticMethodHandler, VarCallableHandler,
                    VariableArgsHandler)


def test_public_instance_event():
    """Test public instance events."""
    ob = EventTest()

    handler = GenericHandler()
    assert handler.value is None

    ob.PublicEvent += handler.handler

    ob.OnPublicEvent(EventArgsTest(10))
    assert handler.value == 10

    ob.PublicEvent -= handler.handler


def test_public_static_event():
    """Test public static events."""
    handler = GenericHandler()
    assert handler.value is None

    EventTest.PublicStaticEvent += handler.handler

    EventTest.OnPublicStaticEvent(EventArgsTest(10))
    assert handler.value == 10


def test_protected_instance_event():
    """Test protected instance events."""
    ob = EventTest()

    handler = GenericHandler()
    assert handler.value is None

    ob.ProtectedEvent += handler.handler

    ob.OnProtectedEvent(EventArgsTest(10))
    assert handler.value == 10

    ob.ProtectedEvent -= handler.handler


def test_protected_static_event():
    """Test protected static events."""
    handler = GenericHandler()
    assert handler.value is None

    EventTest.ProtectedStaticEvent += handler.handler

    EventTest.OnProtectedStaticEvent(EventArgsTest(10))
    assert handler.value == 10

    EventTest.ProtectedStaticEvent -= handler.handler


def test_internal_events():
    """Test internal events."""

    with pytest.raises(AttributeError):
        _ = EventTest().InternalEvent

    with pytest.raises(AttributeError):
        _ = EventTest().InternalStaticEvent

    with pytest.raises(AttributeError):
        _ = EventTest.InternalStaticEvent


def test_private_events():
    """Test private events."""

    with pytest.raises(AttributeError):
        _ = EventTest().PrivateEvent

    with pytest.raises(AttributeError):
        _ = EventTest().PrivateStaticEvent

    with pytest.raises(AttributeError):
        _ = EventTest.PrivateStaticEvent


def test_multicast_event():
    """Test multicast events."""
    ob = EventTest()

    handler1 = GenericHandler()
    handler2 = GenericHandler()
    handler3 = GenericHandler()

    ob.PublicEvent += handler1.handler
    ob.PublicEvent += handler2.handler
    ob.PublicEvent += handler3.handler

    ob.OnPublicEvent(EventArgsTest(10))

    assert handler1.value == 10
    assert handler2.value == 10
    assert handler3.value == 10

    ob.OnPublicEvent(EventArgsTest(20))

    assert handler1.value == 20
    assert handler2.value == 20
    assert handler3.value == 20

    ob.PublicEvent -= handler1.handler
    ob.PublicEvent -= handler2.handler
    ob.PublicEvent -= handler3.handler


def test_instance_method_handler():
    """Test instance method handlers."""
    ob = EventTest()
    handler = GenericHandler()

    ob.PublicEvent += handler.handler
    assert handler.value is None

    ob.OnPublicEvent(EventArgsTest(10))
    assert handler.value == 10

    ob.PublicEvent -= handler.handler
    assert handler.value == 10

    ob.OnPublicEvent(EventArgsTest(20))
    assert handler.value == 10


def test_var_args_instance_method_handler():
    """Test vararg instance method handlers."""
    ob = EventTest()
    handler = VariableArgsHandler()

    ob.PublicEvent += handler.handler
    assert handler.value is None

    ob.OnPublicEvent(EventArgsTest(10))
    assert handler.value == 10

    ob.PublicEvent -= handler.handler
    assert handler.value == 10

    ob.OnPublicEvent(EventArgsTest(20))
    assert handler.value == 10


def test_callableob_handler():
    """Test callable ob handlers."""
    ob = EventTest()
    handler = CallableHandler()

    ob.PublicEvent += handler
    assert handler.value is None

    ob.OnPublicEvent(EventArgsTest(10))
    assert handler.value == 10

    ob.PublicEvent -= handler
    assert handler.value == 10

    ob.OnPublicEvent(EventArgsTest(20))
    assert handler.value == 10


def test_var_args_callable_handler():
    """Test varargs callable handlers."""
    ob = EventTest()
    handler = VarCallableHandler()

    ob.PublicEvent += handler
    assert handler.value is None

    ob.OnPublicEvent(EventArgsTest(10))
    assert handler.value == 10

    ob.PublicEvent -= handler
    assert handler.value == 10

    ob.OnPublicEvent(EventArgsTest(20))
    assert handler.value == 10


def test_static_method_handler():
    """Test static method handlers."""
    ob = EventTest()
    handler = StaticMethodHandler()
    StaticMethodHandler.value = None

    ob.PublicEvent += handler.handler
    assert handler.value is None

    ob.OnPublicEvent(EventArgsTest(10))
    assert handler.value == 10

    ob.PublicEvent -= handler.handler
    assert handler.value == 10

    ob.OnPublicEvent(EventArgsTest(20))
    assert handler.value == 10


def test_class_method_handler():
    """Test class method handlers."""
    ob = EventTest()
    handler = ClassMethodHandler()
    ClassMethodHandler.value = None

    ob.PublicEvent += handler.handler
    assert handler.value is None

    ob.OnPublicEvent(EventArgsTest(10))
    assert handler.value == 10

    ob.PublicEvent -= handler.handler
    assert handler.value == 10

    ob.OnPublicEvent(EventArgsTest(20))
    assert handler.value == 10


def test_managed_instance_method_handler():
    """Test managed instance method handlers."""
    ob = EventTest()

    ob.PublicEvent += ob.GenericHandler
    assert ob.value == 0

    ob.OnPublicEvent(EventArgsTest(10))
    assert ob.value == 10

    ob.PublicEvent -= ob.GenericHandler
    assert ob.value == 10

    ob.OnPublicEvent(EventArgsTest(20))
    assert ob.value == 10


def test_managed_static_method_handler():
    """Test managed static method handlers."""
    ob = EventTest()
    EventTest.s_value = 0

    ob.PublicEvent += ob.StaticHandler
    assert EventTest.s_value == 0

    ob.OnPublicEvent(EventArgsTest(10))
    assert EventTest.s_value == 10

    ob.PublicEvent -= ob.StaticHandler
    assert EventTest.s_value == 10

    ob.OnPublicEvent(EventArgsTest(20))
    assert EventTest.s_value == 10


def test_unbound_method_handler():
    """Test failure mode for unbound method handlers."""
    ob = EventTest()
    ob.PublicEvent += GenericHandler.handler

    with pytest.raises(TypeError):
        ob.OnPublicEvent(EventArgsTest(10))

    ob.PublicEvent -= GenericHandler.handler


def test_function_handler():
    """Test function handlers."""
    ob = EventTest()
    dict_ = {'value': None}

    def handler(sender, args, dict_=dict_):
        dict_['value'] = args.value

    ob.PublicEvent += handler
    assert dict_['value'] is None

    ob.OnPublicEvent(EventArgsTest(10))
    assert dict_['value'] == 10

    ob.PublicEvent -= handler
    assert dict_['value'] == 10

    ob.OnPublicEvent(EventArgsTest(20))
    assert dict_['value'] == 10


def test_add_non_callable_handler():
    """Test handling of attempts to add non-callable handlers."""

    with pytest.raises(TypeError):
        ob = EventTest()
        ob.PublicEvent += 10

    with pytest.raises(TypeError):
        ob = EventTest()
        ob.PublicEvent += "spam"

    with pytest.raises(TypeError):
        class Spam(object):
            pass

        ob = EventTest()
        ob.PublicEvent += Spam()


def test_remove_multiple_handlers():
    """Test removing multiple instances of the same handler."""
    ob = EventTest()
    handler = MultipleHandler()

    h1 = handler.handler
    ob.PublicEvent += h1

    h2 = handler.handler
    ob.PublicEvent += h2

    ob.OnPublicEvent(EventArgsTest(10))
    assert handler.value == 20

    ob.PublicEvent -= h1

    ob.OnPublicEvent(EventArgsTest(10))
    assert handler.value == 30

    ob.PublicEvent -= h2

    ob.OnPublicEvent(EventArgsTest(10))
    assert handler.value == 30

    # try again, removing in a different order.

    ob = EventTest()
    handler = MultipleHandler()

    h1 = handler.handler
    ob.PublicEvent += h1

    h2 = handler.handler
    ob.PublicEvent += h2

    ob.OnPublicEvent(EventArgsTest(10))
    assert handler.value == 20

    ob.PublicEvent -= h2

    ob.OnPublicEvent(EventArgsTest(10))
    assert handler.value == 30

    ob.PublicEvent -= h1

    ob.OnPublicEvent(EventArgsTest(10))
    assert handler.value == 30


def test_remove_multiple_static_handlers():
    """Test removing multiple instances of a static handler."""
    ob = EventTest()
    handler = MultipleHandler()

    h1 = handler.handler
    ob.PublicStaticEvent += h1

    h2 = handler.handler
    ob.PublicStaticEvent += h2

    ob.OnPublicStaticEvent(EventArgsTest(10))
    assert handler.value == 20

    ob.PublicStaticEvent -= h1

    ob.OnPublicStaticEvent(EventArgsTest(10))
    assert handler.value == 30

    ob.PublicStaticEvent -= h2

    ob.OnPublicStaticEvent(EventArgsTest(10))
    assert handler.value == 30

    # try again, removing in a different order.

    ob = EventTest()
    handler = MultipleHandler()

    h1 = handler.handler
    ob.PublicStaticEvent += h1

    h2 = handler.handler
    ob.PublicStaticEvent += h2

    ob.OnPublicStaticEvent(EventArgsTest(10))
    assert handler.value == 20

    ob.PublicStaticEvent -= h2

    ob.OnPublicStaticEvent(EventArgsTest(10))
    assert handler.value == 30

    ob.PublicStaticEvent -= h1

    ob.OnPublicStaticEvent(EventArgsTest(10))
    assert handler.value == 30


def test_random_multiple_handlers():
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
    assert handler.value == 300
    assert handler2.value == 20
    handler.value = 0
    handler2.value = 0

    for i in range(30):
        item = random.choice(handlers)
        handlers.remove(item)
        ob.PublicEvent -= item
        handler.value = 0
        ob.OnPublicEvent(EventArgsTest(10))
        assert handler.value == (len(handlers) * 10)
        assert handler2.value == ((i + 1) * 20)

    handler2.value = 0
    ob.OnPublicEvent(EventArgsTest(10))
    assert handler2.value == 20

    ob.PublicEvent -= handler2.handler

    handler2.value = 0
    ob.OnPublicEvent(EventArgsTest(10))
    assert handler2.value == 10

    ob.PublicEvent -= handler2.handler

    handler2.value = 0
    ob.OnPublicEvent(EventArgsTest(10))
    assert handler2.value == 0


def test_remove_internal_call_handler():
    """Test remove on an event sink implemented w/internalcall."""
    ob = EventTest()

    def h(sender, args):
        pass

    ob.PublicEvent += h
    ob.PublicEvent -= h


def test_remove_unknown_handler():
    """Test removing an event handler that was never added."""

    with pytest.raises(ValueError):
        ob = EventTest()
        handler = GenericHandler()

        ob.PublicEvent -= handler.handler


def test_handler_callback_failure():
    """Test failure mode for inappropriate handlers."""

    class BadHandler(object):
        def handler(self, one):
            return 'too many'

    ob = EventTest()
    handler = BadHandler()

    with pytest.raises(TypeError):
        ob.PublicEvent += handler.handler
        ob.OnPublicEvent(EventArgsTest(10))

    ob.PublicEvent -= handler.handler

    class BadHandler(object):
        def handler(self, one, two, three, four, five):
            return 'not enough'

    ob = EventTest()
    handler = BadHandler()

    with pytest.raises(TypeError):
        ob.PublicEvent += handler.handler
        ob.OnPublicEvent(EventArgsTest(10))

    ob.PublicEvent -= handler.handler


def test_incorrect_invokation():
    """Test incorrect invocation of events."""
    ob = EventTest()

    handler = GenericHandler()
    ob.PublicEvent += handler.handler

    with pytest.raises(TypeError):
        ob.OnPublicEvent()

    with pytest.raises(TypeError):
        ob.OnPublicEvent(32)

    ob.PublicEvent -= handler.handler


def test_explicit_cls_event_registration():
    """Test explicit CLS event registration."""
    from Python.Test import EventHandlerTest

    ob = EventTest()
    handler = GenericHandler()

    delegate = EventHandlerTest(handler.handler)
    ob.add_PublicEvent(delegate)
    assert handler.value is None

    ob.OnPublicEvent(EventArgsTest(10))
    assert handler.value == 10

    ob.remove_PublicEvent(delegate)
    assert handler.value == 10

    ob.OnPublicEvent(EventArgsTest(20))
    assert handler.value == 10


def test_implicit_cls_event_registration():
    """Test implicit CLS event registration."""

    with pytest.raises(TypeError):
        ob = EventTest()
        handler = GenericHandler()
        ob.add_PublicEvent(handler.handler)


def test_event_descriptor_abuse():
    """Test event descriptor abuse."""

    with pytest.raises(TypeError):
        del EventTest.PublicEvent

    with pytest.raises(TypeError):
        del EventTest.__dict__['PublicEvent']

    desc = EventTest.__dict__['PublicEvent']

    with pytest.raises(TypeError):
        desc.__get__(0, 0)

    with pytest.raises(TypeError):
        desc.__set__(0, 0)

    with pytest.raises(TypeError):
        ob = EventTest()
        ob.PublicEvent = 0

    with pytest.raises(TypeError):
        EventTest.PublicStaticEvent = 0
