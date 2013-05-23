import sys
from setuptools import setup

if sys.version_info[0] >= 3:
    import imp
    setupfile = imp.load_source('setupfile', 'setupwin.py')
else:
    execfile('setupwin.py')
