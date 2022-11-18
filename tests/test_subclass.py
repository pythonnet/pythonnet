# -*- coding: utf-8 -*-
# FIXME: This test module randomly passes/fails even if all tests are skipped.
# Something fishy is going on with the Test fixtures. Behavior seen on CI on
# both Linux and Windows
# TODO: Remove delay of class creations. Adding SetUp/TearDown may help

"""Test sub-classing managed types"""

import System
from System import (Console, Attribute, Double)
from System.Diagnostics import (DebuggerDisplay, DebuggerDisplayAttribute, Debug)
from System.ComponentModel import (Browsable, BrowsableAttribute)
from System.Threading import (CancellationToken)
import pytest
from Python.Test import (IInterfaceTest, SubClassTest, EventArgsTest,
                         FunctionsTest, GenericVirtualMethodTest, ISimpleInterface, SimpleClass, TestAttribute, TestAttributeAttribute, ISimpleInterface2, IGenericInterface)
import Python.Test
from System.Collections.Generic import List


def interface_test_class_fixture(subnamespace):
    """Delay creation of class until test starts."""

    class InterfaceTestClass(IInterfaceTest):
        """class that implements the test interface"""
        __namespace__ = "Python.Test." + subnamespace

        def foo(self):
            return "InterfaceTestClass"

        def bar(self, x, i):
            return "/".join([x] * i)

    return InterfaceTestClass


def interface_generic_class_fixture(subnamespace):

    class GenericInterfaceImpl(IGenericInterface[int]):
        __namespace__ = "Python.Test." + subnamespace

        def Get(self, x):
            return x

    return GenericInterfaceImpl


def derived_class_fixture(subnamespace):
    """Delay creation of class until test starts."""

    class DerivedClass(SubClassTest):
        """class that derives from a class deriving from IInterfaceTest"""
        __namespace__ = "Python.Test." + subnamespace

        def foo(self):
            return "DerivedClass"

        def base_foo(self):
            return SubClassTest.foo(self)

        def super_foo(self):
            return super(DerivedClass, self).foo()

        def bar(self, x, i):
            return "_".join([x] * i)

        def return_list(self):
            l = List[str]()
            l.Add("A")
            l.Add("B")
            l.Add("C")
            return l

    return DerivedClass

def broken_derived_class_fixture(subnamespace):
    """Delay creation of class until test starts."""

    class DerivedClass(SubClassTest):
        """class that derives from a class deriving from IInterfaceTest"""
        __namespace__ = 3

    return DerivedClass

def derived_event_test_class_fixture(subnamespace):
    """Delay creation of class until test starts."""

    class DerivedEventTest(IInterfaceTest):
        """class that implements IInterfaceTest.TestEvent"""
        __namespace__ = "Python.Test." + subnamespace

        def __init__(self):
            self.event_handlers = []

        # event handling
        def add_TestEvent(self, handler):
            self.event_handlers.append(handler)

        def remove_TestEvent(self, handler):
            self.event_handlers.remove(handler)

        def OnTestEvent(self, value):
            args = EventArgsTest(value)
            for handler in self.event_handlers:
                handler(self, args)

    return DerivedEventTest


def test_base_class():
    """Test base class managed type"""
    ob = SubClassTest()
    assert ob.foo() == "foo"
    assert FunctionsTest.test_foo(ob) == "foo"
    assert ob.bar("bar", 2) == "bar"
    assert FunctionsTest.test_bar(ob, "bar", 2) == "bar"
    assert ob.not_overriden() == "not_overriden"
    assert list(ob.return_list()) == ["a", "b", "c"]
    assert list(SubClassTest.test_list(ob)) == ["a", "b", "c"]


def test_interface():
    """Test python classes can derive from C# interfaces"""
    InterfaceTestClass = interface_test_class_fixture(test_interface.__name__)
    ob = InterfaceTestClass()
    assert ob.foo() == "InterfaceTestClass"
    assert FunctionsTest.test_foo(ob) == "InterfaceTestClass"
    assert ob.bar("bar", 2) == "bar/bar"
    assert FunctionsTest.test_bar(ob, "bar", 2) == "bar/bar"

    # pass_through will convert from InterfaceTestClass -> IInterfaceTest,
    # causing a new wrapper object to be created. Hence id will differ.
    x = FunctionsTest.pass_through_interface(ob)
    assert id(x) != id(ob)


def test_derived_class():
    """Test python class derived from managed type"""
    DerivedClass = derived_class_fixture(test_derived_class.__name__)
    ob = DerivedClass()
    assert ob.foo() == "DerivedClass"
    assert ob.base_foo() == "foo"
    assert ob.super_foo() == "foo"
    assert FunctionsTest.test_foo(ob) == "DerivedClass"
    assert ob.bar("bar", 2) == "bar_bar"
    assert FunctionsTest.test_bar(ob, "bar", 2) == "bar_bar"
    assert ob.not_overriden() == "not_overriden"
    assert list(ob.return_list()) == ["A", "B", "C"]
    assert list(SubClassTest.test_list(ob)) == ["A", "B", "C"]

    x = FunctionsTest.pass_through(ob)
    assert id(x) == id(ob)

def test_broken_derived_class():
    """Test python class derived from managed type with invalid namespace"""
    with pytest.raises(TypeError):
        DerivedClass = broken_derived_class_fixture(test_derived_class.__name__)
        ob = DerivedClass()

def test_derived_traceback():
    """Test python exception traceback in class derived from managed base"""
    class DerivedClass(SubClassTest):
        __namespace__ = "Python.Test.traceback"

        def foo(self):
            print (xyzname)
            return None

    import sys,traceback
    ob = DerivedClass()

    # direct call
    try:
        ob.foo()
        assert False
    except:
        e = sys.exc_info()
    assert "xyzname" in str(e[1])
    location = traceback.extract_tb(e[2])[-1]
    assert location[2] == "foo"

    # call through managed code
    try:
        FunctionsTest.test_foo(ob)
        assert False
    except:
        e = sys.exc_info()
    assert "xyzname" in str(e[1])
    location = traceback.extract_tb(e[2])[-1]
    assert location[2] == "foo"


def test_create_instance():
    """Test derived instances can be created from managed code"""
    DerivedClass = derived_class_fixture(test_create_instance.__name__)
    ob = FunctionsTest.create_instance(DerivedClass)
    assert ob.foo() == "DerivedClass"
    assert FunctionsTest.test_foo(ob) == "DerivedClass"
    assert ob.bar("bar", 2) == "bar_bar"
    assert FunctionsTest.test_bar(ob, "bar", 2) == "bar_bar"
    assert ob.not_overriden() == "not_overriden"

    x = FunctionsTest.pass_through(ob)
    assert id(x) == id(ob)

    InterfaceTestClass = interface_test_class_fixture(test_create_instance.__name__)
    ob2 = FunctionsTest.create_instance_interface(InterfaceTestClass)
    assert ob2.foo() == "InterfaceTestClass"
    assert FunctionsTest.test_foo(ob2) == "InterfaceTestClass"
    assert ob2.bar("bar", 2) == "bar/bar"
    assert FunctionsTest.test_bar(ob2, "bar", 2) == "bar/bar"

    y = FunctionsTest.pass_through_interface(ob2)
    assert id(y) != id(ob2)


def test_events():
    class EventHandler(object):
        def handler(self, x, args):
            self.value = args.value

    event_handler = EventHandler()

    x = SubClassTest()
    x.TestEvent += event_handler.handler
    assert FunctionsTest.test_event(x, 1) == 1
    assert event_handler.value == 1

    InterfaceTestClass = interface_test_class_fixture(test_events.__name__)
    i = InterfaceTestClass()
    with pytest.raises(System.NotImplementedException):
        FunctionsTest.test_event(i, 2)

    DerivedEventTest = derived_event_test_class_fixture(test_events.__name__)
    d = DerivedEventTest()
    d.add_TestEvent(event_handler.handler)
    assert FunctionsTest.test_event(d, 3) == 3
    assert event_handler.value == 3
    assert len(d.event_handlers) == 1


def test_isinstance_check():
    a = [str(x) for x in range(0, 1000)]
    b = [System.String(x) for x in a]

    for x in a:
        assert not isinstance(x, System.Object)
        assert not isinstance(x, System.String)

    for x in b:
        assert isinstance(x, System.Object)
        assert isinstance(x, System.String)

def test_namespace_and_init():
    calls = []
    class TestX(System.Object):
        __namespace__ = "test_clr_subclass_with_init_args"
        def __init__(self, *args, **kwargs):
            calls.append((args, kwargs))
    t = TestX(1,2,3,foo="bar")
    assert len(calls) == 1
    assert calls[0][0] == (1,2,3)
    assert calls[0][1] == {"foo":"bar"}

def test_namespace_and_argless_init():
    calls = []
    class TestX(System.Object):
        __namespace__ = "test_clr_subclass_without_init_args"
        def __init__(self):
            calls.append(True)
    t = TestX()
    assert len(calls) == 1
    assert calls[0] == True


def test_namespace_and_no_init():
    class TestX(System.Object):
        __namespace__ = "test_clr_subclass_without_init"
        q = 1
    t = TestX()
    assert t.q == 1

def test_virtual_generic_method():
    class OverloadingSubclass(GenericVirtualMethodTest):
        __namespace__ = "test_virtual_generic_method_cls"
    class OverloadingSubclass2(OverloadingSubclass):
        pass
    obj = OverloadingSubclass()
    assert obj.VirtMethod[int](5) == 5

def test_interface_and_class_impl():
    class OverloadingSubclass(GenericVirtualMethodTest):
        __namespace__ = "test_virtual_generic_method_cls"
    class OverloadingSubclass2(OverloadingSubclass):
        pass
    obj = OverloadingSubclass()
    assert obj.VirtMethod[int](5) == 5

def test_interface_and_class_impl2():
    class DualSubClass(ISimpleInterface, SimpleClass):
        def Ok(self):
            return True
    class DualSubClass2(ISimpleInterface):
        def Ok(self):
            return True
    class DualSubClass3(ISimpleInterface2):
        def Execute(self, cancellationToken):
            return 0
    try:
        class DualSubClass4(Python.Test.ISimpleInterface3):
            def Execute(self, cancellationToken):
                return 0
        assert False # An exception should be thrown.
    except AttributeError as ae:
        assert ("not defined" in str(ae))

    obj = DualSubClass()
    SimpleClass.TestObject(obj)
    obj = DualSubClass2()
    SimpleClass.TestObject(obj)

    obj2 = DualSubClass3();
    SimpleClass.TestObject(obj2)
    #obj2.Execute(CancellationToken.None)

def test_class_with_attributes():
    import clr
    @clr.attribute(Browsable(False))
    class ClassWithAttributes(ISimpleInterface, SimpleClass):
        __clr_attributes__ = [DebuggerDisplay("X: {X}")]
        @clr.attribute(Browsable(True))
        def Ok(self):
            return True
        @clr.attribute(Browsable(True))
        @clr.clrmethod(int, [int])
        def Method1(x):
            return x

        X = clr.property(Double, 1.0).add_attribute(DebuggerDisplay("Asd"))
    obj = ClassWithAttributes()
    tp = obj.GetType()
    founddisplay = 0
    foundbrowsable = 0
    for attr in Attribute.GetCustomAttributes(tp):
        if isinstance(attr, DebuggerDisplayAttribute):
            founddisplay = founddisplay + 1
        if isinstance(attr, BrowsableAttribute):
            foundbrowsable = foundbrowsable + 1
    SimpleClass.TestObject(obj)
    found_display_on_property = 0
    for attr in Attribute.GetCustomAttributes(tp.GetProperty("X")):
        if isinstance(attr, DebuggerDisplayAttribute):
                    found_display_on_property = found_display_on_property + 1
    found_display_on_method = 0
    for attr in Attribute.GetCustomAttributes(tp.GetMethod("Method1")):
            if isinstance(attr, BrowsableAttribute):
                        found_display_on_method = found_display_on_method + 1
    assert founddisplay == 1
    assert found_display_on_property == 1
    assert found_display_on_method == 1
    assert foundbrowsable == 1
    assert obj.X == 1.0
    SimpleClass.TestObjectProperty(obj, "X", 10.0)
def test_class_with_advanced_attribute():
    import clr
    @clr.attribute(TestAttribute(1, 2, z = "A", W = "B"))
    class ClassWithAttributes2(ISimpleInterface, SimpleClass):
        pass
    @clr.attribute(TestAttributeAttribute, 1, 2, z = "A", W = "B")
    class ClassWithAttributes3(ISimpleInterface, SimpleClass):
        X = clr.property(Double, 1.0).add_attribute(TestAttributeAttribute, 1, 2)

    c = ClassWithAttributes2()
    c2 = ClassWithAttributes3()

def test_subclass_ctor():
    import clr
    class SubClass0(SimpleClass):
        pass
    class SubClass1(SubClass0):
        def __init__(self):
            super().__init__()
    class SubClass2(SubClass1):
        __namespace__ = "TestModule"
        def __init__(self):
            super().__init__()
    SimpleClass.TestOnType(SubClass0)
    SimpleClass.TestOnType(SubClass1)
    SimpleClass.TestOnType(SubClass2)

def test_more_subclasses():
    import clr
    class SubClass0(SimpleClass):
        pass
    class SubClass1(SubClass0):
        X = clr.property(Double, 1.0)
        def __init__(self):
            super().__init__()
            self.Y = 10.0
            SimpleClass.Pause();

    @clr.attribute(DebuggerDisplay("X"))

    class SubClass2(SubClass1):
        __namespace__ = "TestModule"
        def __init__(self):
            SimpleClass.Pause();
            super().__init__()
        def IncrementThing(self):
            super().IncrementThing()
            return 6;
    SimpleClass.TestOnType(SubClass0)
    SimpleClass.TestOnType(SubClass1)
    SimpleClass.TestOnType(SubClass2)
    obj = SimpleClass.InvokeCtor(SubClass2)

    obj2 = SubClass2()
    tp = obj.GetType()
    obj.X = 5.0
    assert obj.Y == 10.0
    assert obj2.Y == 10.0
    assert obj.Initialized == True
    assert obj2.Initialized == True
    SimpleClass.Test1(obj)
    obj = None
    SimpleClass.Test2()

def abstract_test():
    class abstractClass(SimpleClass):
        __clr_abstract__ = True
    failed = False
    try:
        abstractClass()
    except:
        failed = True
    assert failed

def test_construction_from_clr():
    import clr
    calls = []
    class TestX(System.Object):
        __namespace__ = "test_clr_subclass_init_from_clr"
        @clr.clrmethod(None, [int, str])
        def __init__(self, i, s):
            calls.append((i, s))

    # Construct a TestX from Python
    t = TestX(1, "foo")
    assert len(calls) == 1
    assert calls[0][0] == 1
    assert calls[0][1] == "foo"

    # Reset calls and construct a TestX from CLR
    calls = []
    tp = t.GetType()
    t2 = tp.GetConstructors()[0].Invoke(None)
    assert len(calls) == 0

    # The object has only been constructed, now it needs to be initialized as well
    tp.GetMethod("__init__").Invoke(t2, [1, "foo"])
    assert len(calls) == 1
    assert calls[0][0] == 1
    assert calls[0][1] == "foo"

# regression test for https://github.com/pythonnet/pythonnet/issues/1565
def test_can_be_collected_by_gc():
    from Python.Test import BaseClass

    class Derived(BaseClass):
        __namespace__ = 'test_can_be_collected_by_gc'

    inst = Derived()
    cycle = [inst]
    del inst
    cycle.append(cycle)
    del cycle

    import gc
    gc.collect()
def test_more_subclasses2():
    import clr
    class SubClass50(SimpleClass):
        def __init__(self):
           super().__init__()
        def IncrementThing(self):
           return super().IncrementThing()

    @clr.attribute(DebuggerDisplay("X"))

    class SubClass51(SubClass50):
        __namespace__ = "TestModule"
        def __init__(self):
            super().__init__()

        def IncrementThing(self):
            return super().IncrementThing() + 10
    x = SubClass51()
    print(x.CallIncrementThing())
    print(x.CallIncrementThing())
    print(x.CallIncrementThing())



def test_generic_interface():
    from System import Int32
    from Python.Test import GenericInterfaceUser, SpecificInterfaceUser

    GenericInterfaceImpl = interface_generic_class_fixture(test_generic_interface.__name__)

    obj = GenericInterfaceImpl()
    SpecificInterfaceUser(obj, Int32(0))
    GenericInterfaceUser[Int32](obj, Int32(0))
