import unittest

class ImportTests(unittest.TestCase):
    """Test the import statement."""

    def testRealtiveMissingImport(self):
        """Test that a relative missing import doesn't crash. Some modules use this to check if a package is installed (realtive import in the site-packages folder"""
        try:
            from . import _missing_import
        except ImportError:
            pass


def test_suite():
    return unittest.makeSuite(ImportTests)

