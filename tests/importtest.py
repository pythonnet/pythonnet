# -*- coding: utf-8 -*-

import sys

try:
    del sys.modules["System.IO"]
except KeyError:
    pass

assert "FileStream" not in globals()
from System.IO import *  # noqa

assert "FileStream" in globals()
