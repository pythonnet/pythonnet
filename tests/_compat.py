# -*- coding: utf-8 -*-

"""Python 2.7, 3.3+ compatibility module.

Using Python 3 syntax to encourage upgrade unless otherwise noted.
"""

import operator
import subprocess
import sys
import types

PY2 = sys.version_info[0] == 2
PY3 = sys.version_info[0] == 3

if PY3:
    import _thread as thread  # Using PY2 name
    import pickle
    from collections import UserList

    indexbytes = operator.getitem
    input = input

    string_types = str,
    binary_type = bytes
    text_type = str

    DictProxyType = type(object.__dict__)
    ClassType = type

    # No PY3 equivalents, use PY2 name
    long = int
    unichr = chr
    unicode = str

    # from nowhere import Nothing
    cmp = lambda a, b: (a > b) - (a < b)  # No PY3 equivalent
    map = map
    range = range
    zip = zip

elif PY2:
    import thread  # Using PY2 name
    import cPickle as pickle
    from UserList import UserList

    indexbytes = lambda buf, i: ord(buf[i])
    input = raw_input

    string_types = str, unicode
    bytes_type = str
    text_type = unicode

    DictProxyType = types.DictProxyType
    ClassType = types.ClassType

    # No PY3 equivalents, use PY2 name
    long = long
    unichr = unichr
    unicode = unicode

    from itertools import izip, imap
    cmp = cmp
    map = imap
    range = xrange
    zip = izip


def check_output(*args, **kwargs):
    """Check output wrapper for PY2/PY3 compatibility"""
    output = subprocess.check_output(*args, **kwargs)
    if PY2:
        return output
    return output.decode("ascii")
