# -*- coding: utf-8 -*-
# TODO: Complete removal of methods below. Similar to test_module

import types
import unittest

from _compat import ClassType, PY2, PY3, range
from utils import is_clr_class, is_clr_module, is_clr_root_module


class CompatibilityTests(unittest.TestCase):
    """Backward-compatibility tests for deprecated features."""

    # Tests for old-style CLR-prefixed module naming.

    def test_simple_import(self):
        """Test simple import."""
        import CLR
        self.assertTrue(is_clr_root_module(CLR))
        self.assertTrue(CLR.__name__ == 'clr')

        import sys
        self.assertTrue(isinstance(sys, types.ModuleType))
        self.assertTrue(sys.__name__ == 'sys')

        if PY3:
            import http.client
            self.assertTrue(isinstance(http.client, types.ModuleType))
            self.assertTrue(http.client.__name__ == 'http.client')

        elif PY2:
            import httplib
            self.assertTrue(isinstance(httplib, types.ModuleType))
            self.assertTrue(httplib.__name__ == 'httplib')

    def test_simple_import_with_alias(self):
        """Test simple import with aliasing."""
        import CLR as myCLR
        self.assertTrue(is_clr_root_module(myCLR))
        self.assertTrue(myCLR.__name__ == 'clr')

        import sys as mySys
        self.assertTrue(isinstance(mySys, types.ModuleType))
        self.assertTrue(mySys.__name__ == 'sys')

        if PY3:
            import http.client as myHttplib
            self.assertTrue(isinstance(myHttplib, types.ModuleType))
            self.assertTrue(myHttplib.__name__ == 'http.client')

        elif PY2:
            import httplib as myHttplib
            self.assertTrue(isinstance(myHttplib, types.ModuleType))
            self.assertTrue(myHttplib.__name__ == 'httplib')

    def test_dotted_name_import(self):
        """Test dotted-name import."""
        import CLR.System
        self.assertTrue(is_clr_module(CLR.System))
        self.assertTrue(CLR.System.__name__ == 'System')

        import System
        self.assertTrue(is_clr_module(System))
        self.assertTrue(System.__name__ == 'System')

        self.assertTrue(System is CLR.System)

        import xml.dom
        self.assertTrue(isinstance(xml.dom, types.ModuleType))
        self.assertTrue(xml.dom.__name__ == 'xml.dom')

    def test_dotted_name_import_with_alias(self):
        """Test dotted-name import with aliasing."""
        import CLR.System as myCLRSystem
        self.assertTrue(is_clr_module(myCLRSystem))
        self.assertTrue(myCLRSystem.__name__ == 'System')

        import System as mySystem
        self.assertTrue(is_clr_module(mySystem))
        self.assertTrue(mySystem.__name__ == 'System')

        self.assertTrue(mySystem is myCLRSystem)

        import xml.dom as myDom
        self.assertTrue(isinstance(myDom, types.ModuleType))
        self.assertTrue(myDom.__name__ == 'xml.dom')

    def test_simple_import_from(self):
        """Test simple 'import from'."""
        from CLR import System
        self.assertTrue(is_clr_module(System))
        self.assertTrue(System.__name__ == 'System')

        from xml import dom
        self.assertTrue(isinstance(dom, types.ModuleType))
        self.assertTrue(dom.__name__ == 'xml.dom')

    def test_simple_import_from_with_alias(self):
        """Test simple 'import from' with aliasing."""
        from CLR import System as mySystem
        self.assertTrue(is_clr_module(mySystem))
        self.assertTrue(mySystem.__name__ == 'System')

        from xml import dom as myDom
        self.assertTrue(isinstance(myDom, types.ModuleType))
        self.assertTrue(myDom.__name__ == 'xml.dom')

    def test_dotted_name_import_from(self):
        """Test dotted-name 'import from'."""
        from CLR.System import Xml
        self.assertTrue(is_clr_module(Xml))
        self.assertTrue(Xml.__name__ == 'System.Xml')

        from CLR.System.Xml import XmlDocument
        self.assertTrue(is_clr_class(XmlDocument))
        self.assertTrue(XmlDocument.__name__ == 'XmlDocument')

        from xml.dom import pulldom
        self.assertTrue(isinstance(pulldom, types.ModuleType))
        self.assertTrue(pulldom.__name__ == 'xml.dom.pulldom')

        from xml.dom.pulldom import PullDOM
        self.assertTrue(isinstance(PullDOM, ClassType))
        self.assertTrue(PullDOM.__name__ == 'PullDOM')

    def test_dotted_name_import_from_with_alias(self):
        """Test dotted-name 'import from' with aliasing."""
        from CLR.System import Xml as myXml
        self.assertTrue(is_clr_module(myXml))
        self.assertTrue(myXml.__name__ == 'System.Xml')

        from CLR.System.Xml import XmlDocument as myXmlDocument
        self.assertTrue(is_clr_class(myXmlDocument))
        self.assertTrue(myXmlDocument.__name__ == 'XmlDocument')

        from xml.dom import pulldom as myPulldom
        self.assertTrue(isinstance(myPulldom, types.ModuleType))
        self.assertTrue(myPulldom.__name__ == 'xml.dom.pulldom')

        from xml.dom.pulldom import PullDOM as myPullDOM
        self.assertTrue(isinstance(myPullDOM, ClassType))
        self.assertTrue(myPullDOM.__name__ == 'PullDOM')

    def test_from_module_import_star(self):
        """Test from module import * behavior."""
        count = len(locals().keys())
        m = __import__('CLR.System.Management', globals(), locals(), ['*'])
        self.assertTrue(m.__name__ == 'System.Management')
        self.assertTrue(is_clr_module(m))
        self.assertTrue(len(locals().keys()) > count + 1)

        m2 = __import__('System.Management', globals(), locals(), ['*'])
        self.assertTrue(m2.__name__ == 'System.Management')
        self.assertTrue(is_clr_module(m2))
        self.assertTrue(len(locals().keys()) > count + 1)

        self.assertTrue(m is m2)

    def test_explicit_assembly_load(self):
        """Test explicit assembly loading using standard CLR tools."""
        from CLR.System.Reflection import Assembly
        from CLR import System
        import sys

        assembly = Assembly.LoadWithPartialName('System.Data')
        self.assertTrue(assembly is not None)

        import CLR.System.Data
        self.assertTrue('System.Data' in sys.modules)

        assembly = Assembly.LoadWithPartialName('SpamSpamSpamSpamEggsAndSpam')
        self.assertTrue(assembly is None)

    def test_implicit_load_already_valid_namespace(self):
        """Test implicit assembly load over an already valid namespace."""
        # In this case, the mscorlib assembly (loaded by default) defines
        # a number of types in the System namespace. There is also a System
        # assembly, which is _not_ loaded by default, which also contains
        # types in the System namespace. The desired behavior is for the
        # Python runtime to "do the right thing", allowing types from both
        # assemblies to be found in the CLR.System module implicitly.
        import CLR.System
        self.assertTrue(is_clr_class(CLR.System.UriBuilder))

    def test_import_non_existant_module(self):
        """Test import failure for a non-existent module."""
        with self.assertRaises(ImportError):
            import System.SpamSpamSpam

        with self.assertRaises(ImportError):
            import CLR.System.SpamSpamSpam

    def test_lookup_no_namespace_type(self):
        """Test lookup of types without a qualified namespace."""
        import CLR.Python.Test
        import CLR
        self.assertTrue(is_clr_class(CLR.NoNamespaceType))

    def test_module_lookup_recursion(self):
        """Test for recursive lookup handling."""
        with self.assertRaises(ImportError):
            from CLR import CLR

        with self.assertRaises(AttributeError):
            import CLR
            _ = CLR.CLR

    def test_module_get_attr(self):
        """Test module getattr behavior."""
        import CLR.System as System

        int_type = System.Int32
        self.assertTrue(is_clr_class(int_type))

        module = System.Xml
        self.assertTrue(is_clr_module(module))

        with self.assertRaises(AttributeError):
            _ = System.Spam

        with self.assertRaises(TypeError):
            _ = getattr(System, 1)

    def test_multiple_imports(self):
        # import CLR did raise a Seg Fault once
        # test if the Exceptions.warn() method still causes it
        for _ in range(100):
            import CLR
            _ = CLR


def test_suite():
    return unittest.makeSuite(CompatibilityTests)
