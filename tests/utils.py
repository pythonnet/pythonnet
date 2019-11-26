# -*- coding: utf-8 -*-

"""Tests Utilities

Refactor utility functions and classes
"""

from __future__ import print_function

from ._compat import PY2, PY3


def dprint(msg):
    # Debugging helper to trace thread-related tests.
    if 0:
        print(msg)


def is_clr_module(ob):
    return type(ob).__name__ == 'ModuleObject'


def is_clr_root_module(ob):
    if PY3:
        # in Python 3 the clr module is a normal python module
        return ob.__name__ == "clr"
    elif PY2:
        return type(ob).__name__ == 'CLRModule'


def is_clr_class(ob):
    return type(ob).__name__ == 'CLR Metatype'  # for now


class ClassicClass:
    def kind(self):
        return "classic"


class NewStyleClass(object):
    def kind(self):
        return "new-style"


class GenericHandler(object):
    """A generic handler to test event callbacks."""

    def __init__(self):
        self.value = None

    def handler(self, sender, args):
        self.value = args.value


class VariableArgsHandler(object):
    """A variable args handler to test event callbacks."""

    def __init__(self):
        self.value = None

    def handler(self, *args):
        ob, eventargs = args
        self.value = eventargs.value


class CallableHandler(object):
    """A callable handler to test event callbacks."""

    def __init__(self):
        self.value = None

    def __call__(self, sender, args):
        self.value = args.value


class VarCallableHandler(object):
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


class MultipleHandler(object):
    """A generic handler to test multiple callbacks."""

    def __init__(self):
        self.value = 0

    def handler(self, sender, args):
        self.value += args.value

    def count(self):
        self.value += 1
        return 'ok'


class HelloClass(object):
    def hello(self):
        return "hello"

    def __call__(self):
        return "hello"

    @staticmethod
    def s_hello():
        return "hello"

    @classmethod
    def c_hello(cls):
        return "hello"


def hello_func():
    return "hello"
