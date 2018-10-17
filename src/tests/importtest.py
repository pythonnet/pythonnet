# -*- coding: utf-8 -*-

import sys
try:
    del sys.modules["System.IO"]
except KeyError:
    pass

assert "FileStream" not in globals()
import System.IO
from System.IO import *

assert "FileStream" in globals()
