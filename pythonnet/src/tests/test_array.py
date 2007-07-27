# ===========================================================================
# This software is subject to the provisions of the Zope Public License,
# Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
# THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
# WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
# FOR A PARTICULAR PURPOSE.
# ===========================================================================

import sys, os, string, unittest, types
import Python.Test as Test
import System


class ArrayTests(unittest.TestCase):
    """Test support for managed arrays."""

    def testPublicArray(self):
        """Test public arrays."""
        object = Test.PublicArrayTest()
        items = object.items

        self.failUnless(len(items) == 5)
        
        self.failUnless(items[0] == 0)
        self.failUnless(items[4] == 4)

        items[0] = 8
        self.failUnless(items[0] == 8)

        items[4] = 9
        self.failUnless(items[4] == 9)

        items[-4] = 0
        self.failUnless(items[-4] == 0)

        items[-1] = 4
        self.failUnless(items[-1] == 4)


    def testProtectedArray(self):
        """Test protected arrays."""
        object = Test.ProtectedArrayTest()
        items = object.items

        self.failUnless(len(items) == 5)
        
        self.failUnless(items[0] == 0)
        self.failUnless(items[4] == 4)

        items[0] = 8
        self.failUnless(items[0] == 8)

        items[4] = 9
        self.failUnless(items[4] == 9)

        items[-4] = 0
        self.failUnless(items[-4] == 0)

        items[-1] = 4
        self.failUnless(items[-1] == 4)


    def testInternalArray(self):
        """Test internal arrays."""

        def test():
            object = Test.InternalArrayTest()
            items = object.items

        self.failUnlessRaises(AttributeError, test)


    def testPrivateArray(self):
        """Test private arrays."""

        def test():
            object = Test.PrivateArrayTest()
            items = object.items

        self.failUnlessRaises(AttributeError, test)


    def testArrayBoundsChecking(self):
        """Test array bounds checking."""

        object = Test.Int32ArrayTest()
        items = object.items

        self.failUnless(items[0] == 0)
        self.failUnless(items[1] == 1)
        self.failUnless(items[2] == 2)
        self.failUnless(items[3] == 3)
        self.failUnless(items[4] == 4)

        self.failUnless(items[-5] == 0)
        self.failUnless(items[-4] == 1)
        self.failUnless(items[-3] == 2)
        self.failUnless(items[-2] == 3)
        self.failUnless(items[-1] == 4)

        def test():
            object = Test.Int32ArrayTest()
            object.items[5]

        self.failUnlessRaises(IndexError, test)

        def test():
            object = Test.Int32ArrayTest()
            object.items[5] = 0

        self.failUnlessRaises(IndexError, test)

        def test():
            object = Test.Int32ArrayTest()
            items[-6]

        self.failUnlessRaises(IndexError, test)

        def test():
            object = Test.Int32ArrayTest()
            items[-6] = 0

        self.failUnlessRaises(IndexError, test)


    def testArrayContains(self):
        """Test array support for __contains__."""

        object = Test.Int32ArrayTest()
        items = object.items

        self.failUnless(0 in items)
        self.failUnless(1 in items)
        self.failUnless(2 in items)
        self.failUnless(3 in items)
        self.failUnless(4 in items)

        self.failIf(5 in items)
        self.failIf(-1 in items)
        self.failIf(None in items)


    def testBooleanArray(self):
        """Test boolean arrays."""
        object = Test.BooleanArrayTest()
        items = object.items

        self.failUnless(len(items) == 5)
        
        self.failUnless(items[0] == True)
        self.failUnless(items[1] == False)
        self.failUnless(items[2] == True)
        self.failUnless(items[3] == False)
        self.failUnless(items[4] == True)

        items[0] = False
        self.failUnless(items[0] == False)

        items[0] = True
        self.failUnless(items[0] == True)

        def test():
            object = Test.ByteArrayTest()
            v = object.items["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.ByteArrayTest()
            object[0] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testByteArray(self):
        """Test byte arrays."""
        object = Test.ByteArrayTest()
        items = object.items

        self.failUnless(len(items) == 5)
        
        self.failUnless(items[0] == 0)
        self.failUnless(items[4] == 4)

        max = 255
        min = 0

        items[0] = max
        self.failUnless(items[0] == max)

        items[0] = min
        self.failUnless(items[0] == min)

        items[-4] = max
        self.failUnless(items[-4] == max)

        items[-1] = min
        self.failUnless(items[-1] == min)

        def test():
            object = Test.ByteArrayTest()
            object.items[0] = max + 1

        self.failUnlessRaises(OverflowError, test)

        def test():
            object = Test.ByteArrayTest()
            object.items[0] = min - 1

        self.failUnlessRaises(OverflowError, test)

        def test():
            object = Test.ByteArrayTest()
            v = object.items["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.ByteArrayTest()
            object[0] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testSByteArray(self):
        """Test sbyte arrays."""
        object = Test.SByteArrayTest()
        items = object.items

        self.failUnless(len(items) == 5)
        
        self.failUnless(items[0] == 0)
        self.failUnless(items[4] == 4)

        max = 127
        min = -128

        items[0] = max
        self.failUnless(items[0] == max)

        items[0] = min
        self.failUnless(items[0] == min)

        items[-4] = max
        self.failUnless(items[-4] == max)

        items[-1] = min
        self.failUnless(items[-1] == min)

        def test():
            object = Test.SByteArrayTest()
            object.items[0] = max + 1

        self.failUnlessRaises(OverflowError, test)

        def test():
            object = Test.SByteArrayTest()
            object.items[0] = min - 1

        self.failUnlessRaises(OverflowError, test)

        def test():
            object = Test.SByteArrayTest()
            v = object.items["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.SByteArrayTest()
            object[0] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testCharArray(self):
        """Test char arrays."""
        object = Test.CharArrayTest()
        items = object.items

        self.failUnless(len(items) == 5)
        
        self.failUnless(items[0] == 'a')
        self.failUnless(items[4] == 'e')

        max = unichr(65535)
        min = unichr(0)

        items[0] = max
        self.failUnless(items[0] == max)

        items[0] = min
        self.failUnless(items[0] == min)

        items[-4] = max
        self.failUnless(items[-4] == max)

        items[-1] = min
        self.failUnless(items[-1] == min)

        def test():
            object = Test.CharArrayTest()
            v = object.items["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.CharArrayTest()
            object[0] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testInt16Array(self):
        """Test Int16 arrays."""
        object = Test.Int16ArrayTest()
        items = object.items

        self.failUnless(len(items) == 5)
        
        self.failUnless(items[0] == 0)
        self.failUnless(items[4] == 4)

        max = 32767
        min = -32768

        items[0] = max
        self.failUnless(items[0] == max)

        items[0] = min
        self.failUnless(items[0] == min)

        items[-4] = max
        self.failUnless(items[-4] == max)

        items[-1] = min
        self.failUnless(items[-1] == min)

        def test():
            object = Test.Int16ArrayTest()
            object.items[0] = max + 1

        self.failUnlessRaises(OverflowError, test)

        def test():
            object = Test.Int16ArrayTest()
            object.items[0] = min - 1

        self.failUnlessRaises(OverflowError, test)

        def test():
            object = Test.Int16ArrayTest()
            v = object.items["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.Int16ArrayTest()
            object[0] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testInt32Array(self):
        """Test Int32 arrays."""
        object = Test.Int32ArrayTest()
        items = object.items

        self.failUnless(len(items) == 5)
        
        self.failUnless(items[0] == 0)
        self.failUnless(items[4] == 4)

        max = 2147483647
        min = -2147483648

        items[0] = max
        self.failUnless(items[0] == max)

        items[0] = min
        self.failUnless(items[0] == min)

        items[-4] = max
        self.failUnless(items[-4] == max)

        items[-1] = min
        self.failUnless(items[-1] == min)

        def test():
            object = Test.Int32ArrayTest()
            object.items[0] = max + 1

        self.failUnlessRaises(OverflowError, test)

        def test():
            object = Test.Int32ArrayTest()
            object.items[0] = min - 1

        self.failUnlessRaises(OverflowError, test)

        def test():
            object = Test.Int32ArrayTest()
            v = object.items["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.Int32ArrayTest()
            object[0] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testInt64Array(self):
        """Test Int64 arrays."""
        object = Test.Int64ArrayTest()
        items = object.items

        self.failUnless(len(items) == 5)
        
        self.failUnless(items[0] == 0)
        self.failUnless(items[4] == 4)

        max = 9223372036854775807L
        min = -9223372036854775808L

        items[0] = max
        self.failUnless(items[0] == max)

        items[0] = min
        self.failUnless(items[0] == min)

        items[-4] = max
        self.failUnless(items[-4] == max)

        items[-1] = min
        self.failUnless(items[-1] == min)

        def test():
            object = Test.Int64ArrayTest()
            object.items[0] = max + 1

        self.failUnlessRaises(OverflowError, test)

        def test():
            object = Test.Int64ArrayTest()
            object.items[0] = min - 1

        self.failUnlessRaises(OverflowError, test)

        def test():
            object = Test.Int64ArrayTest()
            v = object.items["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.Int64ArrayTest()
            object[0] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testUInt16Array(self):
        """Test UInt16 arrays."""
        object = Test.UInt16ArrayTest()
        items = object.items

        self.failUnless(len(items) == 5)
        
        self.failUnless(items[0] == 0)
        self.failUnless(items[4] == 4)

        max = 65535
        min = 0

        items[0] = max
        self.failUnless(items[0] == max)

        items[0] = min
        self.failUnless(items[0] == min)

        items[-4] = max
        self.failUnless(items[-4] == max)

        items[-1] = min
        self.failUnless(items[-1] == min)

        def test():
            object = Test.UInt16ArrayTest()
            object.items[0] = max + 1

        self.failUnlessRaises(OverflowError, test)

        def test():
            object = Test.UInt16ArrayTest()
            object.items[0] = min - 1

        self.failUnlessRaises(OverflowError, test)

        def test():
            object = Test.UInt16ArrayTest()
            v = object.items["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.UInt16ArrayTest()
            object[0] = "wrong"

        self.failUnlessRaises(TypeError, test)
        

    def testUInt32Array(self):
        """Test UInt32 arrays."""
        object = Test.UInt32ArrayTest()
        items = object.items

        self.failUnless(len(items) == 5)
        
        self.failUnless(items[0] == 0)
        self.failUnless(items[4] == 4)

        max = 4294967295L
        min = 0

        items[0] = max
        self.failUnless(items[0] == max)

        items[0] = min
        self.failUnless(items[0] == min)

        items[-4] = max
        self.failUnless(items[-4] == max)

        items[-1] = min
        self.failUnless(items[-1] == min)

        def test():
            object = Test.UInt32ArrayTest()
            object.items[0] = max + 1

        self.failUnlessRaises(OverflowError, test)

        def test():
            object = Test.UInt32ArrayTest()
            object.items[0] = min - 1

        self.failUnlessRaises(OverflowError, test)

        def test():
            object = Test.UInt32ArrayTest()
            v = object.items["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.UInt32ArrayTest()
            object[0] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testUInt64Array(self):
        """Test UInt64 arrays."""
        object = Test.UInt64ArrayTest()
        items = object.items

        self.failUnless(len(items) == 5)
        
        self.failUnless(items[0] == 0)
        self.failUnless(items[4] == 4)

        max = 18446744073709551615L
        min = 0

        items[0] = max
        self.failUnless(items[0] == max)

        items[0] = min
        self.failUnless(items[0] == min)

        items[-4] = max
        self.failUnless(items[-4] == max)

        items[-1] = min
        self.failUnless(items[-1] == min)

        def test():
            object = Test.UInt64ArrayTest()
            object.items[0] = max + 1

        self.failUnlessRaises(OverflowError, test)

        def test():
            object = Test.UInt64ArrayTest()
            object.items[0] = min - 1

        self.failUnlessRaises(OverflowError, test)

        def test():
            object = Test.UInt64ArrayTest()
            v = object.items["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.UInt64ArrayTest()
            object[0] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testSingleArray(self):
        """Test Single arrays."""
        object = Test.SingleArrayTest()
        items = object.items

        self.failUnless(len(items) == 5)
        
        self.failUnless(items[0] == 0.0)
        self.failUnless(items[4] == 4.0)

        max = 3.402823e38
        min = -3.402823e38

        items[0] = max
        self.failUnless(items[0] == max)

        items[0] = min
        self.failUnless(items[0] == min)

        items[-4] = max
        self.failUnless(items[-4] == max)

        items[-1] = min
        self.failUnless(items[-1] == min)

        def test():
            object = Test.SingleArrayTest()
            v = object.items["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.SingleArrayTest()
            object[0] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testDoubleArray(self):
        """Test Double arrays."""
        object = Test.DoubleArrayTest()
        items = object.items

        self.failUnless(len(items) == 5)
        
        self.failUnless(items[0] == 0.0)
        self.failUnless(items[4] == 4.0)

        max = 1.7976931348623157e308
        min = -1.7976931348623157e308

        items[0] = max
        self.failUnless(items[0] == max)

        items[0] = min
        self.failUnless(items[0] == min)

        items[-4] = max
        self.failUnless(items[-4] == max)

        items[-1] = min
        self.failUnless(items[-1] == min)

        def test():
            object = Test.DoubleArrayTest()
            v = object.items["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.DoubleArrayTest()
            object[0] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testDecimalArray(self):
        """Test Decimal arrays."""
        object = Test.DecimalArrayTest()
        items = object.items

        from System import Decimal
        max_d = Decimal.Parse("79228162514264337593543950335")
        min_d = Decimal.Parse("-79228162514264337593543950335")

        self.failUnless(len(items) == 5)
        
        self.failUnless(items[0] == Decimal(0))
        self.failUnless(items[4] == Decimal(4))

        items[0] = max_d
        self.failUnless(items[0] == max_d)

        items[0] = min_d
        self.failUnless(items[0] == min_d)

        items[-4] = max_d
        self.failUnless(items[-4] == max_d)

        items[-1] = min_d
        self.failUnless(items[-1] == min_d)

        def test():
            object = Test.DecimalArrayTest()
            v = object.items["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.DecimalArrayTest()
            object[0] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testStringArray(self):
        """Test String arrays."""
        object = Test.StringArrayTest()
        items = object.items

        self.failUnless(len(items) == 5)
        
        self.failUnless(items[0] == '0')
        self.failUnless(items[4] == '4')

        items[0] = "spam"
        self.failUnless(items[0] == "spam")

        items[0] = "eggs"
        self.failUnless(items[0] == "eggs")

        items[-4] = "spam"
        self.failUnless(items[-4] == "spam")

        items[-1] = "eggs"
        self.failUnless(items[-1] == "eggs")

        def test():
            object = Test.StringArrayTest()
            v = object.items["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.Int64ArrayTest()
            object[0] = 0

        self.failUnlessRaises(TypeError, test)


    def testEnumArray(self):
        """Test enum arrays."""
        from Python.Test import ShortEnum
        object = Test.EnumArrayTest()
        items = object.items

        self.failUnless(len(items) == 5)
        
        self.failUnless(items[0] == ShortEnum.Zero)
        self.failUnless(items[4] == ShortEnum.Four)

        items[0] = ShortEnum.Four
        self.failUnless(items[0] == ShortEnum.Four)

        items[0] = ShortEnum.Zero
        self.failUnless(items[0] == ShortEnum.Zero)

        items[-4] = ShortEnum.Four
        self.failUnless(items[-4] == ShortEnum.Four)

        items[-1] = ShortEnum.Zero
        self.failUnless(items[-1] == ShortEnum.Zero)

        def test():
            object = Test.EnumArrayTest()
            object.items[0] = 99

        self.failUnlessRaises(ValueError, test)

        def test():
            object = Test.EnumArrayTest()
            v = object.items["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.EnumArrayTest()
            object[0] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testObjectArray(self):
        """Test object arrays."""
        from Python.Test import Spam
        object = Test.ObjectArrayTest()
        items = object.items

        self.failUnless(len(items) == 5)
        
        self.failUnless(items[0].GetValue() == "0")
        self.failUnless(items[4].GetValue() == "4")

        items[0] = Spam("4")
        self.failUnless(items[0].GetValue() == "4")

        items[0] = Spam("0")
        self.failUnless(items[0].GetValue() == "0")

        items[-4] = Spam("4")
        self.failUnless(items[-4].GetValue() == "4")

        items[-1] = Spam("0")
        self.failUnless(items[-1].GetValue() == "0")

        items[0] = 99
        self.failUnless(items[0] == 99)

        items[0] = None
        self.failUnless(items[0] == None)

        def test():
            object = Test.ObjectArrayTest()
            v = object.items["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.ObjectArrayTest()
            object.items["wrong"] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testNullArray(self):
        """Test null arrays."""
        object = Test.NullArrayTest()
        items = object.items

        self.failUnless(len(items) == 5)
        
        self.failUnless(items[0] == None)
        self.failUnless(items[4] == None)

        items[0] = "spam"
        self.failUnless(items[0] == "spam")

        items[0] = None
        self.failUnless(items[0] == None)

        items[-4] = "spam"
        self.failUnless(items[-4] == "spam")

        items[-1] = None
        self.failUnless(items[-1] == None)

        empty = object.empty
        self.failUnless(len(empty) == 0)

        def test():
            object = Test.NullArrayTest()
            v = object.items["wrong"]

        self.failUnlessRaises(TypeError, test)


    def testInterfaceArray(self):
        """Test interface arrays."""
        from Python.Test import Spam
        object = Test.InterfaceArrayTest()
        items = object.items

        self.failUnless(len(items) == 5)
        
        self.failUnless(items[0].GetValue() == "0")
        self.failUnless(items[4].GetValue() == "4")

        items[0] = Spam("4")
        self.failUnless(items[0].GetValue() == "4")

        items[0] = Spam("0")
        self.failUnless(items[0].GetValue() == "0")

        items[-4] = Spam("4")
        self.failUnless(items[-4].GetValue() == "4")

        items[-1] = Spam("0")
        self.failUnless(items[-1].GetValue() == "0")

        items[0] = None
        self.failUnless(items[0] == None)

        def test():
            object = Test.InterfaceArrayTest()
            object.items[0] = 99

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.InterfaceArrayTest()
            v = object.items["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.InterfaceArrayTest()
            object.items["wrong"] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testTypedArray(self):
        """Test typed arrays."""
        from Python.Test import Spam
        object = Test.TypedArrayTest()
        items = object.items

        self.failUnless(len(items) == 5)
        
        self.failUnless(items[0].GetValue() == "0")
        self.failUnless(items[4].GetValue() == "4")

        items[0] = Spam("4")
        self.failUnless(items[0].GetValue() == "4")

        items[0] = Spam("0")
        self.failUnless(items[0].GetValue() == "0")

        items[-4] = Spam("4")
        self.failUnless(items[-4].GetValue() == "4")

        items[-1] = Spam("0")
        self.failUnless(items[-1].GetValue() == "0")

        items[0] = None
        self.failUnless(items[0] == None)

        def test():
            object = Test.TypedArrayTest()
            object.items[0] = 99

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.TypedArrayTest()
            v = object.items["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.TypedArrayTest()
            object.items["wrong"] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testMultiDimensionalArray(self):
        """Test multi-dimensional arrays."""
        object = Test.MultiDimensionalArrayTest()
        items = object.items

        self.failUnless(len(items) == 25)
        
        self.failUnless(items[0, 0] == 0)
        self.failUnless(items[0, 1] == 1)
        self.failUnless(items[0, 2] == 2)
        self.failUnless(items[0, 3] == 3)
        self.failUnless(items[0, 4] == 4)
        self.failUnless(items[1, 0] == 5)
        self.failUnless(items[1, 1] == 6)
        self.failUnless(items[1, 2] == 7)
        self.failUnless(items[1, 3] == 8)
        self.failUnless(items[1, 4] == 9)
        self.failUnless(items[2, 0] == 10)
        self.failUnless(items[2, 1] == 11)
        self.failUnless(items[2, 2] == 12)
        self.failUnless(items[2, 3] == 13)
        self.failUnless(items[2, 4] == 14)
        self.failUnless(items[3, 0] == 15)
        self.failUnless(items[3, 1] == 16)
        self.failUnless(items[3, 2] == 17)
        self.failUnless(items[3, 3] == 18)
        self.failUnless(items[3, 4] == 19)
        self.failUnless(items[4, 0] == 20)
        self.failUnless(items[4, 1] == 21)
        self.failUnless(items[4, 2] == 22)
        self.failUnless(items[4, 3] == 23)
        self.failUnless(items[4, 4] == 24)
        
        max = 2147483647
        min = -2147483648

        items[0, 0] = max
        self.failUnless(items[0, 0] == max)

        items[0, 0] = min
        self.failUnless(items[0, 0] == min)

        items[-4, 0] = max
        self.failUnless(items[-4, 0] == max)

        items[-1, -1] = min
        self.failUnless(items[-1, -1] == min)

        def test():
            object = Test.MultiDimensionalArrayTest()
            object.items[0, 0] = max + 1

        self.failUnlessRaises(OverflowError, test)

        def test():
            object = Test.MultiDimensionalArrayTest()
            object.items[0, 0] = min - 1

        self.failUnlessRaises(OverflowError, test)

        def test():
            object = Test.MultiDimensionalArrayTest()
            v = object.items["wrong", 0]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.MultiDimensionalArrayTest()
            object[0, 0] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testArrayIteration(self):
        """Test array iteration."""
        items = Test.Int32ArrayTest().items

        for i in items:
            self.failUnless((i > -1) and (i < 5))

        items = Test.NullArrayTest().items

        for i in items:
            self.failUnless(i == None)

        empty = Test.NullArrayTest().empty

        for i in empty:
            raise TypeError, 'iteration over empty array'


    def testTupleArrayConversion(self):
        """Test conversion of tuples to array arguments."""
        from Python.Test import ArrayConversionTest
        from Python.Test import Spam

        items = []
        for i in range(10):
            items.append(Spam(str(i)))
        items = tuple(items)

        result = ArrayConversionTest.EchoRange(items)
        self.failUnless(result[0].__class__ == Spam)
        self.failUnless(len(result) == 10)


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
        
        self.failUnless(len(result) == 10)
        self.failUnless(len(result[0]) == 10)
        self.failUnless(result[0][0].__class__ == Spam)


    def testListArrayConversion(self):
        """Test conversion of lists to array arguments."""
        from Python.Test import ArrayConversionTest
        from Python.Test import Spam

        items = []
        for i in range(10):
            items.append(Spam(str(i)))

        result = ArrayConversionTest.EchoRange(items)
        self.failUnless(result[0].__class__ == Spam)
        self.failUnless(len(result) == 10)


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
        
        self.failUnless(len(result) == 10)
        self.failUnless(len(result[0]) == 10)
        self.failUnless(result[0][0].__class__ == Spam)


    def testSequenceArrayConversion(self):
        """Test conversion of sequence-like objects to array arguments."""
        from Python.Test import ArrayConversionTest
        from Python.Test import Spam
        from UserList import UserList

        items = UserList()
        for i in range(10):
            items.append(Spam(str(i)))

        result = ArrayConversionTest.EchoRange(items)
        self.failUnless(result[0].__class__ == Spam)
        self.failUnless(len(result) == 10)


    def testSequenceNestedArrayConversion(self):
        """Test conversion of sequences to array-of-array arguments."""
        from Python.Test import ArrayConversionTest
        from Python.Test import Spam
        from UserList import UserList

        items = UserList()
        for i in range(10):
            subs = UserList()
            for n in range(10):
                subs.append(Spam(str(i)))
            items.append(subs)
        
        result = ArrayConversionTest.EchoRangeAA(items)
        
        self.failUnless(len(result) == 10)
        self.failUnless(len(result[0]) == 10)
        self.failUnless(result[0][0].__class__ == Spam)


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

        self.failUnless(result[0].__class__ == Spam)
        self.failUnless(result[1] == None)
        self.failUnless(len(result) == 10)

        def test(items = items):
            temp = list(items)
            temp[1] = 1

            result = ArrayConversionTest.EchoRange(tuple(temp))

        self.failUnlessRaises(TypeError, test) 

        def test(items = items):
            temp = list(items)
            temp[1] = "spam"

            result = ArrayConversionTest.EchoRange(tuple(temp))

        self.failUnlessRaises(TypeError, test) 


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

        self.failUnless(result[0].__class__ == Spam)
        self.failUnless(result[1] == None)
        self.failUnless(len(result) == 10)

        def test(items = items):
            items[1] = 1
            result = ArrayConversionTest.EchoRange(items)

        self.failUnlessRaises(TypeError, test) 

        def test(items = items):
            items[1] = "spam"
            result = ArrayConversionTest.EchoRange(items)

        self.failUnlessRaises(TypeError, test) 


    def testSequenceArrayConversionTypeChecking(self):
        """Test error handling for sequence conversion to array arguments."""
        from Python.Test import ArrayConversionTest
        from Python.Test import Spam
        from UserList import UserList

        # This should work, because null / None is a valid value in an
        # array of reference types.

        items = UserList()
        for i in range(10):
            items.append(Spam(str(i)))
        items[1] = None

        result = ArrayConversionTest.EchoRange(items)

        self.failUnless(result[0].__class__ == Spam)
        self.failUnless(result[1] == None)
        self.failUnless(len(result) == 10)

        def test(items = items):
            items[1] = 1
            result = ArrayConversionTest.EchoRange(items)

        self.failUnlessRaises(TypeError, test) 

        def test(items = items):
            items[1] = "spam"
            result = ArrayConversionTest.EchoRange(items)

        self.failUnlessRaises(TypeError, test) 


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
        
        self.failUnless(len(result) == 25)
        self.failUnless(result[0, 0].__class__ == Spam)
        self.failUnless(result[0, 0].__class__ == Spam)


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
            self.failUnless(items[i].X == i)
            self.failUnless(items[i].Y == i)
            items[i].X = i + 1
            items[i].Y = i + 1
            self.failUnless(items[i].X == i)
            self.failUnless(items[i].Y == i)

        for i in range(5):
            # Demonstrates the workaround that will change the members.
            self.failUnless(items[i].X == i)
            self.failUnless(items[i].Y == i)
            item = items[i]
            item.X = i + 1
            item.Y = i + 1
            items[i] = item
            self.failUnless(items[i].X == i + 1)
            self.failUnless(items[i].Y == i + 1)


    def testSpecialArrayCreation(self):
        """Test using the Array[<type>] syntax for creating arrays."""
        from Python.Test import ISayHello1, InterfaceTest, ShortEnum        
        from System import Array
        inst = InterfaceTest()

        value = Array[System.Boolean]([True, True])
        self.failUnless(value[0] == True)
        self.failUnless(value[1] == True)
        self.failUnless(value.Length == 2)

        value = Array[bool]([True, True])
        self.failUnless(value[0] == True)
        self.failUnless(value[1] == True)        
        self.failUnless(value.Length == 2)
        
        value = Array[System.Byte]([0, 255])
        self.failUnless(value[0] == 0)
        self.failUnless(value[1] == 255)        
        self.failUnless(value.Length == 2)
        
        value = Array[System.SByte]([0, 127])
        self.failUnless(value[0] == 0)
        self.failUnless(value[1] == 127)        
        self.failUnless(value.Length == 2)
        
        value = Array[System.Char]([u'A', u'Z'])
        self.failUnless(value[0] == u'A')
        self.failUnless(value[1] == u'Z')
        self.failUnless(value.Length == 2)
        
        value = Array[System.Char]([0, 65535])
        self.failUnless(value[0] == unichr(0))
        self.failUnless(value[1] == unichr(65535))        
        self.failUnless(value.Length == 2)
        
        value = Array[System.Int16]([0, 32767])
        self.failUnless(value[0] == 0)
        self.failUnless(value[1] == 32767)        
        self.failUnless(value.Length == 2)
        
        value = Array[System.Int32]([0, 2147483647])
        self.failUnless(value[0] == 0)
        self.failUnless(value[1] == 2147483647)        
        self.failUnless(value.Length == 2)
        
        value = Array[int]([0, 2147483647])
        self.failUnless(value[0] == 0)
        self.failUnless(value[1] == 2147483647)        
        self.failUnless(value.Length == 2)
        
        value = Array[System.Int64]([0, 9223372036854775807L])
        self.failUnless(value[0] == 0)
        self.failUnless(value[1] == 9223372036854775807L)        
        self.failUnless(value.Length == 2)
        
        value = Array[long]([0, 9223372036854775807L])
        self.failUnless(value[0] == 0)
        self.failUnless(value[1] == 9223372036854775807L)        
        self.failUnless(value.Length == 2)
        
        value = Array[System.UInt16]([0, 65000])
        self.failUnless(value[0] == 0)
        self.failUnless(value[1] == 65000)        
        self.failUnless(value.Length == 2)
        
        value = Array[System.UInt32]([0, 4294967295L])
        self.failUnless(value[0] == 0)
        self.failUnless(value[1] == 4294967295L)        
        self.failUnless(value.Length == 2)
        
        value = Array[System.UInt64]([0, 18446744073709551615L])
        self.failUnless(value[0] == 0)
        self.failUnless(value[1] == 18446744073709551615L)        
        self.failUnless(value.Length == 2)
        
        value = Array[System.Single]([0.0, 3.402823e38])
        self.failUnless(value[0] == 0.0)
        self.failUnless(value[1] == 3.402823e38)        
        self.failUnless(value.Length == 2)
        
        value = Array[System.Double]([0.0, 1.7976931348623157e308])
        self.failUnless(value[0] == 0.0)
        self.failUnless(value[1] == 1.7976931348623157e308)        
        self.failUnless(value.Length == 2)
        
        value = Array[float]([0.0, 1.7976931348623157e308])
        self.failUnless(value[0] == 0.0)
        self.failUnless(value[1] == 1.7976931348623157e308)        
        self.failUnless(value.Length == 2)
        
        value = Array[System.Decimal]([System.Decimal.Zero,System.Decimal.One])
        self.failUnless(value[0] == System.Decimal.Zero)
        self.failUnless(value[1] == System.Decimal.One)        
        self.failUnless(value.Length == 2)
        
        value = Array[System.String](["one", "two"])
        self.failUnless(value[0] == "one")
        self.failUnless(value[1] == "two")
        self.failUnless(value.Length == 2)
        
        value = Array[str](["one", "two"])
        self.failUnless(value[0] == "one")
        self.failUnless(value[1] == "two")
        self.failUnless(value.Length == 2)
        
        value = Array[ShortEnum]([ShortEnum.Zero, ShortEnum.One])
        self.failUnless(value[0] == ShortEnum.Zero)
        self.failUnless(value[1] == ShortEnum.One)
        self.failUnless(value.Length == 2)
        
        value = Array[System.Object]([inst, inst])
        self.failUnless(value[0].__class__ == inst.__class__)
        self.failUnless(value[1].__class__ == inst.__class__)        
        self.failUnless(value.Length == 2)
        
        value = Array[InterfaceTest]([inst, inst])
        self.failUnless(value[0].__class__ == inst.__class__)
        self.failUnless(value[1].__class__ == inst.__class__)        
        self.failUnless(value.Length == 2)
        
        value = Array[ISayHello1]([inst, inst])
        self.failUnless(value[0].__class__ == inst.__class__)
        self.failUnless(value[1].__class__ == inst.__class__)        
        self.failUnless(value.Length == 2)

        inst = System.Exception("badness")
        value = Array[System.Exception]([inst, inst])
        self.failUnless(value[0].__class__ == inst.__class__)
        self.failUnless(value[1].__class__ == inst.__class__)        
        self.failUnless(value.Length == 2)


    def testArrayAbuse(self):
        """Test array abuse."""
        _class = Test.PublicArrayTest
        object = Test.PublicArrayTest()

        def test():
            del _class.__getitem__

        self.failUnlessRaises(AttributeError, test)

        def test():
            del object.__getitem__

        self.failUnlessRaises(AttributeError, test)

        def test():
            del _class.__setitem__

        self.failUnlessRaises(AttributeError, test)

        def test():
            del object.__setitem__

        self.failUnlessRaises(AttributeError, test)

        def test():
            Test.PublicArrayTest.__getitem__(0, 0)

        self.failUnlessRaises(TypeError, test)

        def test():
            Test.PublicArrayTest.__setitem__(0, 0, 0)

        self.failUnlessRaises(TypeError, test)

        def test():
            desc = Test.PublicArrayTest.__dict__['__getitem__']
            desc(0, 0)

        self.failUnlessRaises(TypeError, test)

        def test():
            desc = Test.PublicArrayTest.__dict__['__setitem__']
            desc(0, 0, 0)

        self.failUnlessRaises(TypeError, test)



def test_suite():
    return unittest.makeSuite(ArrayTests)

def main():
    unittest.TextTestRunner().run(test_suite())

if __name__ == '__main__':
    main()

