# -*- coding: utf-8 -*-

import unittest
import sys

class SysArgvTests(unittest.TestCase):
    """Test sys.argv state."""

    def test_sys_argv_state(self):
        """Test sys.argv state doesn't change after clr import."""
        argv = sys.argv
        import clr
        self.assertTrue(argv == sys.argv)


def test_suite():
    return unittest.makeSuite(SysArgvTests)
