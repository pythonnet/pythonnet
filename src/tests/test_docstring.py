import unittest
import clr

clr.AddReference('Python.Test')

from Python.Test import DocWithCtorTest, DocWithoutCtorTest, DocWithCtorNoDocTest


class DocStringTests(unittest.TestCase):
    """Test doc strings support."""

    def testDocWithCtor(self):
        self.assertEqual(DocWithCtorTest.__doc__, 'DocWithCtorTest Class')
        self.assertEqual(DocWithCtorTest.TestMethod.__doc__, 'DocWithCtorTest TestMethod')
        self.assertEqual(DocWithCtorTest.StaticTestMethod.__doc__, 'DocWithCtorTest StaticTestMethod')

    def testDocWithCtorNoDoc(self):
        self.assertEqual(DocWithCtorNoDocTest.__doc__, 'Void .ctor(Boolean)')
        self.assertEqual(DocWithCtorNoDocTest.TestMethod.__doc__, 'Void TestMethod(Double, Int32)')
        self.assertEqual(DocWithCtorNoDocTest.StaticTestMethod.__doc__, 'Void StaticTestMethod(Double, Int32)')

    def testDocWithoutCtor(self):
        self.assertEqual(DocWithoutCtorTest.__doc__, 'DocWithoutCtorTest Class')
        self.assertEqual(DocWithoutCtorTest.TestMethod.__doc__, 'DocWithoutCtorTest TestMethod')
        self.assertEqual(DocWithoutCtorTest.StaticTestMethod.__doc__, 'DocWithoutCtorTest StaticTestMethod')


def test_suite():
    return unittest.makeSuite(DocStringTests)


def main():
    unittest.TextTestRunner().run(test_suite())


if __name__ == '__main__':
    main()
