# -*- coding: utf-8 -*-

"""Helper script to test argv.
Ensures that argv isn't modified after importing clr.
For more details see GH#404 - argv not found"""

from __future__ import print_function

import sys
import clr

print(sys.argv)
