import sys, os, string, unittest, types
import Python.Test as Test
import System
import six

if six.PY3:
    long = int
    unichr = chr


class ArrayTests(unittest.TestCase):
    """Test support for managed arrays."""

    def testPublicArray(self):
        """Test public arrays."""
        object = Test.PublicArrayTest()
        items = object.items

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

    def testProtectedArray(self):
        """Test protected arrays."""
        object = Test.ProtectedArrayTest()
        items = object.items

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

    def testInternalArray(self):
        """Test internal arrays."""

        def test():
            object = Test.InternalArrayTest()
            items = object.items

        self.assertRaises(AttributeError, test)

    def testPrivateArray(self):
        """Test private arrays."""

        def test():
            object = Test.PrivateArrayTest()
            items = object.items

        self.assertRaises(AttributeError, test)

    def testArrayBoundsChecking(self):
        """Test array bounds checking."""

        object = Test.Int32ArrayTest()
        items = object.items

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

        def test():
            object = Test.Int32ArrayTest()
            object.items[5]

        self.assertRaises(IndexError, test)

        def test():
            object = Test.Int32ArrayTest()
            object.items[5] = 0

        self.assertRaises(IndexError, test)

        def test():
            object = Test.Int32ArrayTest()
            items[-6]

        self.assertRaises(IndexError, test)

        def test():
            object = Test.Int32ArrayTest()
            items[-6] = 0

        self.assertRaises(IndexError, test)

    def testArrayContains(self):
        """Test array support for __contains__."""

        object = Test.Int32ArrayTest()
        items = object.items

        self.assertTrue(0 in items)
        self.assertTrue(1 in items)
        self.assertTrue(2 in items)
        self.assertTrue(3 in items)
        self.assertTrue(4 in items)

        self.assertFalse(5 in items)  # "H:\Python27\Lib\unittest\case.py", line 592, in deprecated_func,
        self.assertFalse(-1 in items)  # TypeError: int() argument must be a string or a number, not 'NoneType'
        self.assertFalse(None in items)  # which threw ^ here which is a little odd.
        # But when run from runtests.py. Not when this module ran by itself.

    def testBooleanArray(self):
        """Test boolean arrays."""
        object = Test.BooleanArrayTest()
        items = object.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == True)
        self.assertTrue(items[1] == False)
        self.assertTrue(items[2] == True)
        self.assertTrue(items[3] == False)
        self.assertTrue(items[4] == True)

        items[0] = False
        self.assertTrue(items[0] == False)

        items[0] = True
        self.assertTrue(items[0] == True)

        def test():
            object = Test.ByteArrayTest()
            v = object.items["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.ByteArrayTest()
            object[0] = "wrong"

        self.assertRaises(TypeError, test)

    def testByteArray(self):
        """Test byte arrays."""
        object = Test.ByteArrayTest()
        items = object.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == 0)
        self.assertTrue(items[4] == 4)

        max = 255
        min = 0

        items[0] = max
        self.assertTrue(items[0] == max)

        items[0] = min
        self.assertTrue(items[0] == min)

        items[-4] = max
        self.assertTrue(items[-4] == max)

        items[-1] = min
        self.assertTrue(items[-1] == min)

        def test():
            object = Test.ByteArrayTest()
            object.items[0] = max + 1

        self.assertRaises(OverflowError, test)

        def test():
            object = Test.ByteArrayTest()
            object.items[0] = min - 1

        self.assertRaises(OverflowError, test)

        def test():
            object = Test.ByteArrayTest()
            v = object.items["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.ByteArrayTest()
            object[0] = "wrong"

        self.assertRaises(TypeError, test)

    def testSByteArray(self):
        """Test sbyte arrays."""
        object = Test.SByteArrayTest()
        items = object.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == 0)
        self.assertTrue(items[4] == 4)

        max = 127
        min = -128

        items[0] = max
        self.assertTrue(items[0] == max)

        items[0] = min
        self.assertTrue(items[0] == min)

        items[-4] = max
        self.assertTrue(items[-4] == max)

        items[-1] = min
        self.assertTrue(items[-1] == min)

        def test():
            object = Test.SByteArrayTest()
            object.items[0] = max + 1

        self.assertRaises(OverflowError, test)

        def test():
            object = Test.SByteArrayTest()
            object.items[0] = min - 1

        self.assertRaises(OverflowError, test)

        def test():
            object = Test.SByteArrayTest()
            v = object.items["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.SByteArrayTest()
            object[0] = "wrong"

        self.assertRaises(TypeError, test)

    def testCharArray(self):
        """Test char arrays."""
        object = Test.CharArrayTest()
        items = object.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == 'a')
        self.assertTrue(items[4] == 'e')

        max = unichr(65535)
        min = unichr(0)

        items[0] = max
        self.assertTrue(items[0] == max)

        items[0] = min
        self.assertTrue(items[0] == min)

        items[-4] = max
        self.assertTrue(items[-4] == max)

        items[-1] = min
        self.assertTrue(items[-1] == min)

        def test():
            object = Test.CharArrayTest()
            v = object.items["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.CharArrayTest()
            object[0] = "wrong"

        self.assertRaises(TypeError, test)

    def testInt16Array(self):
        """Test Int16 arrays."""
        object = Test.Int16ArrayTest()
        items = object.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == 0)
        self.assertTrue(items[4] == 4)

        max = 32767
        min = -32768

        items[0] = max
        self.assertTrue(items[0] == max)

        items[0] = min
        self.assertTrue(items[0] == min)

        items[-4] = max
        self.assertTrue(items[-4] == max)

        items[-1] = min
        self.assertTrue(items[-1] == min)

        def test():
            object = Test.Int16ArrayTest()
            object.items[0] = max + 1

        self.assertRaises(OverflowError, test)

        def test():
            object = Test.Int16ArrayTest()
            object.items[0] = min - 1

        self.assertRaises(OverflowError, test)

        def test():
            object = Test.Int16ArrayTest()
            v = object.items["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.Int16ArrayTest()
            object[0] = "wrong"

        self.assertRaises(TypeError, test)

    def testInt32Array(self):
        """Test Int32 arrays."""
        object = Test.Int32ArrayTest()
        items = object.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == 0)
        self.assertTrue(items[4] == 4)

        max = 2147483647
        min = -2147483648

        items[0] = max
        self.assertTrue(items[0] == max)

        items[0] = min
        self.assertTrue(items[0] == min)

        items[-4] = max
        self.assertTrue(items[-4] == max)

        items[-1] = min
        self.assertTrue(items[-1] == min)

        def test():
            object = Test.Int32ArrayTest()
            object.items[0] = max + 1

        self.assertRaises(OverflowError, test)

        def test():
            object = Test.Int32ArrayTest()
            object.items[0] = min - 1

        self.assertRaises(OverflowError, test)

        def test():
            object = Test.Int32ArrayTest()
            v = object.items["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.Int32ArrayTest()
            object[0] = "wrong"

        self.assertRaises(TypeError, test)

    def testInt64Array(self):
        """Test Int64 arrays."""
        object = Test.Int64ArrayTest()
        items = object.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == 0)
        self.assertTrue(items[4] == 4)

        max = long(9223372036854775807)
        min = long(-9223372036854775808)

        items[0] = max
        self.assertTrue(items[0] == max)

        items[0] = min
        self.assertTrue(items[0] == min)

        items[-4] = max
        self.assertTrue(items[-4] == max)

        items[-1] = min
        self.assertTrue(items[-1] == min)

        def test():
            object = Test.Int64ArrayTest()
            object.items[0] = max + 1

        self.assertRaises(OverflowError, test)

        def test():
            object = Test.Int64ArrayTest()
            object.items[0] = min - 1

        self.assertRaises(OverflowError, test)

        def test():
            object = Test.Int64ArrayTest()
            v = object.items["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.Int64ArrayTest()
            object[0] = "wrong"

        self.assertRaises(TypeError, test)

    def testUInt16Array(self):
        """Test UInt16 arrays."""
        object = Test.UInt16ArrayTest()
        items = object.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == 0)
        self.assertTrue(items[4] == 4)

        max = 65535
        min = 0

        items[0] = max
        self.assertTrue(items[0] == max)

        items[0] = min
        self.assertTrue(items[0] == min)

        items[-4] = max
        self.assertTrue(items[-4] == max)

        items[-1] = min
        self.assertTrue(items[-1] == min)

        def test():
            object = Test.UInt16ArrayTest()
            object.items[0] = max + 1

        self.assertRaises(OverflowError, test)

        def test():
            object = Test.UInt16ArrayTest()
            object.items[0] = min - 1

        self.assertRaises(OverflowError, test)

        def test():
            object = Test.UInt16ArrayTest()
            v = object.items["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.UInt16ArrayTest()
            object[0] = "wrong"

        self.assertRaises(TypeError, test)

    def testUInt32Array(self):
        """Test UInt32 arrays."""
        object = Test.UInt32ArrayTest()
        items = object.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == 0)
        self.assertTrue(items[4] == 4)

        max = long(4294967295)
        min = 0

        items[0] = max
        self.assertTrue(items[0] == max)

        items[0] = min
        self.assertTrue(items[0] == min)

        items[-4] = max
        self.assertTrue(items[-4] == max)

        items[-1] = min
        self.assertTrue(items[-1] == min)

        def test():
            object = Test.UInt32ArrayTest()
            object.items[0] = max + 1

        self.assertRaises(OverflowError, test)

        def test():
            object = Test.UInt32ArrayTest()
            object.items[0] = min - 1

        self.assertRaises(OverflowError, test)

        def test():
            object = Test.UInt32ArrayTest()
            v = object.items["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.UInt32ArrayTest()
            object[0] = "wrong"

        self.assertRaises(TypeError, test)

    def testUInt64Array(self):
        """Test UInt64 arrays."""
        object = Test.UInt64ArrayTest()
        items = object.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == 0)
        self.assertTrue(items[4] == 4)

        max = long(18446744073709551615)
        min = 0

        items[0] = max
        self.assertTrue(items[0] == max)

        items[0] = min
        self.assertTrue(items[0] == min)

        items[-4] = max
        self.assertTrue(items[-4] == max)

        items[-1] = min
        self.assertTrue(items[-1] == min)

        def test():
            object = Test.UInt64ArrayTest()
            object.items[0] = max + 1

        self.assertRaises(OverflowError, test)

        def test():
            object = Test.UInt64ArrayTest()
            object.items[0] = min - 1

        self.assertRaises(OverflowError, test)

        def test():
            object = Test.UInt64ArrayTest()
            v = object.items["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.UInt64ArrayTest()
            object[0] = "wrong"

        self.assertRaises(TypeError, test)

    def testSingleArray(self):
        """Test Single arrays."""
        object = Test.SingleArrayTest()
        items = object.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == 0.0)
        self.assertTrue(items[4] == 4.0)

        max = 3.402823e38
        min = -3.402823e38

        items[0] = max
        self.assertTrue(items[0] == max)

        items[0] = min
        self.assertTrue(items[0] == min)

        items[-4] = max
        self.assertTrue(items[-4] == max)

        items[-1] = min
        self.assertTrue(items[-1] == min)

        def test():
            object = Test.SingleArrayTest()
            v = object.items["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.SingleArrayTest()
            object[0] = "wrong"

        self.assertRaises(TypeError, test)

    def testDoubleArray(self):
        """Test Double arrays."""
        object = Test.DoubleArrayTest()
        items = object.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == 0.0)
        self.assertTrue(items[4] == 4.0)

        max = 1.7976931348623157e308
        min = -1.7976931348623157e308

        items[0] = max
        self.assertTrue(items[0] == max)

        items[0] = min
        self.assertTrue(items[0] == min)

        items[-4] = max
        self.assertTrue(items[-4] == max)

        items[-1] = min
        self.assertTrue(items[-1] == min)

        def test():
            object = Test.DoubleArrayTest()
            v = object.items["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.DoubleArrayTest()
            object[0] = "wrong"

        self.assertRaises(TypeError, test)

    def testDecimalArray(self):
        """Test Decimal arrays."""
        object = Test.DecimalArrayTest()
        items = object.items

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

        def test():
            object = Test.DecimalArrayTest()
            v = object.items["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.DecimalArrayTest()
            object[0] = "wrong"

        self.assertRaises(TypeError, test)

    def testStringArray(self):
        """Test String arrays."""
        object = Test.StringArrayTest()
        items = object.items

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

        def test():
            object = Test.StringArrayTest()
            v = object.items["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.Int64ArrayTest()
            object[0] = 0

        self.assertRaises(TypeError, test)

    def testEnumArray(self):
        """Test enum arrays."""
        from Python.Test import ShortEnum
        object = Test.EnumArrayTest()
        items = object.items

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

        def test():
            object = Test.EnumArrayTest()
            object.items[0] = 99

        self.assertRaises(ValueError, test)

        def test():
            object = Test.EnumArrayTest()
            v = object.items["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.EnumArrayTest()
            object[0] = "wrong"

        self.assertRaises(TypeError, test)

    def testObjectArray(self):
        """Test object arrays."""
        from Python.Test import Spam
        object = Test.ObjectArrayTest()
        items = object.items

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
        self.assertTrue(items[0] == None)

        def test():
            object = Test.ObjectArrayTest()
            v = object.items["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.ObjectArrayTest()
            object.items["wrong"] = "wrong"

        self.assertRaises(TypeError, test)

    def testNullArray(self):
        """Test null arrays."""
        object = Test.NullArrayTest()
        items = object.items

        self.assertTrue(len(items) == 5)

        self.assertTrue(items[0] == None)
        self.assertTrue(items[4] == None)

        items[0] = "spam"
        self.assertTrue(items[0] == "spam")

        items[0] = None
        self.assertTrue(items[0] == None)

        items[-4] = "spam"
        self.assertTrue(items[-4] == "spam")

        items[-1] = None
        self.assertTrue(items[-1] == None)

        empty = object.empty
        self.assertTrue(len(empty) == 0)

        def test():
            object = Test.NullArrayTest()
            v = object.items["wrong"]

        self.assertRaises(TypeError, test)

    def testInterfaceArray(self):
        """Test interface arrays."""
        from Python.Test import Spam
        object = Test.InterfaceArrayTest()
        items = object.items

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
        self.assertTrue(items[0] == None)

        def test():
            object = Test.InterfaceArrayTest()
            object.items[0] = 99

        self.assertRaises(TypeError, test)

        def test():
            object = Test.InterfaceArrayTest()
            v = object.items["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.InterfaceArrayTest()
            object.items["wrong"] = "wrong"

        self.assertRaises(TypeError, test)

    def testTypedArray(self):
        """Test typed arrays."""
        from Python.Test import Spam
        object = Test.TypedArrayTest()
        items = object.items

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
        self.assertTrue(items[0] == None)

        def test():
            object = Test.TypedArrayTest()
            object.items[0] = 99

        self.assertRaises(TypeError, test)

        def test():
            object = Test.TypedArrayTest()
            v = object.items["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.TypedArrayTest()
            object.items["wrong"] = "wrong"

        self.assertRaises(TypeError, test)

    def testMultiDimensionalArray(self):
        """Test multi-dimensional arrays."""
        object = Test.MultiDimensionalArrayTest()
        items = object.items

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

        max = 2147483647
        min = -2147483648

        items[0, 0] = max
        self.assertTrue(items[0, 0] == max)

        items[0, 0] = min
        self.assertTrue(items[0, 0] == min)

        items[-4, 0] = max
        self.assertTrue(items[-4, 0] == max)

        items[-1, -1] = min
        self.assertTrue(items[-1, -1] == min)

        def test():
            object = Test.MultiDimensionalArrayTest()
            object.items[0, 0] = max + 1

        self.assertRaises(OverflowError, test)

        def test():
            object = Test.MultiDimensionalArrayTest()
            object.items[0, 0] = min - 1

        self.assertRaises(OverflowError, test)

        def test():
            object = Test.MultiDimensionalArrayTest()
            v = object.items["wrong", 0]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.MultiDimensionalArrayTest()
            object[0, 0] = "wrong"

        self.assertRaises(TypeError, test)

    def testArrayIteration(self):
        """Test array iteration."""
        items = Test.Int32ArrayTest().items

        for i in items:
            self.assertTrue((i > -1) and (i < 5))

        items = Test.NullArrayTest().items

        for i in items:
            self.assertTrue(i == None)

        empty = Test.NullArrayTest().empty

        for i in empty:
            raise TypeError('iteration over empty array')

    def testTupleArrayConversion(self):
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

    def testTupleNestedArrayConversion(self):
        """Test conversion of tuples to array-of-array arguments."""
        from Python.Test import ArrayConversionTest
        from Python.Test import Spam

        items = []
        for i in range(10):
            subs = []
            for n in range(10):
                subs.append(Spam(str(i)))
            items.append(tuple(subs))
        items = tuple(items)

        result = ArrayConversionTest.EchoRangeAA(items)

        self.assertTrue(len(result) == 10)
        self.assertTrue(len(result[0]) == 10)
        self.assertTrue(result[0][0].__class__ == Spam)

    def testListArrayConversion(self):
        """Test conversion of lists to array arguments."""
        from Python.Test import ArrayConversionTest
        from Python.Test import Spam

        items = []
        for i in range(10):
            items.append(Spam(str(i)))

        result = ArrayConversionTest.EchoRange(items)
        self.assertTrue(result[0].__class__ == Spam)
        self.assertTrue(len(result) == 10)

    def testListNestedArrayConversion(self):
        """Test conversion of lists to array-of-array arguments."""
        from Python.Test import ArrayConversionTest
        from Python.Test import Spam

        items = []
        for i in range(10):
            subs = []
            for n in range(10):
                subs.append(Spam(str(i)))
            items.append(subs)

        result = ArrayConversionTest.EchoRangeAA(items)

        self.assertTrue(len(result) == 10)
        self.assertTrue(len(result[0]) == 10)
        self.assertTrue(result[0][0].__class__ == Spam)

    def testSequenceArrayConversion(self):
        """Test conversion of sequence-like objects to array arguments."""
        from Python.Test import ArrayConversionTest
        from Python.Test import Spam
        if six.PY3:
            from collections import UserList
        else:
            from UserList import UserList

        items = UserList()
        for i in range(10):
            items.append(Spam(str(i)))

        result = ArrayConversionTest.EchoRange(items)
        self.assertTrue(result[0].__class__ == Spam)
        self.assertTrue(len(result) == 10)

    def testSequenceNestedArrayConversion(self):
        """Test conversion of sequences to array-of-array arguments."""
        from Python.Test import ArrayConversionTest
        from Python.Test import Spam
        if six.PY3:
            from collections import UserList
        else:
            from UserList import UserList

        items = UserList()
        for i in range(10):
            subs = UserList()
            for n in range(10):
                subs.append(Spam(str(i)))
            items.append(subs)

        result = ArrayConversionTest.EchoRangeAA(items)

        self.assertTrue(len(result) == 10)
        self.assertTrue(len(result[0]) == 10)
        self.assertTrue(result[0][0].__class__ == Spam)

    def testTupleArrayConversionTypeChecking(self):
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
        self.assertTrue(result[1] == None)
        self.assertTrue(len(result) == 10)

        def test(items=items):
            temp = list(items)
            temp[1] = 1

            result = ArrayConversionTest.EchoRange(tuple(temp))

        self.assertRaises(TypeError, test)

        def test(items=items):
            temp = list(items)
            temp[1] = "spam"

            result = ArrayConversionTest.EchoRange(tuple(temp))

        self.assertRaises(TypeError, test)

    def testListArrayConversionTypeChecking(self):
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
        self.assertTrue(result[1] == None)
        self.assertTrue(len(result) == 10)

        def test(items=items):
            items[1] = 1
            result = ArrayConversionTest.EchoRange(items)

        self.assertRaises(TypeError, test)

        def test(items=items):
            items[1] = "spam"
            result = ArrayConversionTest.EchoRange(items)

        self.assertRaises(TypeError, test)

    def testSequenceArrayConversionTypeChecking(self):
        """Test error handling for sequence conversion to array arguments."""
        from Python.Test import ArrayConversionTest
        from Python.Test import Spam
        if six.PY3:
            from collections import UserList
        else:
            from UserList import UserList

        # This should work, because null / None is a valid value in an
        # array of reference types.

        items = UserList()
        for i in range(10):
            items.append(Spam(str(i)))
        items[1] = None

        result = ArrayConversionTest.EchoRange(items)

        self.assertTrue(result[0].__class__ == Spam)
        self.assertTrue(result[1] == None)
        self.assertTrue(len(result) == 10)

        def test(items=items):
            items[1] = 1
            result = ArrayConversionTest.EchoRange(items)

        self.assertRaises(TypeError, test)

        def test(items=items):
            items[1] = "spam"
            result = ArrayConversionTest.EchoRange(items)

        self.assertRaises(TypeError, test)

    def testMDArrayConversion(self):
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

    def testBoxedValueTypeMutationResult(self):
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
            # Boxed items, so settr will not change the array member.
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

    def testSpecialArrayCreation(self):
        """Test using the Array[<type>] syntax for creating arrays."""
        from Python.Test import ISayHello1, InterfaceTest, ShortEnum
        from System import Array
        inst = InterfaceTest()

        value = Array[System.Boolean]([True, True])
        self.assertTrue(value[0] == True)
        self.assertTrue(value[1] == True)
        self.assertTrue(value.Length == 2)

        value = Array[bool]([True, True])
        self.assertTrue(value[0] == True)
        self.assertTrue(value[1] == True)
        self.assertTrue(value.Length == 2)

        value = Array[System.Byte]([0, 255])
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == 255)
        self.assertTrue(value.Length == 2)

        value = Array[System.SByte]([0, 127])
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == 127)
        self.assertTrue(value.Length == 2)

        value = Array[System.Char]([six.u('A'), six.u('Z')])
        self.assertTrue(value[0] == six.u('A'))
        self.assertTrue(value[1] == six.u('Z'))
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
        if not six.PY3:
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

    def testArrayAbuse(self):
        """Test array abuse."""
        _class = Test.PublicArrayTest
        object = Test.PublicArrayTest()

        def test():
            del _class.__getitem__

        self.assertRaises(AttributeError, test)

        def test():
            del object.__getitem__

        self.assertRaises(AttributeError, test)

        def test():
            del _class.__setitem__

        self.assertRaises(AttributeError, test)

        def test():
            del object.__setitem__

        self.assertRaises(AttributeError, test)

        def test():
            Test.PublicArrayTest.__getitem__(0, 0)

        self.assertRaises(TypeError, test)

        def test():
            Test.PublicArrayTest.__setitem__(0, 0, 0)

        self.assertRaises(TypeError, test)

        def test():
            desc = Test.PublicArrayTest.__dict__['__getitem__']
            desc(0, 0)

        self.assertRaises(TypeError, test)

        def test():
            desc = Test.PublicArrayTest.__dict__['__setitem__']
            desc(0, 0, 0)

        self.assertRaises(TypeError, test)


def test_suite():
    return unittest.makeSuite(ArrayTests)


def main():
    unittest.TextTestRunner().run(test_suite())


if __name__ == '__main__':
    main()
