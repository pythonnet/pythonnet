# -*- coding: utf-8 -*-
# TODO: Add tests for ClassicClass, NewStyleClass?

"""Test CLR class support."""

import Python.Test as Test
import System
import pytest

from ._compat import DictProxyType, range


def test_basic_reference_type():
    """Test usage of CLR defined reference types."""
    assert System.String.Empty == ""


def test_basic_value_type():
    """Test usage of CLR defined value types."""
    assert System.Int32.MaxValue == 2147483647


def test_class_standard_attrs():
    """Test standard class attributes."""
    from Python.Test import ClassTest

    assert ClassTest.__name__ == 'ClassTest'
    assert ClassTest.__module__ == 'Python.Test'
    assert isinstance(ClassTest.__dict__, DictProxyType)
    assert len(ClassTest.__doc__) > 0


def test_class_docstrings():
    """Test standard class docstring generation"""
    from Python.Test import ClassTest

    value = 'Void .ctor()'
    assert ClassTest.__doc__ == value


def test_class_default_str():
    """Test the default __str__ implementation for managed objects."""
    s = System.String("this is a test")
    assert str(s) == "this is a test"


def test_class_default_repr():
    """Test the default __repr__ implementation for managed objects."""
    s = System.String("this is a test")
    assert repr(s).startswith("<System.String object")


def test_non_public_class():
    """Test that non-public classes are inaccessible."""
    with pytest.raises(ImportError):
        from Python.Test import InternalClass

    with pytest.raises(AttributeError):
        _ = Test.InternalClass


def test_basic_subclass():
    """Test basic subclass of a managed class."""
    # Uses ArrayList instead of Hashtable
    # As it's available from .Net Core 2.0 without an addref
    from System.Collections import ArrayList

    class MyArrayList(ArrayList):
        def how_many(self):
            return self.Count

    array_list = MyArrayList()

    assert array_list.__class__.__name__.endswith('MyArrayList')
    assert type(array_list).__name__.endswith('MyArrayList')
    assert len(array_list.__class__.__bases__) == 1
    assert array_list.__class__.__bases__[0] == ArrayList

    assert array_list.how_many() == 0
    assert array_list.Count == 0

    array_list.Add('one')

    assert array_list.how_many() == 1
    assert array_list.Count == 1


def test_subclass_with_no_arg_constructor():
    """Test subclass of a managed class with a no-arg constructor."""
    from Python.Test import ClassCtorTest1

    class SubClass(ClassCtorTest1):
        def __init__(self, name):
            self.name = name

    # This failed in earlier versions
    _ = SubClass('test')


def test_subclass_with_various_constructors():
    """Test subclass of a managed class with various constructors."""
    from Python.Test import ClassCtorTest2

    class SubClass(ClassCtorTest2):
        def __init__(self, v):
            ClassCtorTest2.__init__(self)
            self.value = v

    inst = SubClass('test')
    assert inst.value == 'test'

    class SubClass2(ClassCtorTest2):
        def __init__(self, v):
            ClassCtorTest2.__init__(self)
            self.value = v

    inst = SubClass2('test')
    assert inst.value == 'test'


def test_struct_construction():
    """Test construction of structs."""
    # Uses DateTime instead of Point
    # As it's available from .Net Core 2.0 without an addref
    from System import DateTime

    dt1 = DateTime(2017, 2, 27)
    assert dt1.Day == 27
    assert dt1.Month == 2
    assert dt1.Year == 2017

    # Static method calls default constructor
    dt2 = DateTime.MinValue
    assert dt2.Day == 1
    assert dt2.Month == 1
    assert dt2.Year == 1

    # mutation tests removed
    # structs are not typically mutable

    # test strange __new__ interactions

    # test weird metatype
    # test recursion
    # test


def test_ienumerable_iteration():
    """Test iteration over objects supporting IEnumerable."""
    from Python.Test import ClassTest

    list_ = ClassTest.GetArrayList()

    for item in list_:
        assert (item > -1) and (item < 10)

    dict_ = ClassTest.GetHashtable()

    for item in dict_:
        cname = item.__class__.__name__
        assert cname.endswith('DictionaryEntry')


def test_ienumerator_iteration():
    """Test iteration over objects supporting IEnumerator."""
    from Python.Test import ClassTest

    chars = ClassTest.GetEnumerator()

    for item in chars:
        assert item in 'test string'


def test_override_get_item():
    """Test managed subclass overriding __getitem__."""
    from System.Collections import ArrayList
    
    # Uses ArrayList instead of Hashtable
    # As it's available from .Net Core 2.0 without an addref
    class MyArrayList(ArrayList):
        def __getitem__(self, key):
            value = ArrayList.__getitem__(self, key)
            return 'my ' + str(value)

    array_list = MyArrayList()
    array_list.Add('zero')
    array_list.Add('one')
    array_list.Add('two')

    assert array_list[0] == 'my zero'
    assert array_list[1] == 'my one'
    assert array_list[2] == 'my two'

    assert array_list.Count == 3


def test_override_set_item():
    """Test managed subclass overriding __setitem__."""
    from System.Collections import ArrayList
    
    # Uses ArrayList instead of Hashtable
    # As it's available from .Net Core 2.0 without an addref
    class MyArrayList(ArrayList):
        def __setitem__(self, key, value):
            value = 'your ' + str(value)
            ArrayList.__setitem__(self, key, value)

    array_list = MyArrayList()
    # need to add three items first
    array_list.Add("a")
    array_list.Add("b")
    array_list.Add("c")
    
    array_list[0] = 'zero'
    array_list[1] = 'one'
    array_list[2] = 'two'

    assert array_list[0] == 'your zero'
    assert array_list[1] == 'your one'
    assert array_list[2] == 'your two'

    assert array_list.Count == 3


def test_add_and_remove_class_attribute():
    from System import TimeSpan

    for _ in range(100):
        TimeSpan.new_method = lambda self_: self_.TotalMinutes
        ts = TimeSpan.FromHours(1)
        assert ts.new_method() == 60
        del TimeSpan.new_method
        assert not hasattr(ts, "new_method")


def test_comparisons():
    from System import DateTimeOffset
    from Python.Test import ClassTest

    d1 = DateTimeOffset.Parse("2016-11-14")
    d2 = DateTimeOffset.Parse("2016-11-15")

    assert (d1 == d2) == False
    assert (d1 != d2) == True

    assert (d1 < d2) == True
    assert (d1 <= d2) == True
    assert (d1 >= d2) == False
    assert (d1 > d2) == False

    assert (d1 == d1) == True
    assert (d1 != d1) == False

    assert (d1 < d1) == False
    assert (d1 <= d1) == True
    assert (d1 >= d1) == True
    assert (d1 > d1) == False

    assert (d2 == d1) == False
    assert (d2 != d1) == True

    assert (d2 < d1) == False
    assert (d2 <= d1) == False
    assert (d2 >= d1) == True
    assert (d2 > d1) == True

    with pytest.raises(TypeError):
        d1 < None

    with pytest.raises(TypeError):
        d1 < System.Guid()

    # ClassTest does not implement IComparable
    c1 = ClassTest()
    c2 = ClassTest()
    with pytest.raises(TypeError):
        c1 < c2


def test_self_callback():
    """Test calling back and forth between this and a c# baseclass."""

    class CallbackUser(Test.SelfCallbackTest):
        def DoCallback(self):
            self.PyCallbackWasCalled = False
            self.SameReference = False
            return self.Callback(self)

        def PyCallback(self, self2):
            self.PyCallbackWasCalled = True
            self.SameReference = self == self2

    testobj = CallbackUser()
    testobj.DoCallback()
    assert testobj.PyCallbackWasCalled
    assert testobj.SameReference
