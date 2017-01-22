# -*- coding: utf-8 -*-

import types
import unittest

from _compat import ClassType, PY2, PY3, range
from utils import isCLRClass, isCLRModule, isCLRRootModule


class CompatibilityTests(unittest.TestCase):
    """Backward-compatibility tests for deprecated features."""

    # Tests for old-style CLR-prefixed module naming.

    def test_simple_import(self):
        """Test simple import."""
        import CLR
        self.assertTrue(isCLRRootModule(CLR))
        self.assertTrue(CLR.__name__ == 'clr')

        import sys
        self.assertTrue(type(sys) == types.ModuleType)
        self.assertTrue(sys.__name__ == 'sys')

        if PY3:
            import http.client
            self.assertTrue(type(http.client) == types.ModuleType)
            self.assertTrue(http.client.__name__ == 'http.client')

        elif PY2:
            import httplib
            self.assertTrue(type(httplib) == types.ModuleType)
            self.assertTrue(httplib.__name__ == 'httplib')

    def test_simple_import_with_alias(self):
        """Test simple import with aliasing."""
        import CLR as myCLR
        self.assertTrue(isCLRRootModule(myCLR))
        self.assertTrue(myCLR.__name__ == 'clr')

        import sys as mySys
        self.assertTrue(type(mySys) == types.ModuleType)
        self.assertTrue(mySys.__name__ == 'sys')

        if PY3:
            import http.client as myHttplib
            self.assertTrue(type(myHttplib) == types.ModuleType)
            self.assertTrue(myHttplib.__name__ == 'http.client')

        elif PY2:
            import httplib as myHttplib
            self.assertTrue(type(myHttplib) == types.ModuleType)
            self.assertTrue(myHttplib.__name__ == 'httplib')

    def test_dotted_name_import(self):
        """Test dotted-name import."""
        import CLR.System
        self.assertTrue(isCLRModule(CLR.System))
        self.assertTrue(CLR.System.__name__ == 'System')

        import System
        self.assertTrue(isCLRModule(System))
        self.assertTrue(System.__name__ == 'System')

        self.assertTrue(System is CLR.System)

        import xml.dom
        self.assertTrue(type(xml.dom) == types.ModuleType)
        self.assertTrue(xml.dom.__name__ == 'xml.dom')

    def test_dotted_name_import_with_alias(self):
        """Test dotted-name import with aliasing."""
        import CLR.System as myCLRSystem
        self.assertTrue(isCLRModule(myCLRSystem))
        self.assertTrue(myCLRSystem.__name__ == 'System')

        import System as mySystem
        self.assertTrue(isCLRModule(mySystem))
        self.assertTrue(mySystem.__name__ == 'System')

        self.assertTrue(mySystem is myCLRSystem)

        import xml.dom as myDom
        self.assertTrue(type(myDom) == types.ModuleType)
        self.assertTrue(myDom.__name__ == 'xml.dom')

    def test_simple_import_from(self):
        """Test simple 'import from'."""
        from CLR import System
        self.assertTrue(isCLRModule(System))
        self.assertTrue(System.__name__ == 'System')

        from xml import dom
        self.assertTrue(type(dom) == types.ModuleType)
        self.assertTrue(dom.__name__ == 'xml.dom')

    def test_simple_import_from_with_alias(self):
        """Test simple 'import from' with aliasing."""
        from CLR import System as mySystem
        self.assertTrue(isCLRModule(mySystem))
        self.assertTrue(mySystem.__name__ == 'System')

        from xml import dom as myDom
        self.assertTrue(type(myDom) == types.ModuleType)
        self.assertTrue(myDom.__name__ == 'xml.dom')

    def test_dotted_name_import_from(self):
        """Test dotted-name 'import from'."""
        from CLR.System import Xml
        self.assertTrue(isCLRModule(Xml))
        self.assertTrue(Xml.__name__ == 'System.Xml')

        from CLR.System.Xml import XmlDocument
        self.assertTrue(isCLRClass(XmlDocument))
        self.assertTrue(XmlDocument.__name__ == 'XmlDocument')

        from xml.dom import pulldom
        self.assertTrue(type(pulldom) == types.ModuleType)
        self.assertTrue(pulldom.__name__ == 'xml.dom.pulldom')

        from xml.dom.pulldom import PullDOM
        self.assertTrue(type(PullDOM) == ClassType)
        self.assertTrue(PullDOM.__name__ == 'PullDOM')

    def test_dotted_name_import_from_with_alias(self):
        """Test dotted-name 'import from' with aliasing."""
        from CLR.System import Xml as myXml
        self.assertTrue(isCLRModule(myXml))
        self.assertTrue(myXml.__name__ == 'System.Xml')

        from CLR.System.Xml import XmlDocument as myXmlDocument
        self.assertTrue(isCLRClass(myXmlDocument))
        self.assertTrue(myXmlDocument.__name__ == 'XmlDocument')

        from xml.dom import pulldom as myPulldom
        self.assertTrue(type(myPulldom) == types.ModuleType)
        self.assertTrue(myPulldom.__name__ == 'xml.dom.pulldom')

        from xml.dom.pulldom import PullDOM as myPullDOM
        self.assertTrue(type(myPullDOM) == ClassType)
        self.assertTrue(myPullDOM.__name__ == 'PullDOM')

    def test_from_module_import_star(self):
        """Test from module import * behavior."""
        count = len(locals().keys())
        m = __import__('CLR.System.Management', globals(), locals(), ['*'])
        self.assertTrue(m.__name__ == 'System.Management')
        self.assertTrue(isCLRModule(m))
        self.assertTrue(len(locals().keys()) > count + 1)

        m2 = __import__('System.Management', globals(), locals(), ['*'])
        self.assertTrue(m2.__name__ == 'System.Management')
        self.assertTrue(isCLRModule(m2))
        self.assertTrue(len(locals().keys()) > count + 1)

        self.assertTrue(m is m2)

    def test_explicit_assembly_load(self):
        """Test explicit assembly loading using standard CLR tools."""
        from CLR.System.Reflection import Assembly
        from CLR import System
        import sys

        assembly = Assembly.LoadWithPartialName('System.Data')
        self.assertTrue(assembly != None)

        import CLR.System.Data
        self.assertTrue('System.Data' in sys.modules)

        assembly = Assembly.LoadWithPartialName('SpamSpamSpamSpamEggsAndSpam')
        self.assertTrue(assembly == None)

    def test_implicit_load_already_valid_namespace(self):
        """Test implicit assembly load over an already valid namespace."""
        # In this case, the mscorlib assembly (loaded by default) defines
        # a number of types in the System namespace. There is also a System
        # assembly, which is _not_ loaded by default, which also contains
        # types in the System namespace. The desired behavior is for the
        # Python runtime to "do the right thing", allowing types from both
        # assemblies to be found in the CLR.System module implicitly.
        import CLR.System
        self.assertTrue(isCLRClass(CLR.System.UriBuilder))

    def test_import_non_existant_module(self):
        """Test import failure for a non-existant module."""

        def test():
            import System.SpamSpamSpam

        def testclr():
            import CLR.System.SpamSpamSpam

        self.assertRaises(ImportError, test)
        self.assertRaises(ImportError, testclr)

    def test_lookup_no_namespace_type(self):
        """Test lookup of types without a qualified namespace."""
        import CLR.Python.Test
        import CLR
        self.assertTrue(isCLRClass(CLR.NoNamespaceType))

    def test_module_lookup_recursion(self):
        """Test for recursive lookup handling."""

        def test1():
            from CLR import CLR

        self.assertRaises(ImportError, test1)

        def test2():
            import CLR
            x = CLR.CLR

        self.assertRaises(AttributeError, test2)

    def test_module_get_attr(self):
        """Test module getattr behavior."""
        import CLR.System as System

        int_type = System.Int32
        self.assertTrue(isCLRClass(int_type))

        module = System.Xml
        self.assertTrue(isCLRModule(module))

        def test():
            spam = System.Spam

        self.assertRaises(AttributeError, test)

        def test():
            spam = getattr(System, 1)

        self.assertRaises(TypeError, test)

    def test000_multiple_imports(self):
        # import CLR did raise a Seg Fault once
        # test if the Exceptions.warn() method still causes it
        for n in range(100):
            import CLR


def test_suite():
    return unittest.makeSuite(CompatibilityTests)
