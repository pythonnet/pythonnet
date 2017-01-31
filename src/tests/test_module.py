# -*- coding: utf-8 -*-

import clr
import time
import types
import unittest
import warnings
from fnmatch import fnmatch

from _compat import ClassType, PY2, PY3, range
from utils import is_clr_class, is_clr_module, is_clr_root_module


# testImplicitAssemblyLoad() passes on deprecation warning; perfect! #
# clr.AddReference('System.Windows.Forms')

class ModuleTests(unittest.TestCase):
    """Test CLR modules and the CLR import hook."""

    def test_import_hook_works(self):
        """Test that the import hook works correctly both using the
           included runtime and an external runtime. This must be
           the first test run in the unit tests!"""
        from System import String

    def test_import_clr(self):
        import clr
        self.assertTrue(is_clr_root_module(clr))

    def test_version_clr(self):
        import clr
        self.assertTrue(clr.__version__ >= "2.2.0")

    def test_preload_var(self):
        import clr
        self.assertTrue(clr.getPreload() is False, clr.getPreload())
        clr.setPreload(False)
        self.assertTrue(clr.getPreload() is False, clr.getPreload())
        try:
            clr.setPreload(True)
            self.assertTrue(clr.getPreload() is True, clr.getPreload())
            clr.setPreload(0)
            self.assertTrue(clr.getPreload() is False, clr.getPreload())
            clr.setPreload(1)
            self.assertTrue(clr.getPreload() is True, clr.getPreload())

            import System.Configuration
            content = dir(System.Configuration)
            self.assertTrue(len(content) > 10, content)
        finally:
            clr.setPreload(False)

    def test_module_interface(self):
        """Test the interface exposed by CLR module objects."""
        import System
        self.assertEquals(type(System.__dict__), type({}))
        self.assertEquals(System.__name__, 'System')
        # the filename can be any module from the System namespace
        # (eg System.Data.dll or System.dll, but also mscorlib.dll)
        system_file = System.__file__
        self.assertTrue(fnmatch(system_file, "*System*.dll") or fnmatch(system_file, "*mscorlib.dll"),
                        "unexpected System.__file__: " + system_file)
        self.assertTrue(System.__doc__.startswith("Namespace containing types from the following assemblies:"))
        self.assertTrue(is_clr_class(System.String))
        self.assertTrue(is_clr_class(System.Int32))

    def test_simple_import(self):
        """Test simple import."""
        import System
        self.assertTrue(is_clr_module(System))
        self.assertTrue(System.__name__ == 'System')

        import sys
        self.assertTrue(isinstance(sys, types.ModuleType))
        self.assertTrue(sys.__name__ == 'sys')

        if PY3:
            import http.client as httplib
            self.assertTrue(isinstance(httplib, types.ModuleType))
            self.assertTrue(httplib.__name__ == 'http.client')
        elif PY2:
            import httplib
            self.assertTrue(isinstance(httplib, types.ModuleType))
            self.assertTrue(httplib.__name__ == 'httplib')

    def test_simple_import_with_alias(self):
        """Test simple import with aliasing."""
        import System as mySystem
        self.assertTrue(is_clr_module(mySystem))
        self.assertTrue(mySystem.__name__ == 'System')

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
        import System.Reflection
        self.assertTrue(is_clr_module(System.Reflection))
        self.assertTrue(System.Reflection.__name__ == 'System.Reflection')

        import xml.dom
        self.assertTrue(isinstance(xml.dom, types.ModuleType))
        self.assertTrue(xml.dom.__name__ == 'xml.dom')

    def test_multiple_dotted_name_import(self):
        """Test an import bug with multiple dotted imports."""
        import System.Data
        self.assertTrue(is_clr_module(System.Data))
        self.assertTrue(System.Data.__name__ == 'System.Data')
        import System.Data
        self.assertTrue(is_clr_module(System.Data))
        self.assertTrue(System.Data.__name__ == 'System.Data')

    def test_dotted_name_import_with_alias(self):
        """Test dotted-name import with aliasing."""
        import System.Reflection as SysRef
        self.assertTrue(is_clr_module(SysRef))
        self.assertTrue(SysRef.__name__ == 'System.Reflection')

        import xml.dom as myDom
        self.assertTrue(isinstance(myDom, types.ModuleType))
        self.assertTrue(myDom.__name__ == 'xml.dom')

    def test_simple_import_from(self):
        """Test simple 'import from'."""
        from System import Reflection
        self.assertTrue(is_clr_module(Reflection))
        self.assertTrue(Reflection.__name__ == 'System.Reflection')

        from xml import dom
        self.assertTrue(isinstance(dom, types.ModuleType))
        self.assertTrue(dom.__name__ == 'xml.dom')

    def test_simple_import_from_with_alias(self):
        """Test simple 'import from' with aliasing."""
        from System import Collections as Coll
        self.assertTrue(is_clr_module(Coll))
        self.assertTrue(Coll.__name__ == 'System.Collections')

        from xml import dom as myDom
        self.assertTrue(isinstance(myDom, types.ModuleType))
        self.assertTrue(myDom.__name__ == 'xml.dom')

    def test_dotted_name_import_from(self):
        """Test dotted-name 'import from'."""
        from System.Collections import Specialized
        self.assertTrue(is_clr_module(Specialized))
        self.assertTrue(
            Specialized.__name__ == 'System.Collections.Specialized')

        from System.Collections.Specialized import StringCollection
        self.assertTrue(is_clr_class(StringCollection))
        self.assertTrue(StringCollection.__name__ == 'StringCollection')

        from xml.dom import pulldom
        self.assertTrue(isinstance(pulldom, types.ModuleType))
        self.assertTrue(pulldom.__name__ == 'xml.dom.pulldom')

        from xml.dom.pulldom import PullDOM
        self.assertTrue(isinstance(PullDOM, ClassType))
        self.assertTrue(PullDOM.__name__ == 'PullDOM')

    def test_dotted_name_import_from_with_alias(self):
        """Test dotted-name 'import from' with aliasing."""
        from System.Collections import Specialized as Spec
        self.assertTrue(is_clr_module(Spec))
        self.assertTrue(Spec.__name__ == 'System.Collections.Specialized')

        from System.Collections.Specialized import StringCollection as SC
        self.assertTrue(is_clr_class(SC))
        self.assertTrue(SC.__name__ == 'StringCollection')

        from xml.dom import pulldom as myPulldom
        self.assertTrue(isinstance(myPulldom, types.ModuleType))
        self.assertTrue(myPulldom.__name__ == 'xml.dom.pulldom')

        from xml.dom.pulldom import PullDOM as myPullDOM
        self.assertTrue(isinstance(myPullDOM, ClassType))
        self.assertTrue(myPullDOM.__name__ == 'PullDOM')

    def test_from_module_import_star(self):
        """Test from module import * behavior."""
        count = len(locals().keys())
        m = __import__('System.Xml', globals(), locals(), ['*'])
        self.assertTrue(m.__name__ == 'System.Xml')
        self.assertTrue(is_clr_module(m))
        self.assertTrue(len(locals().keys()) > count + 1)

    def test_implicit_assembly_load(self):
        """Test implicit assembly loading via import."""
        with warnings.catch_warnings(record=True) as w:
            warnings.simplefilter("always")

            # should trigger a DeprecationWarning as Microsoft.Build hasn't
            # been added as a reference yet (and should exist for mono)
            import Microsoft.Build

            self.assertEqual(len(w), 1)
            self.assertTrue(isinstance(w[0].message, DeprecationWarning))

        with warnings.catch_warnings(record=True) as w:
            clr.AddReference("System.Windows.Forms")
            import System.Windows.Forms as Forms
            self.assertTrue(is_clr_module(Forms))
            self.assertTrue(Forms.__name__ == 'System.Windows.Forms')
            from System.Windows.Forms import Form
            self.assertTrue(is_clr_class(Form))
            self.assertTrue(Form.__name__ == 'Form')
            self.assertEqual(len(w), 0)

    def test_explicit_assembly_load(self):
        """Test explicit assembly loading using standard CLR tools."""
        from System.Reflection import Assembly
        import System, sys

        assembly = Assembly.LoadWithPartialName('System.Data')
        self.assertTrue(assembly is not None)

        import System.Data
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
        # assemblies to be found in the System module implicitly.
        import System
        self.assertTrue(is_clr_class(System.UriBuilder))

    def test_import_non_existant_module(self):
        """Test import failure for a non-existent module."""
        with self.assertRaises(ImportError):
            import System.SpamSpamSpam

    def test_lookup_no_namespace_type(self):
        """Test lookup of types without a qualified namespace."""
        import Python.Test
        import clr
        self.assertTrue(is_clr_class(clr.NoNamespaceType))

    def test_module_lookup_recursion(self):
        """Test for recursive lookup handling."""

        with self.assertRaises(ImportError):
            from System import System

        with self.assertRaises(AttributeError):
            import System
            _ = System.System

    def test_module_get_attr(self):
        """Test module getattr behavior."""
        import System

        int_type = System.Int32
        self.assertTrue(is_clr_class(int_type))

        module = System.Xml
        self.assertTrue(is_clr_module(module))

        with self.assertRaises(AttributeError):
            _ = System.Spam

        with self.assertRaises(TypeError):
            _ = getattr(System, 1)

    def test_module_attr_abuse(self):
        """Test handling of attempts to set module attributes."""

        # It would be safer to use a dict-proxy as the __dict__ for CLR
        # modules, but as of Python 2.3 some parts of the CPython runtime
        # like dir() will fail if a module dict is not a real dictionary.

        def test():
            import System
            System.__dict__['foo'] = 0
            return 1

        self.assertTrue(test())

    def test_module_type_abuse(self):
        """Test handling of attempts to break the module type."""
        import System
        mtype = type(System)

        with self.assertRaises(TypeError):
            mtype.__getattribute__(0, 'spam')

        with self.assertRaises(TypeError):
            mtype.__setattr__(0, 'spam', 1)

        with self.assertRaises(TypeError):
            mtype.__repr__(0)

    def test_clr_list_assemblies(self):
        from clr import ListAssemblies
        verbose = list(ListAssemblies(True))
        short = list(ListAssemblies(False))
        self.assertTrue(u'mscorlib' in short)
        self.assertTrue(u'System' in short)
        self.assertTrue(u'Culture=' in verbose[0])
        self.assertTrue(u'Version=' in verbose[0])

    def test_clr_add_reference(self):
        from clr import AddReference
        from System.IO import FileNotFoundException
        for name in ("System", "Python.Runtime"):
            assy = AddReference(name)
            assy_name = assy.GetName().Name
            self.assertEqual(assy_name, name)

        with self.assertRaises(FileNotFoundException):
            AddReference("somethingtotallysilly")

    def test_assembly_load_thread_safety(self):
        from Python.Test import ModuleTest
        # spin up .NET thread which loads assemblies and triggers AppDomain.AssemblyLoad event
        ModuleTest.RunThreads()
        time.sleep(1e-5)
        for _ in range(1, 100):
            # call import clr, which in AssemblyManager.GetNames iterates through the loaded types
            import clr
            # import some .NET types
            from System import DateTime
            from System import Guid
            from System.Collections.Generic import Dictionary
            _ = Dictionary[Guid, DateTime]()
        ModuleTest.JoinThreads()


def test_suite():
    return unittest.makeSuite(ModuleTests)
