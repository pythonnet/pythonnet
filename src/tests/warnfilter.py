"""Warnfilter
"""

from warnings import filterwarnings
from warnings import resetwarnings


def addClrWarnfilter(action="ignore", append=False):
    msgs = ["^The CLR module is deprecated.*", "^Importing from the CLR\.\* namespace.*"]
    for msg in msgs:
        filterwarnings(action, msg, category=DeprecationWarning, append=append)
