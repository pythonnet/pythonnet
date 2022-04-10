# -*- coding: utf-8 -*-

"""Test conversions using codecs from client python code"""
import clr
import System
import pytest
import Python.Runtime
from Python.Test import ListConversionTester, ListMember

class int_iterable():
    def __init__(self):
        self.counter = 0
    def __iter__(self):
        return self
    def __next__(self):
        if self.counter == 3:
            raise StopIteration
        self.counter = self.counter + 1
        return self.counter

class obj_iterable():
    def __init__(self):
        self.counter = 0
    def __iter__(self):
        return self
    def __next__(self):
        if self.counter == 3:
            raise StopIteration
        self.counter = self.counter + 1
        return ListMember(self.counter, "Number " + str(self.counter))

def test_iterable():
    """Test that a python iterable can be passed into a function that takes an IEnumerable<object>"""

    #Python.Runtime.Codecs.ListDecoder.Register()
    #Python.Runtime.Codecs.SequenceDecoder.Register()
    Python.Runtime.Codecs.IterableDecoder.Register()
    ob = ListConversionTester()

    iterable = int_iterable()
    assert 3 == ob.GetLength(iterable)

    iterable2 = obj_iterable()
    assert 3 == ob.GetLength2(iterable2)

    Python.Runtime.PyObjectConversions.Reset()

def test_sequence():
    Python.Runtime.Codecs.SequenceDecoder.Register()
    ob = ListConversionTester()

    tup = (1,2,3)
    assert 3 == ob.GetLength(tup)

    tup2 = (ListMember(1, "one"), ListMember(2, "two"), ListMember(3, "three"))
    assert 3 == ob.GetLength(tup2)

    Python.Runtime.PyObjectConversions.Reset()

def test_list():
    Python.Runtime.Codecs.SequenceDecoder.Register()
    ob = ListConversionTester()

    l = [1,2,3]
    assert 3 == ob.GetLength(l)

    l2 = [ListMember(1, "one"), ListMember(2, "two"), ListMember(3, "three")]
    assert 3 == ob.GetLength(l2)

    Python.Runtime.PyObjectConversions.Reset()
