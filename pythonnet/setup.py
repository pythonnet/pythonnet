#!/usr/bin/env python
# ==========================================================================
# This software is subject to the provisions of the Zope Public License,
# Version 2.1 (ZPL).  A copy of the ZPL should accompany this distribution.
# THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
# WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
# FOR A PARTICULAR PURPOSE.
# ==========================================================================
"""Setup file for Mono clr.so

Author: Christian Heimes <christian(at)cheimes(dot)de>
"""

import os
import sys
from distutils.core import setup
from distutils.core import Extension
import subprocess

VERSION = "%i.%i" % sys.version_info[:2] 

def pkgconfig(*packages, **kw):
    """From http://aspn.activestate.com/ASPN/Cookbook/Python/Recipe/502261
    """
    flag_map = {'-I': 'include_dirs', '-L': 'library_dirs', '-l': 'libraries'}
    cmd = "pkg-config --libs --cflags %s" % ' '.join(packages)
    popen = subprocess.Popen(cmd, shell=True, close_fds=True, stdout=subprocess.PIPE)
    popen.wait()
    if popen.returncode != 0:
        raise RuntimeError("An error has occured")
    output = popen.stdout.read().strip()

    for token in output.split():
        if flag_map.has_key(token[:2]):
            kw.setdefault(flag_map.get(token[:2]), []).append(token[2:])
        else: # throw others to extra_link_args
            kw.setdefault('extra_link_args', []).append(token)

    for k, v in kw.iteritems(): # remove duplicated
        kw[k] = list(set(v))

    return kw

clr = Extension('clr',
    ['src/monoclr/clrmod.c', 'src/monoclr/pynetinit.c'],
    depends=['src/monoclr/pynetclr.h'],
    **pkgconfig('mono')
    )

extensions = []
if os.name == "posix":
    extensions.append(clr)

setup(name="clr",
    ext_modules = extensions,
    scripts = ["src/monoclr/clrpython%s" % VERSION],
    )
