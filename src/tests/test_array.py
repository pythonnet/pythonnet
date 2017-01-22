# -*- coding: utf-8 -*-

import unittest

import Python.Test as Test
import System

from _compat import PY2, UserList, long, range, unichr


class ArrayTests(unittest.TestCase):
    """Test support for managed arrays."""

    def test_public_array(self):
        """Test public arrays."""
        ob = Test.PublicArrayTest()
        items = ob.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == 0)
        self.assertTrue(items[4] == 4)

        items[0] = 8
        self.assertTrue(items[0] == 8)

        items[4] = 9
        self.assertTrue(items[4] == 9)

        items[-4] = 0
        self.assertTrue(items[-4] == 0)

        items[-1] = 4
        self.assertTrue(items[-1] == 4)

    def test_protected_array(self):
        """Test protected arrays."""
        ob = Test.ProtectedArrayTest()
        items = ob.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == 0)
        self.assertTrue(items[4] == 4)

        items[0] = 8
        self.assertTrue(items[0] == 8)

        items[4] = 9
        self.assertTrue(items[4] == 9)

        items[-4] = 0
        self.assertTrue(items[-4] == 0)

        items[-1] = 4
        self.assertTrue(items[-1] == 4)

    def test_internal_array(self):
        """Test internal arrays."""

        with self.assertRaises(AttributeError):
            ob = Test.InternalArrayTest()
            _ = ob.items

    def test_private_array(self):
        """Test private arrays."""

        with self.assertRaises(AttributeError):
            ob = Test.PrivateArrayTest()
            _ = ob.items

    def test_array_bounds_checking(self):
        """Test array bounds checking."""

        ob = Test.Int32ArrayTest()
        items = ob.items

        self.assertTrue(items[0] == 0)
        self.assertTrue(items[1] == 1)
        self.assertTrue(items[2] == 2)
        self.assertTrue(items[3] == 3)
        self.assertTrue(items[4] == 4)

        self.assertTrue(items[-5] == 0)
        self.assertTrue(items[-4] == 1)
        self.assertTrue(items[-3] == 2)
        self.assertTrue(items[-2] == 3)
        self.assertTrue(items[-1] == 4)

        with self.assertRaises(IndexError):
            ob = Test.Int32ArrayTest()
            _ = ob.items[5]

        with self.assertRaises(IndexError):
            ob = Test.Int32ArrayTest()
            ob.items[5] = 0

        with self.assertRaises(IndexError):
            ob = Test.Int32ArrayTest()
            items[-6]

        with self.assertRaises(IndexError):
            ob = Test.Int32ArrayTest()
            items[-6] = 0

    def test_array_contains(self):
        """Test array support for __contains__."""

        ob = Test.Int32ArrayTest()
        items = ob.items

        self.assertTrue(0 in items)
        self.assertTrue(1 in items)
        self.assertTrue(2 in items)
        self.assertTrue(3 in items)
        self.assertTrue(4 in items)

        self.assertFalse(5 in items)  # "H:\Python27\Lib\unittest\case.py", line 592, in deprecated_func,
        self.assertFalse(-1 in items)  # TypeError: int() argument must be a string or a number, not 'NoneType'
        self.assertFalse(None in items)  # which threw ^ here which is a little odd.
        # But when run from runtests.py. Not when this module ran by itself.

    def test_boolean_array(self):
        """Test boolean arrays."""
        ob = Test.BooleanArrayTest()
        items = ob.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] is True)
        self.assertTrue(items[1] is False)
        self.assertTrue(items[2] is True)
        self.assertTrue(items[3] is False)
        self.assertTrue(items[4] is True)

        items[0] = False
        self.assertTrue(items[0] is False)

        items[0] = True
        self.assertTrue(items[0] is True)

        with self.assertRaises(TypeError):
            ob = Test.ByteArrayTest()
            _ = ob.items["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.ByteArrayTest()
            ob[0] = "wrong"

    def test_byte_array(self):
        """Test byte arrays."""
        ob = Test.ByteArrayTest()
        items = ob.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == 0)
        self.assertTrue(items[4] == 4)

        max_ = 255
        min_ = 0

        items[0] = max_
        self.assertTrue(items[0] == max_)

        items[0] = min_
        self.assertTrue(items[0] == min_)

        items[-4] = max_
        self.assertTrue(items[-4] == max_)

        items[-1] = min_
        self.assertTrue(items[-1] == min_)

        with self.assertRaises(OverflowError):
            ob = Test.ByteArrayTest()
            ob.items[0] = max_ + 1

        with self.assertRaises(OverflowError):
            ob = Test.ByteArrayTest()
            ob.items[0] = min_ - 1

        with self.assertRaises(TypeError):
            ob = Test.ByteArrayTest()
            _ = ob.items["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.ByteArrayTest()
            ob[0] = "wrong"

    def test_sbyte_array(self):
        """Test sbyte arrays."""
        ob = Test.SByteArrayTest()
        items = ob.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == 0)
        self.assertTrue(items[4] == 4)

        max_ = 127
        min_ = -128

        items[0] = max_
        self.assertTrue(items[0] == max_)

        items[0] = min_
        self.assertTrue(items[0] == min_)

        items[-4] = max_
        self.assertTrue(items[-4] == max_)

        items[-1] = min_
        self.assertTrue(items[-1] == min_)

        with self.assertRaises(OverflowError):
            ob = Test.SByteArrayTest()
            ob.items[0] = max_ + 1

        with self.assertRaises(OverflowError):
            ob = Test.SByteArrayTest()
            ob.items[0] = min_ - 1

        with self.assertRaises(TypeError):
            ob = Test.SByteArrayTest()
            _ = ob.items["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.SByteArrayTest()
            ob[0] = "wrong"

    def test_char_array(self):
        """Test char arrays."""
        ob = Test.CharArrayTest()
        items = ob.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == 'a')
        self.assertTrue(items[4] == 'e')

        max_ = unichr(65535)
        min_ = unichr(0)

        items[0] = max_
        self.assertTrue(items[0] == max_)

        items[0] = min_
        self.assertTrue(items[0] == min_)

        items[-4] = max_
        self.assertTrue(items[-4] == max_)

        items[-1] = min_
        self.assertTrue(items[-1] == min_)

        with self.assertRaises(TypeError):
            ob = Test.CharArrayTest()
            _ = ob.items["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.CharArrayTest()
            ob[0] = "wrong"

    def test_int16_array(self):
        """Test Int16 arrays."""
        ob = Test.Int16ArrayTest()
        items = ob.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == 0)
        self.assertTrue(items[4] == 4)

        max_ = 32767
        min_ = -32768

        items[0] = max_
        self.assertTrue(items[0] == max_)

        items[0] = min_
        self.assertTrue(items[0] == min_)

        items[-4] = max_
        self.assertTrue(items[-4] == max_)

        items[-1] = min_
        self.assertTrue(items[-1] == min_)

        with self.assertRaises(OverflowError):
            ob = Test.Int16ArrayTest()
            ob.items[0] = max_ + 1

        with self.assertRaises(OverflowError):
            ob = Test.Int16ArrayTest()
            ob.items[0] = min_ - 1

        with self.assertRaises(TypeError):
            ob = Test.Int16ArrayTest()
            _ = ob.items["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.Int16ArrayTest()
            ob[0] = "wrong"

    def test_int32_array(self):
        """Test Int32 arrays."""
        ob = Test.Int32ArrayTest()
        items = ob.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == 0)
        self.assertTrue(items[4] == 4)

        max_ = 2147483647
        min_ = -2147483648

        items[0] = max_
        self.assertTrue(items[0] == max_)

        items[0] = min_
        self.assertTrue(items[0] == min_)

        items[-4] = max_
        self.assertTrue(items[-4] == max_)

        items[-1] = min_
        self.assertTrue(items[-1] == min_)

        with self.assertRaises(OverflowError):
            ob = Test.Int32ArrayTest()
            ob.items[0] = max_ + 1

        with self.assertRaises(OverflowError):
            ob = Test.Int32ArrayTest()
            ob.items[0] = min_ - 1

        with self.assertRaises(TypeError):
            ob = Test.Int32ArrayTest()
            _ = ob.items["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.Int32ArrayTest()
            ob[0] = "wrong"

    def test_int64_array(self):
        """Test Int64 arrays."""
        ob = Test.Int64ArrayTest()
        items = ob.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == 0)
        self.assertTrue(items[4] == 4)

        max_ = long(9223372036854775807)
        min_ = long(-9223372036854775808)

        items[0] = max_
        self.assertTrue(items[0] == max_)

        items[0] = min_
        self.assertTrue(items[0] == min_)

        items[-4] = max_
        self.assertTrue(items[-4] == max_)

        items[-1] = min_
        self.assertTrue(items[-1] == min_)

        with self.assertRaises(OverflowError):
            ob = Test.Int64ArrayTest()
            ob.items[0] = max_ + 1

        with self.assertRaises(OverflowError):
            ob = Test.Int64ArrayTest()
            ob.items[0] = min_ - 1

        with self.assertRaises(TypeError):
            ob = Test.Int64ArrayTest()
            _ = ob.items["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.Int64ArrayTest()
            ob[0] = "wrong"

    def test_uint16_array(self):
        """Test UInt16 arrays."""
        ob = Test.UInt16ArrayTest()
        items = ob.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == 0)
        self.assertTrue(items[4] == 4)

        max_ = 65535
        min_ = 0

        items[0] = max_
        self.assertTrue(items[0] == max_)

        items[0] = min_
        self.assertTrue(items[0] == min_)

        items[-4] = max_
        self.assertTrue(items[-4] == max_)

        items[-1] = min_
        self.assertTrue(items[-1] == min_)

        with self.assertRaises(OverflowError):
            ob = Test.UInt16ArrayTest()
            ob.items[0] = max_ + 1

        with self.assertRaises(OverflowError):
            ob = Test.UInt16ArrayTest()
            ob.items[0] = min_ - 1

        with self.assertRaises(TypeError):
            ob = Test.UInt16ArrayTest()
            _ = ob.items["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.UInt16ArrayTest()
            ob[0] = "wrong"

    def test_uint32_array(self):
        """Test UInt32 arrays."""
        ob = Test.UInt32ArrayTest()
        items = ob.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == 0)
        self.assertTrue(items[4] == 4)

        max_ = long(4294967295)
        min_ = 0

        items[0] = max_
        self.assertTrue(items[0] == max_)

        items[0] = min_
        self.assertTrue(items[0] == min_)

        items[-4] = max_
        self.assertTrue(items[-4] == max_)

        items[-1] = min_
        self.assertTrue(items[-1] == min_)

        with self.assertRaises(OverflowError):
            ob = Test.UInt32ArrayTest()
            ob.items[0] = max_ + 1

        with self.assertRaises(OverflowError):
            ob = Test.UInt32ArrayTest()
            ob.items[0] = min_ - 1

        with self.assertRaises(TypeError):
            ob = Test.UInt32ArrayTest()
            _ = ob.items["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.UInt32ArrayTest()
            ob[0] = "wrong"

    def test_uint64_array(self):
        """Test UInt64 arrays."""
        ob = Test.UInt64ArrayTest()
        items = ob.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == 0)
        self.assertTrue(items[4] == 4)

        max_ = long(18446744073709551615)
        min_ = 0

        items[0] = max_
        self.assertTrue(items[0] == max_)

        items[0] = min_
        self.assertTrue(items[0] == min_)

        items[-4] = max_
        self.assertTrue(items[-4] == max_)

        items[-1] = min_
        self.assertTrue(items[-1] == min_)

        with self.assertRaises(OverflowError):
            ob = Test.UInt64ArrayTest()
            ob.items[0] = max_ + 1

        with self.assertRaises(OverflowError):
            ob = Test.UInt64ArrayTest()
            ob.items[0] = min_ - 1

        with self.assertRaises(TypeError):
            ob = Test.UInt64ArrayTest()
            _ = ob.items["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.UInt64ArrayTest()
            ob[0] = "wrong"

    def test_single_array(self):
        """Test Single arrays."""
        ob = Test.SingleArrayTest()
        items = ob.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == 0.0)
        self.assertTrue(items[4] == 4.0)

        max_ = 3.402823e38
        min_ = -3.402823e38

        items[0] = max_
        self.assertTrue(items[0] == max_)

        items[0] = min_
        self.assertTrue(items[0] == min_)

        items[-4] = max_
        self.assertTrue(items[-4] == max_)

        items[-1] = min_
        self.assertTrue(items[-1] == min_)

        with self.assertRaises(TypeError):
            ob = Test.SingleArrayTest()
            _ = ob.items["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.SingleArrayTest()
            ob[0] = "wrong"

    def test_double_array(self):
        """Test Double arrays."""
        ob = Test.DoubleArrayTest()
        items = ob.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == 0.0)
        self.assertTrue(items[4] == 4.0)

        max_ = 1.7976931348623157e308
        min_ = -1.7976931348623157e308

        items[0] = max_
        self.assertTrue(items[0] == max_)

        items[0] = min_
        self.assertTrue(items[0] == min_)

        items[-4] = max_
        self.assertTrue(items[-4] == max_)

        items[-1] = min_
        self.assertTrue(items[-1] == min_)

        with self.assertRaises(TypeError):
            ob = Test.DoubleArrayTest()
            _ = ob.items["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.DoubleArrayTest()
            ob[0] = "wrong"

    def test_decimal_array(self):
        """Test Decimal arrays."""
        ob = Test.DecimalArrayTest()
        items = ob.items

        from System import Decimal
        max_d = Decimal.Parse("79228162514264337593543950335")
        min_d = Decimal.Parse("-79228162514264337593543950335")

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == Decimal(0))
        self.assertTrue(items[4] == Decimal(4))

        items[0] = max_d
        self.assertTrue(items[0] == max_d)

        items[0] = min_d
        self.assertTrue(items[0] == min_d)

        items[-4] = max_d
        self.assertTrue(items[-4] == max_d)

        items[-1] = min_d
        self.assertTrue(items[-1] == min_d)

        with self.assertRaises(TypeError):
            ob = Test.DecimalArrayTest()
            _ = ob.items["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.DecimalArrayTest()
            ob[0] = "wrong"

    def test_string_array(self):
        """Test String arrays."""
        ob = Test.StringArrayTest()
        items = ob.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == '0')
        self.assertTrue(items[4] == '4')

        items[0] = "spam"
        self.assertTrue(items[0] == "spam")

        items[0] = "eggs"
        self.assertTrue(items[0] == "eggs")

        items[-4] = "spam"
        self.assertTrue(items[-4] == "spam")

        items[-1] = "eggs"
        self.assertTrue(items[-1] == "eggs")

        with self.assertRaises(TypeError):
            ob = Test.StringArrayTest()
            _ = ob.items["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.Int64ArrayTest()
            ob[0] = 0

    def test_enum_array(self):
        """Test enum arrays."""
        from Python.Test import ShortEnum
        ob = Test.EnumArrayTest()
        items = ob.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == ShortEnum.Zero)
        self.assertTrue(items[4] == ShortEnum.Four)

        items[0] = ShortEnum.Four
        self.assertTrue(items[0] == ShortEnum.Four)

        items[0] = ShortEnum.Zero
        self.assertTrue(items[0] == ShortEnum.Zero)

        items[-4] = ShortEnum.Four
        self.assertTrue(items[-4] == ShortEnum.Four)

        items[-1] = ShortEnum.Zero
        self.assertTrue(items[-1] == ShortEnum.Zero)

        with self.assertRaises(ValueError):
            ob = Test.EnumArrayTest()
            ob.items[0] = 99

        with self.assertRaises(TypeError):
            ob = Test.EnumArrayTest()
            _ = ob.items["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.EnumArrayTest()
            ob[0] = "wrong"

    def test_object_array(self):
        """Test ob arrays."""
        from Python.Test import Spam
        ob = Test.ObjectArrayTest()
        items = ob.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0].GetValue() == "0")
        self.assertTrue(items[4].GetValue() == "4")

        items[0] = Spam("4")
        self.assertTrue(items[0].GetValue() == "4")

        items[0] = Spam("0")
        self.assertTrue(items[0].GetValue() == "0")

        items[-4] = Spam("4")
        self.assertTrue(items[-4].GetValue() == "4")

        items[-1] = Spam("0")
        self.assertTrue(items[-1].GetValue() == "0")

        items[0] = 99
        self.assertTrue(items[0] == 99)

        items[0] = None
        self.assertTrue(items[0] is None)

        with self.assertRaises(TypeError):
            ob = Test.ObjectArrayTest()
            _ = ob.items["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.ObjectArrayTest()
            ob.items["wrong"] = "wrong"

    def test_null_array(self):
        """Test null arrays."""
        ob = Test.NullArrayTest()
        items = ob.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] is None)
        self.assertTrue(items[4] is None)

        items[0] = "spam"
        self.assertTrue(items[0] == "spam")

        items[0] = None
        self.assertTrue(items[0] is None)

        items[-4] = "spam"
        self.assertTrue(items[-4] == "spam")

        items[-1] = None
        self.assertTrue(items[-1] is None)

        empty = ob.empty
        self.assertTrue(len(empty) == 0)

        with self.assertRaises(TypeError):
            ob = Test.NullArrayTest()
            _ = ob.items["wrong"]

    def test_interface_array(self):
        """Test interface arrays."""
        from Python.Test import Spam
        ob = Test.InterfaceArrayTest()
        items = ob.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0].GetValue() == "0")
        self.assertTrue(items[4].GetValue() == "4")

        items[0] = Spam("4")
        self.assertTrue(items[0].GetValue() == "4")

        items[0] = Spam("0")
        self.assertTrue(items[0].GetValue() == "0")

        items[-4] = Spam("4")
        self.assertTrue(items[-4].GetValue() == "4")

        items[-1] = Spam("0")
        self.assertTrue(items[-1].GetValue() == "0")

        items[0] = None
        self.assertTrue(items[0] is None)

        with self.assertRaises(TypeError):
            ob = Test.InterfaceArrayTest()
            ob.items[0] = 99

        with self.assertRaises(TypeError):
            ob = Test.InterfaceArrayTest()
            _ = ob.items["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.InterfaceArrayTest()
            ob.items["wrong"] = "wrong"

    def test_typed_array(self):
        """Test typed arrays."""
        from Python.Test import Spam
        ob = Test.TypedArrayTest()
        items = ob.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0].GetValue() == "0")
        self.assertTrue(items[4].GetValue() == "4")

        items[0] = Spam("4")
        self.assertTrue(items[0].GetValue() == "4")

        items[0] = Spam("0")
        self.assertTrue(items[0].GetValue() == "0")

        items[-4] = Spam("4")
        self.assertTrue(items[-4].GetValue() == "4")

        items[-1] = Spam("0")
        self.assertTrue(items[-1].GetValue() == "0")

        items[0] = None
        self.assertTrue(items[0] is None)

        with self.assertRaises(TypeError):
            ob = Test.TypedArrayTest()
            ob.items[0] = 99

        with self.assertRaises(TypeError):
            ob = Test.TypedArrayTest()
            _ = ob.items["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.TypedArrayTest()
            ob.items["wrong"] = "wrong"

    def test_multi_dimensional_array(self):
        """Test multi-dimensional arrays."""
        ob = Test.MultiDimensionalArrayTest()
        items = ob.items

        self.assertTrue(len(items) == 25)

        self.assertTrue(items[0, 0] == 0)
        self.assertTrue(items[0, 1] == 1)
        self.assertTrue(items[0, 2] == 2)
        self.assertTrue(items[0, 3] == 3)
        self.assertTrue(items[0, 4] == 4)
        self.assertTrue(items[1, 0] == 5)
        self.assertTrue(items[1, 1] == 6)
        self.assertTrue(items[1, 2] == 7)
        self.assertTrue(items[1, 3] == 8)
        self.assertTrue(items[1, 4] == 9)
        self.assertTrue(items[2, 0] == 10)
        self.assertTrue(items[2, 1] == 11)
        self.assertTrue(items[2, 2] == 12)
        self.assertTrue(items[2, 3] == 13)
        self.assertTrue(items[2, 4] == 14)
        self.assertTrue(items[3, 0] == 15)
        self.assertTrue(items[3, 1] == 16)
        self.assertTrue(items[3, 2] == 17)
        self.assertTrue(items[3, 3] == 18)
        self.assertTrue(items[3, 4] == 19)
        self.assertTrue(items[4, 0] == 20)
        self.assertTrue(items[4, 1] == 21)
        self.assertTrue(items[4, 2] == 22)
        self.assertTrue(items[4, 3] == 23)
        self.assertTrue(items[4, 4] == 24)

        max_ = 2147483647
        min_ = -2147483648

        items[0, 0] = max_
        self.assertTrue(items[0, 0] == max_)

        items[0, 0] = min_
        self.assertTrue(items[0, 0] == min_)

        items[-4, 0] = max_
        self.assertTrue(items[-4, 0] == max_)

        items[-1, -1] = min_
        self.assertTrue(items[-1, -1] == min_)

        with self.assertRaises(OverflowError):
            ob = Test.MultiDimensionalArrayTest()
            ob.items[0, 0] = max_ + 1

        with self.assertRaises(OverflowError):
            ob = Test.MultiDimensionalArrayTest()
            ob.items[0, 0] = min_ - 1

        with self.assertRaises(TypeError):
            ob = Test.MultiDimensionalArrayTest()
            _ = ob.items["wrong", 0]

        with self.assertRaises(TypeError):
            ob = Test.MultiDimensionalArrayTest()
            ob[0, 0] = "wrong"

    def test_array_iteration(self):
        """Test array iteration."""
        items = Test.Int32ArrayTest().items

        for i in items:
            self.assertTrue((i > -1) and (i < 5))

        items = Test.NullArrayTest().items

        for i in items:
            self.assertTrue(i is None)

        empty = Test.NullArrayTest().empty

        for i in empty:
            raise TypeError('iteration over empty array')

    def test_tuple_array_conversion(self):
        """Test conversion of tuples to array arguments."""
        from Python.Test import ArrayConversionTest
        from Python.Test import Spam

        items = []
        for i in range(10):
            items.append(Spam(str(i)))
        items = tuple(items)

        result = ArrayConversionTest.EchoRange(items)
        self.assertTrue(result[0].__class__ == Spam)
        self.assertTrue(len(result) == 10)

    def test_tuple_nested_array_conversion(self):
        """Test conversion of tuples to array-of-array arguments."""
        from Python.Test import ArrayConversionTest
        from Python.Test import Spam

        items = []
        for i in range(10):
            subs = []
            for _ in range(10):
                subs.append(Spam(str(i)))
            items.append(tuple(subs))
        items = tuple(items)

        result = ArrayConversionTest.EchoRangeAA(items)

        self.assertTrue(len(result) == 10)
        self.assertTrue(len(result[0]) == 10)
        self.assertTrue(result[0][0].__class__ == Spam)

    def test_list_array_conversion(self):
        """Test conversion of lists to array arguments."""
        from Python.Test import ArrayConversionTest
        from Python.Test import Spam

        items = []
        for i in range(10):
            items.append(Spam(str(i)))

        result = ArrayConversionTest.EchoRange(items)
        self.assertTrue(result[0].__class__ == Spam)
        self.assertTrue(len(result) == 10)

    def test_list_nested_array_conversion(self):
        """Test conversion of lists to array-of-array arguments."""
        from Python.Test import ArrayConversionTest
        from Python.Test import Spam

        items = []
        for i in range(10):
            subs = []
            for _ in range(10):
                subs.append(Spam(str(i)))
            items.append(subs)

        result = ArrayConversionTest.EchoRangeAA(items)

        self.assertTrue(len(result) == 10)
        self.assertTrue(len(result[0]) == 10)
        self.assertTrue(result[0][0].__class__ == Spam)

    def test_sequence_array_conversion(self):
        """Test conversion of sequence-like obs to array arguments."""
        from Python.Test import ArrayConversionTest
        from Python.Test import Spam

        items = UserList()
        for i in range(10):
            items.append(Spam(str(i)))

        result = ArrayConversionTest.EchoRange(items)
        self.assertTrue(result[0].__class__ == Spam)
        self.assertTrue(len(result) == 10)

    def test_sequence_nested_array_conversion(self):
        """Test conversion of sequences to array-of-array arguments."""
        from Python.Test import ArrayConversionTest
        from Python.Test import Spam

        items = UserList()
        for i in range(10):
            subs = UserList()
            for _ in range(10):
                subs.append(Spam(str(i)))
            items.append(subs)

        result = ArrayConversionTest.EchoRangeAA(items)

        self.assertTrue(len(result) == 10)
        self.assertTrue(len(result[0]) == 10)
        self.assertTrue(result[0][0].__class__ == Spam)

    def test_tuple_array_conversion_type_checking(self):
        """Test error handling for tuple conversion to array arguments."""
        from Python.Test import ArrayConversionTest
        from Python.Test import Spam

        # This should work, because null / None is a valid value in an
        # array of reference types.

        items = []
        for i in range(10):
            items.append(Spam(str(i)))
        items[1] = None
        items = tuple(items)

        result = ArrayConversionTest.EchoRange(items)

        self.assertTrue(result[0].__class__ == Spam)
        self.assertTrue(result[1] is None)
        self.assertTrue(len(result) == 10)

        with self.assertRaises(TypeError):
            temp = list(items)
            temp[1] = 1
            _ = ArrayConversionTest.EchoRange(tuple(temp))

        with self.assertRaises(TypeError):
            temp = list(items)
            temp[1] = "spam"
            _ = ArrayConversionTest.EchoRange(tuple(temp))

    def test_list_array_conversion_type_checking(self):
        """Test error handling for list conversion to array arguments."""
        from Python.Test import ArrayConversionTest
        from Python.Test import Spam

        # This should work, because null / None is a valid value in an
        # array of reference types.

        items = []
        for i in range(10):
            items.append(Spam(str(i)))
        items[1] = None

        result = ArrayConversionTest.EchoRange(items)

        self.assertTrue(result[0].__class__ == Spam)
        self.assertTrue(result[1] is None)
        self.assertTrue(len(result) == 10)

        with self.assertRaises(TypeError):
            items[1] = 1
            _ = ArrayConversionTest.EchoRange(items)

        with self.assertRaises(TypeError):
            items[1] = "spam"
            _ = ArrayConversionTest.EchoRange(items)

    def test_sequence_array_conversion_type_checking(self):
        """Test error handling for sequence conversion to array arguments."""
        from Python.Test import ArrayConversionTest
        from Python.Test import Spam

        # This should work, because null / None is a valid value in an
        # array of reference types.

        items = UserList()
        for i in range(10):
            items.append(Spam(str(i)))
        items[1] = None

        result = ArrayConversionTest.EchoRange(items)

        self.assertTrue(result[0].__class__ == Spam)
        self.assertTrue(result[1] is None)
        self.assertTrue(len(result) == 10)

        with self.assertRaises(TypeError):
            items[1] = 1
            _ = ArrayConversionTest.EchoRange(items)

        with self.assertRaises(TypeError):
            items[1] = "spam"
            _ = ArrayConversionTest.EchoRange(items)

    def test_md_array_conversion(self):
        """Test passing of multi-dimensional array arguments."""
        from Python.Test import ArrayConversionTest
        from Python.Test import Spam
        from System import Array

        # Currently, the runtime does not support automagic conversion of
        # Python sequences to true multi-dimensional arrays (though it
        # does support arrays-of-arrays). This test exists mostly as an
        # example of how a multi-dimensional array can be created and used
        # with managed code from Python.

        items = Array.CreateInstance(Spam, 5, 5)

        for i in range(5):
            for n in range(5):
                items.SetValue(Spam(str((i, n))), (i, n))

        result = ArrayConversionTest.EchoRangeMD(items)

        self.assertTrue(len(result) == 25)
        self.assertTrue(result[0, 0].__class__ == Spam)
        self.assertTrue(result[0, 0].__class__ == Spam)

    def test_boxed_value_type_mutation_result(self):
        """Test behavior of boxed value types."""

        # This test actually exists mostly as documentation of an important
        # concern when dealing with value types. Python does not have any
        # value type semantics that can be mapped to the CLR, so it is easy
        # to accidentally write code like the following which is not really
        # mutating value types in-place but changing boxed copies.

        from System.Drawing import Point
        from System import Array

        items = Array.CreateInstance(Point, 5)

        for i in range(5):
            items[i] = Point(i, i)

        for i in range(5):
            # Boxed items, so set_attr will not change the array member.
            self.assertTrue(items[i].X == i)
            self.assertTrue(items[i].Y == i)
            items[i].X = i + 1
            items[i].Y = i + 1
            self.assertTrue(items[i].X == i)
            self.assertTrue(items[i].Y == i)

        for i in range(5):
            # Demonstrates the workaround that will change the members.
            self.assertTrue(items[i].X == i)
            self.assertTrue(items[i].Y == i)
            item = items[i]
            item.X = i + 1
            item.Y = i + 1
            items[i] = item
            self.assertTrue(items[i].X == i + 1)
            self.assertTrue(items[i].Y == i + 1)

    def test_special_array_creation(self):
        """Test using the Array[<type>] syntax for creating arrays."""
        from Python.Test import ISayHello1, InterfaceTest, ShortEnum
        from System import Array
        inst = InterfaceTest()

        value = Array[System.Boolean]([True, True])
        self.assertTrue(value[0] is True)
        self.assertTrue(value[1] is True)
        self.assertTrue(value.Length == 2)

        value = Array[bool]([True, True])
        self.assertTrue(value[0] is True)
        self.assertTrue(value[1] is True)
        self.assertTrue(value.Length == 2)

        value = Array[System.Byte]([0, 255])
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == 255)
        self.assertTrue(value.Length == 2)

        value = Array[System.SByte]([0, 127])
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == 127)
        self.assertTrue(value.Length == 2)

        value = Array[System.Char]([u'A', u'Z'])
        self.assertTrue(value[0] == u'A')
        self.assertTrue(value[1] == u'Z')
        self.assertTrue(value.Length == 2)

        value = Array[System.Char]([0, 65535])
        self.assertTrue(value[0] == unichr(0))
        self.assertTrue(value[1] == unichr(65535))
        self.assertTrue(value.Length == 2)

        value = Array[System.Int16]([0, 32767])
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == 32767)
        self.assertTrue(value.Length == 2)

        value = Array[System.Int32]([0, 2147483647])
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == 2147483647)
        self.assertTrue(value.Length == 2)

        value = Array[int]([0, 2147483647])
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == 2147483647)
        self.assertTrue(value.Length == 2)

        value = Array[System.Int64]([0, long(9223372036854775807)])
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == long(9223372036854775807))
        self.assertTrue(value.Length == 2)

        # there's no explicit long type in python3, use System.Int64 instead
        if PY2:
            value = Array[long]([0, long(9223372036854775807)])
            self.assertTrue(value[0] == 0)
            self.assertTrue(value[1] == long(9223372036854775807))
            self.assertTrue(value.Length == 2)

        value = Array[System.UInt16]([0, 65000])
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == 65000)
        self.assertTrue(value.Length == 2)

        value = Array[System.UInt32]([0, long(4294967295)])
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == long(4294967295))
        self.assertTrue(value.Length == 2)

        value = Array[System.UInt64]([0, long(18446744073709551615)])
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == long(18446744073709551615))
        self.assertTrue(value.Length == 2)

        value = Array[System.Single]([0.0, 3.402823e38])
        self.assertTrue(value[0] == 0.0)
        self.assertTrue(value[1] == 3.402823e38)
        self.assertTrue(value.Length == 2)

        value = Array[System.Double]([0.0, 1.7976931348623157e308])
        self.assertTrue(value[0] == 0.0)
        self.assertTrue(value[1] == 1.7976931348623157e308)
        self.assertTrue(value.Length == 2)

        value = Array[float]([0.0, 1.7976931348623157e308])
        self.assertTrue(value[0] == 0.0)
        self.assertTrue(value[1] == 1.7976931348623157e308)
        self.assertTrue(value.Length == 2)

        value = Array[System.Decimal]([System.Decimal.Zero, System.Decimal.One])
        self.assertTrue(value[0] == System.Decimal.Zero)
        self.assertTrue(value[1] == System.Decimal.One)
        self.assertTrue(value.Length == 2)

        value = Array[System.String](["one", "two"])
        self.assertTrue(value[0] == "one")
        self.assertTrue(value[1] == "two")
        self.assertTrue(value.Length == 2)

        value = Array[str](["one", "two"])
        self.assertTrue(value[0] == "one")
        self.assertTrue(value[1] == "two")
        self.assertTrue(value.Length == 2)

        value = Array[ShortEnum]([ShortEnum.Zero, ShortEnum.One])
        self.assertTrue(value[0] == ShortEnum.Zero)
        self.assertTrue(value[1] == ShortEnum.One)
        self.assertTrue(value.Length == 2)

        value = Array[System.Object]([inst, inst])
        self.assertTrue(value[0].__class__ == inst.__class__)
        self.assertTrue(value[1].__class__ == inst.__class__)
        self.assertTrue(value.Length == 2)

        value = Array[InterfaceTest]([inst, inst])
        self.assertTrue(value[0].__class__ == inst.__class__)
        self.assertTrue(value[1].__class__ == inst.__class__)
        self.assertTrue(value.Length == 2)

        value = Array[ISayHello1]([inst, inst])
        self.assertTrue(value[0].__class__ == inst.__class__)
        self.assertTrue(value[1].__class__ == inst.__class__)
        self.assertTrue(value.Length == 2)

        inst = System.Exception("badness")
        value = Array[System.Exception]([inst, inst])
        self.assertTrue(value[0].__class__ == inst.__class__)
        self.assertTrue(value[1].__class__ == inst.__class__)
        self.assertTrue(value.Length == 2)

    def test_array_abuse(self):
        """Test array abuse."""
        _class = Test.PublicArrayTest
        ob = Test.PublicArrayTest()

        with self.assertRaises(AttributeError):
            del _class.__getitem__

        with self.assertRaises(AttributeError):
            del ob.__getitem__

        with self.assertRaises(AttributeError):
            del _class.__setitem__

        with self.assertRaises(AttributeError):
            del ob.__setitem__

        with self.assertRaises(TypeError):
            Test.PublicArrayTest.__getitem__(0, 0)

        with self.assertRaises(TypeError):
            Test.PublicArrayTest.__setitem__(0, 0, 0)

        with self.assertRaises(TypeError):
            desc = Test.PublicArrayTest.__dict__['__getitem__']
            desc(0, 0)

        with self.assertRaises(TypeError):
            desc = Test.PublicArrayTest.__dict__['__setitem__']
            desc(0, 0, 0)


def test_suite():
    return unittest.makeSuite(ArrayTests)
