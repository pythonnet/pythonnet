# -*- coding: utf-8 -*-
# TODO: Add tests for ClassicClass, NewStyleClass?

import unittest

import Python.Test as Test
import System

from _compat import DictProxyType, range


class ClassTests(unittest.TestCase):
    """Test CLR class support."""

    def test_basic_reference_type(self):
        """Test usage of CLR defined reference types."""
        self.assertEquals(System.String.Empty, "")

    def test_basic_value_type(self):
        """Test usage of CLR defined value types."""
        self.assertEquals(System.Int32.MaxValue, 2147483647)

    def test_class_standard_attrs(self):
        """Test standard class attributes."""
        from Python.Test import ClassTest

        self.assertTrue(ClassTest.__name__ == 'ClassTest')
        self.assertTrue(ClassTest.__module__ == 'Python.Test')
        self.assertTrue(isinstance(ClassTest.__dict__, DictProxyType))
        self.assertTrue(len(ClassTest.__doc__) > 0)

    def test_class_docstrings(self):
        """Test standard class docstring generation"""
        from Python.Test import ClassTest

        value = 'Void .ctor()'
        self.assertTrue(ClassTest.__doc__ == value)

    def test_class_default_str(self):
        """Test the default __str__ implementation for managed objects."""
        s = System.String("this is a test")
        self.assertTrue(str(s) == "this is a test")

    def test_class_default_repr(self):
        """Test the default __repr__ implementation for managed objects."""
        s = System.String("this is a test")
        self.assertTrue(repr(s).startswith("<System.String object"))

    def test_non_public_class(self):
        """Test that non-public classes are inaccessible."""
        with self.assertRaises(ImportError):
            from Python.Test import InternalClass

        with self.assertRaises(AttributeError):
            _ = Test.InternalClass

    def test_basic_subclass(self):
        """Test basic subclass of a managed class."""
        from System.Collections import Hashtable

        class MyTable(Hashtable):
            def how_many(self):
                return self.Count

        table = MyTable()

        self.assertTrue(table.__class__.__name__.endswith('MyTable'))
        self.assertTrue(type(table).__name__.endswith('MyTable'))
        self.assertTrue(len(table.__class__.__bases__) == 1)
        self.assertTrue(table.__class__.__bases__[0] == Hashtable)

        self.assertTrue(table.how_many() == 0)
        self.assertTrue(table.Count == 0)

        table.set_Item('one', 'one')

        self.assertTrue(table.how_many() == 1)
        self.assertTrue(table.Count == 1)

    def test_subclass_with_no_arg_constructor(self):
        """Test subclass of a managed class with a no-arg constructor."""
        from Python.Test import ClassCtorTest1

        class SubClass(ClassCtorTest1):
            def __init__(self, name):
                self.name = name

        # This failed in earlier versions
        _ = SubClass('test')

    def test_subclass_with_various_constructors(self):
        """Test subclass of a managed class with various constructors."""
        from Python.Test import ClassCtorTest2

        class SubClass(ClassCtorTest2):
            def __init__(self, v):
                ClassCtorTest2.__init__(self)
                self.value = v

        inst = SubClass('test')
        self.assertTrue(inst.value == 'test')

        class SubClass2(ClassCtorTest2):
            def __init__(self, v):
                ClassCtorTest2.__init__(self)
                self.value = v

        inst = SubClass2('test')
        self.assertTrue(inst.value == 'test')

    def test_struct_construction(self):
        """Test construction of structs."""
        from System.Drawing import Point

        p = Point()
        self.assertTrue(p.X == 0)
        self.assertTrue(p.Y == 0)

        p = Point(0, 0)
        self.assertTrue(p.X == 0)
        self.assertTrue(p.Y == 0)

        p.X = 10
        p.Y = 10

        self.assertTrue(p.X == 10)
        self.assertTrue(p.Y == 10)

    # test strange __new__ interactions

    # test weird metatype
    # test recursion
    # test

    def test_ienumerable_iteration(self):
        """Test iteration over objects supporting IEnumerable."""
        from Python.Test import ClassTest

        list_ = ClassTest.GetArrayList()

        for item in list_:
            self.assertTrue((item > -1) and (item < 10))

        dict_ = ClassTest.GetHashtable()

        for item in dict_:
            cname = item.__class__.__name__
            self.assertTrue(cname.endswith('DictionaryEntry'))

    def test_ienumerator_iteration(self):
        """Test iteration over objects supporting IEnumerator."""
        from Python.Test import ClassTest

        chars = ClassTest.GetEnumerator()

        for item in chars:
            self.assertTrue(item in 'test string')

    def test_override_get_item(self):
        """Test managed subclass overriding __getitem__."""
        from System.Collections import Hashtable

        class MyTable(Hashtable):
            def __getitem__(self, key):
                value = Hashtable.__getitem__(self, key)
                return 'my ' + str(value)

        table = MyTable()
        table['one'] = 'one'
        table['two'] = 'two'
        table['three'] = 'three'

        self.assertTrue(table['one'] == 'my one')
        self.assertTrue(table['two'] == 'my two')
        self.assertTrue(table['three'] == 'my three')

        self.assertTrue(table.Count == 3)

    def test_override_set_item(self):
        """Test managed subclass overriding __setitem__."""
        from System.Collections import Hashtable

        class MyTable(Hashtable):
            def __setitem__(self, key, value):
                value = 'my ' + str(value)
                Hashtable.__setitem__(self, key, value)

        table = MyTable()
        table['one'] = 'one'
        table['two'] = 'two'
        table['three'] = 'three'

        self.assertTrue(table['one'] == 'my one')
        self.assertTrue(table['two'] == 'my two')
        self.assertTrue(table['three'] == 'my three')

        self.assertTrue(table.Count == 3)

    def test_add_and_remove_class_attribute(self):
        from System import TimeSpan

        for _ in range(100):
            TimeSpan.new_method = lambda self_: self_.TotalMinutes
            ts = TimeSpan.FromHours(1)
            self.assertTrue(ts.new_method() == 60)
            del TimeSpan.new_method
            self.assertFalse(hasattr(ts, "new_method"))

    def test_comparisons(self):
        from System import DateTimeOffset
        from Python.Test import ClassTest

        d1 = DateTimeOffset.Parse("2016-11-14")
        d2 = DateTimeOffset.Parse("2016-11-15")

        self.assertEqual(d1 == d2, False)
        self.assertEqual(d1 != d2, True)

        self.assertEqual(d1 < d2, True)
        self.assertEqual(d1 <= d2, True)
        self.assertEqual(d1 >= d2, False)
        self.assertEqual(d1 > d2, False)

        self.assertEqual(d1 == d1, True)
        self.assertEqual(d1 != d1, False)

        self.assertEqual(d1 < d1, False)
        self.assertEqual(d1 <= d1, True)
        self.assertEqual(d1 >= d1, True)
        self.assertEqual(d1 > d1, False)

        self.assertEqual(d2 == d1, False)
        self.assertEqual(d2 != d1, True)

        self.assertEqual(d2 < d1, False)
        self.assertEqual(d2 <= d1, False)
        self.assertEqual(d2 >= d1, True)
        self.assertEqual(d2 > d1, True)

        with self.assertRaises(TypeError):
            d1 < None

        with self.assertRaises(TypeError):
            d1 < System.Guid()

        # ClassTest does not implement IComparable
        c1 = ClassTest()
        c2 = ClassTest()
        with self.assertRaises(TypeError):
            c1 < c2

    def test_self_callback(self):
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
        self.assertTrue(testobj.PyCallbackWasCalled)
        self.assertTrue(testobj.SameReference)


def test_suite():
    return unittest.makeSuite(ClassTests)
