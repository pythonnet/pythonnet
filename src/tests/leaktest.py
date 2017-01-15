#!/usr/bin/env python
# -*- coding: utf-8 -*-
# FIXME: TypeError: 'EventBinding' object is not callable

from __future__ import print_function

import clr
import gc
import sys

import System

from ._compat import range
from .utils import (CallableHandler, ClassMethodHandler, GenericHandler,
                    HelloClass, StaticMethodHandler, VarCallableHandler,
                    VariableArgsHandler, hello_func)


class LeakTest(object):
    """A leak-check test for the objects implemented in the managed
       runtime. For each kind of object tested, memory should reach
       a particular level after warming up and stay essentially the
       same, net of minor fluctuation induced by GC."""

    def __init__(self):
        self.count = 50000
        self.quiet = 0
        self._ws = 0

    def notify(self, msg):
        if not self.quiet:
            print(msg)

    def start_test(self):
        System.GC.Collect(System.GC.MaxGeneration)
        gc.collect()
        self._ws = System.Environment.WorkingSet

    def end_test(self):
        start = self._ws
        System.GC.Collect(System.GC.MaxGeneration)
        gc.collect()
        end = System.Environment.WorkingSet
        diff = end - start
        if diff > 0:
            diff = '+{0}'.format(diff)
        else:
            diff = '{0}'.format(diff)
        print("  start: {0}  end: {1} diff: {2}".format(start, end, diff))
        print("")

    def run(self):
        self.testModules()
        self.testClasses()
        self.testEnumerations()
        self.testEvents()
        self.testDelegates()

    def report(self):
        gc.collect()
        dicttype = type({})
        for item in gc.get_objects():
            if type(item) != dicttype:
                print(item, sys.getrefcount(item))

    def test_modules(self):
        self.notify("Running module leak check...")

        for i in range(self.count):
            if i == 10:
                self.start_test()

            __import__('clr')
            __import__('System')
            __import__('System.IO')
            __import__('System.Net')
            __import__('System.Xml')

        self.end_test()

    def test_classes(self):
        from System.Collections import Hashtable
        from Python.Test import StringDelegate

        self.notify("Running class leak check...")

        for i in range(self.count):
            if i == 10:
                self.start_test()

            # Reference type
            x = Hashtable()
            del x

            # Value type
            x = System.Int32(99)
            del x

            # Delegate type
            x = StringDelegate(hello_func)
            del x

        self.end_test()

    def test_enumerations(self):
        import Python.Test as Test

        self.notify("Running enum leak check...")

        for i in range(self.count):
            if i == 10:
                self.start_test()

            x = Test.ByteEnum.Zero
            del x

            x = Test.SByteEnum.Zero
            del x

            x = Test.ShortEnum.Zero
            del x

            x = Test.UShortEnum.Zero
            del x

            x = Test.IntEnum.Zero
            del x

            x = Test.UIntEnum.Zero
            del x

            x = Test.LongEnum.Zero
            del x

            x = Test.ULongEnum.Zero
            del x

        self.end_test()

    def test_events(self):
        from Python.Test import EventTest, EventArgsTest

        self.notify("Running event leak check...")

        for i in range(self.count):
            if i == 10:
                self.start_test()

            testob = EventTest()

            # Instance method event handler
            handler = GenericHandler()
            testob.PublicEvent += handler.handler
            testob.PublicEvent(testob, EventArgsTest(10))
            testob.PublicEvent -= handler.handler
            del handler

            # Vararg method event handler
            handler = VariableArgsHandler()
            testob.PublicEvent += handler.handler
            testob.PublicEvent(testob, EventArgsTest(10))
            testob.PublicEvent -= handler.handler
            del handler

            # Callable object event handler
            handler = CallableHandler()
            testob.PublicEvent += handler
            testob.PublicEvent(testob, EventArgsTest(10))
            testob.PublicEvent -= handler
            del handler

            # Callable vararg event handler
            handler = VarCallableHandler()
            testob.PublicEvent += handler
            testob.PublicEvent(testob, EventArgsTest(10))
            testob.PublicEvent -= handler
            del handler

            # Static method event handler
            handler = StaticMethodHandler()
            StaticMethodHandler.value = None
            testob.PublicEvent += handler.handler
            testob.PublicEvent(testob, EventArgsTest(10))
            testob.PublicEvent -= handler.handler
            del handler

            # Class method event handler
            handler = ClassMethodHandler()
            ClassMethodHandler.value = None
            testob.PublicEvent += handler.handler
            testob.PublicEvent(testob, EventArgsTest(10))
            testob.PublicEvent -= handler.handler
            del handler

            # Managed instance event handler
            testob.PublicEvent += testob.GenericHandler
            testob.PublicEvent(testob, EventArgsTest(10))
            testob.PublicEvent -= testob.GenericHandler

            # Static managed event handler
            testob.PublicEvent += EventTest.StaticHandler
            testob.PublicEvent(testob, EventArgsTest(10))
            testob.PublicEvent -= EventTest.StaticHandler

            # Function event handler
            dict_ = {'value': None}

            def handler(sender, args, dict_=dict_):
                dict_['value'] = args.value

            testob.PublicEvent += handler
            testob.PublicEvent(testob, EventArgsTest(10))
            testob.PublicEvent -= handler
            del handler

        self.end_test()

    def test_delegates(self):
        from Python.Test import DelegateTest, StringDelegate

        self.notify("Running delegate leak check...")

        for i in range(self.count):
            if i == 10:
                self.start_test()

            # Delegate from function
            testob = DelegateTest()
            d = StringDelegate(hello_func)
            testob.CallStringDelegate(d)
            testob.stringDelegate = d
            testob.stringDelegate()
            testob.stringDelegate = None
            del testob
            del d

            # Delegate from instance method
            inst = HelloClass()
            testob = DelegateTest()
            d = StringDelegate(inst.hello)
            testob.CallStringDelegate(d)
            testob.stringDelegate = d
            testob.stringDelegate()
            testob.stringDelegate = None
            del testob
            del inst
            del d

            # Delegate from static method
            testob = DelegateTest()
            d = StringDelegate(HelloClass.s_hello)
            testob.CallStringDelegate(d)
            testob.stringDelegate = d
            testob.stringDelegate()
            testob.stringDelegate = None
            del testob
            del d

            # Delegate from class method
            testob = DelegateTest()
            d = StringDelegate(HelloClass.c_hello)
            testob.CallStringDelegate(d)
            testob.stringDelegate = d
            testob.stringDelegate()
            testob.stringDelegate = None
            del testob
            del d

            # Delegate from callable object
            inst = HelloClass()
            testob = DelegateTest()
            d = StringDelegate(inst)
            testob.CallStringDelegate(d)
            testob.stringDelegate = d
            testob.stringDelegate()
            testob.stringDelegate = None
            del testob
            del inst
            del d

            # Delegate from managed instance method
            testob = DelegateTest()
            d = StringDelegate(testob.SayHello)
            testob.CallStringDelegate(d)
            testob.stringDelegate = d
            testob.stringDelegate()
            testob.stringDelegate = None
            del testob
            del d

            # Delegate from managed static method
            testob = DelegateTest()
            d = StringDelegate(DelegateTest.StaticSayHello)
            testob.CallStringDelegate(d)
            testob.stringDelegate = d
            testob.stringDelegate()
            testob.stringDelegate = None
            del testob
            del d

            # Nested delegates
            testob = DelegateTest()
            d1 = StringDelegate(hello_func)
            d2 = StringDelegate(d1)
            testob.CallStringDelegate(d2)
            testob.stringDelegate = d2
            testob.stringDelegate()
            testob.stringDelegate = None
            del testob
            del d1
            del d2

            # Multicast delegates
            testob = DelegateTest()
            d1 = StringDelegate(hello_func)
            d2 = StringDelegate(hello_func)
            md = System.Delegate.Combine(d1, d2)
            testob.CallStringDelegate(md)
            testob.stringDelegate = md
            testob.stringDelegate()
            testob.stringDelegate = None
            del testob
            del d1
            del d2
            del md

        self.end_test()


if __name__ == '__main__':
    test = LeakTest()
    test.run()
    test.report()
