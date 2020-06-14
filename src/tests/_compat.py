# -*- coding: utf-8 -*-

"""Python 2.7, 3.3+ compatibility module.

Using Python 3 syntax to encourage upgrade unless otherwise noted.
"""

import subprocess
import _thread as thread
DictProxyType = type(object.__dict__)

def check_output(*args, **kwargs):
    """Check output wrapper for PY2/PY3 compatibility"""
    output = subprocess.check_output(*args, **kwargs)
    return output.decode("ascii")
