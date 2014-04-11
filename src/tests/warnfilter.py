# ===========================================================================
# This software is subject to the provisions of the Zope Public License,
# Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
# THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
# WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
# FOR A PARTICULAR PURPOSE.
# ===========================================================================

"""Warnfilter
"""

from warnings import filterwarnings
from warnings import resetwarnings

def addClrWarnfilter(action="ignore", append=False):
    msgs = ["^The CLR module is deprecated.*", "^Importing from the CLR\.\* namespace.*"]
    for msg in msgs:
        filterwarnings(action, msg, category=DeprecationWarning, append=append)
