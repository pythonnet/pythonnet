# ===========================================================================
# This software is subject to the provisions of the Zope Public License,
# Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
# THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
# WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
# FOR A PARTICULAR PURPOSE.
# ===========================================================================

"""Run all of the unit tests for this package."""

import os
import sys
import unittest
import warnfilter
warnfilter.addClrWarnfilter()

try:
    import System
except ImportError:
    print "Load clr import hook"
    import clr

test_modules = (
        'test_exceptions',
        'test_module',
        'test_compat',    
        'test_generic',
        'test_conversion',
        'test_class',
        'test_interface',
        'test_enum',
        'test_field',
        'test_property',
        'test_indexer',
        'test_event',
        'test_method',
        'test_delegate',
        'test_array',
        'test_thread'
)

def removePyc():
    path = os.path.dirname(os.path.abspath(__file__))
    for name in test_modules:
        pyc = os.path.join(path, "%s.pyc" % name)
        if os.path.isfile(pyc):
            os.unlink(pyc)

def main(verbosity=1):

    removePyc()
    
    suite = unittest.TestSuite()
    
    for name in test_modules:
        module = __import__(name)
        suite.addTests((module.test_suite(),))
        
    unittest.TextTestRunner(verbosity=verbosity).run(suite)

if __name__ == '__main__':
    main(1)
    if '--pause' in sys.argv:
        print "Press enter to continue"
        raw_input()
