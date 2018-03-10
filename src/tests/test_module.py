# -*- coding: utf-8 -*-

"""Test CLR modules and the CLR import hook."""

import clr
import time
import types
import warnings
from fnmatch import fnmatch

import pytest

from ._compat import ClassType, PY2, PY3, range
from .utils import is_clr_class, is_clr_module, is_clr_root_module


# testImplicitAssemblyLoad() passes on deprecation warning; perfect! #
# clr.AddReference('System.Windows.Forms')

def test_import_hook_works():
    """Test that the import hook works correctly both using the
       included runtime and an external runtime. This must be
       the first test run in the unit tests!"""
    from System import String


def test_import_clr():
    import clr
    assert is_clr_root_module(clr)


def test_version_clr():
    import clr
    assert clr.__version__ >= "2.2.0"


def test_preload_var():
    import clr
    assert clr.getPreload() is False, clr.getPreload()
    clr.setPreload(False)
    assert clr.getPreload() is False, clr.getPreload()
    try:
        clr.setPreload(True)
        assert clr.getPreload() is True, clr.getPreload()
        clr.setPreload(0)
        assert clr.getPreload() is False, clr.getPreload()
        clr.setPreload(1)
        assert clr.getPreload() is True, clr.getPreload()

        import System.Configuration
        content = dir(System.Configuration)
        assert len(content) > 10, content
    finally:
        clr.setPreload(False)


def test_module_interface():
    """Test the interface exposed by CLR module objects."""
    import System
    assert type(System.__dict__) == type({})
    assert System.__name__ == 'System'
    # the filename can be any module from the System namespace
    # (eg System.Data.dll or System.dll, but also mscorlib.dll)
    system_file = System.__file__
    assert fnmatch(system_file, "*System*.dll") or fnmatch(system_file, "*mscorlib.dll"), \
        "unexpected System.__file__: " + system_file
    assert System.__doc__.startswith("Namespace containing types from the following assemblies:")
    assert is_clr_class(System.String)
    assert is_clr_class(System.Int32)


def test_simple_import():
    """Test simple import."""
    import System
    assert is_clr_module(System)
    assert System.__name__ == 'System'

    import sys
    assert isinstance(sys, types.ModuleType)
    assert sys.__name__ == 'sys'

    if PY3:
        import http.client as httplib
        assert isinstance(httplib, types.ModuleType)
        assert httplib.__name__ == 'http.client'
    elif PY2:
        import httplib
        assert isinstance(httplib, types.ModuleType)
        assert httplib.__name__ == 'httplib'


def test_simple_import_with_alias():
    """Test simple import with aliasing."""
    import System as mySystem
    assert is_clr_module(mySystem)
    assert mySystem.__name__ == 'System'

    import sys as mySys
    assert isinstance(mySys, types.ModuleType)
    assert mySys.__name__ == 'sys'

    if PY3:
        import http.client as myHttplib
        assert isinstance(myHttplib, types.ModuleType)
        assert myHttplib.__name__ == 'http.client'
    elif PY2:
        import httplib as myHttplib
        assert isinstance(myHttplib, types.ModuleType)
        assert myHttplib.__name__ == 'httplib'


def test_dotted_name_import():
    """Test dotted-name import."""
    import System.Reflection
    assert is_clr_module(System.Reflection)
    assert System.Reflection.__name__ == 'System.Reflection'

    import xml.dom
    assert isinstance(xml.dom, types.ModuleType)
    assert xml.dom.__name__ == 'xml.dom'


def test_multiple_dotted_name_import():
    """Test an import bug with multiple dotted imports."""
    import System.Data
    assert is_clr_module(System.Data)
    assert System.Data.__name__ == 'System.Data'
    import System.Data
    assert is_clr_module(System.Data)
    assert System.Data.__name__ == 'System.Data'


def test_dotted_name_import_with_alias():
    """Test dotted-name import with aliasing."""
    import System.Reflection as SysRef
    assert is_clr_module(SysRef)
    assert SysRef.__name__ == 'System.Reflection'

    import xml.dom as myDom
    assert isinstance(myDom, types.ModuleType)
    assert myDom.__name__ == 'xml.dom'


def test_simple_import_from():
    """Test simple 'import from'."""
    from System import Reflection
    assert is_clr_module(Reflection)
    assert Reflection.__name__ == 'System.Reflection'

    from xml import dom
    assert isinstance(dom, types.ModuleType)
    assert dom.__name__ == 'xml.dom'


def test_simple_import_from_with_alias():
    """Test simple 'import from' with aliasing."""
    from System import Collections as Coll
    assert is_clr_module(Coll)
    assert Coll.__name__ == 'System.Collections'

    from xml import dom as myDom
    assert isinstance(myDom, types.ModuleType)
    assert myDom.__name__ == 'xml.dom'


def test_dotted_name_import_from():
    """Test dotted-name 'import from'."""
    from System.Collections import Specialized
    assert is_clr_module(Specialized)
    assert Specialized.__name__ == 'System.Collections.Specialized'

    from System.Collections.Specialized import StringCollection
    assert is_clr_class(StringCollection)
    assert StringCollection.__name__ == 'StringCollection'

    from xml.dom import pulldom
    assert isinstance(pulldom, types.ModuleType)
    assert pulldom.__name__ == 'xml.dom.pulldom'

    from xml.dom.pulldom import PullDOM
    assert isinstance(PullDOM, ClassType)
    assert PullDOM.__name__ == 'PullDOM'


def test_dotted_name_import_from_with_alias():
    """Test dotted-name 'import from' with aliasing."""
    from System.Collections import Specialized as Spec
    assert is_clr_module(Spec)
    assert Spec.__name__ == 'System.Collections.Specialized'

    from System.Collections.Specialized import StringCollection as SC
    assert is_clr_class(SC)
    assert SC.__name__ == 'StringCollection'

    from xml.dom import pulldom as myPulldom
    assert isinstance(myPulldom, types.ModuleType)
    assert myPulldom.__name__ == 'xml.dom.pulldom'

    from xml.dom.pulldom import PullDOM as myPullDOM
    assert isinstance(myPullDOM, ClassType)
    assert myPullDOM.__name__ == 'PullDOM'


def test_from_module_import_star():
    """Test from module import * behavior."""
    count = len(locals().keys())
    m = __import__('System.Xml', globals(), locals(), ['*'])
    assert m.__name__ == 'System.Xml'
    assert is_clr_module(m)
    assert len(locals().keys()) > count + 1


def test_implicit_assembly_load():
    """Test implicit assembly loading via import."""
    with warnings.catch_warnings(record=True) as w:
        warnings.simplefilter("always")

        # should trigger a DeprecationWarning as Microsoft.Build hasn't
        # been added as a reference yet (and should exist for mono)
        import Microsoft.Build

        assert len(w) == 1
        assert isinstance(w[0].message, DeprecationWarning)

    with warnings.catch_warnings(record=True) as w:
        clr.AddReference("System.Windows.Forms")
        import System.Windows.Forms as Forms
        assert is_clr_module(Forms)
        assert Forms.__name__ == 'System.Windows.Forms'
        from System.Windows.Forms import Form
        assert is_clr_class(Form)
        assert Form.__name__ == 'Form'
        assert len(w) == 0


def test_explicit_assembly_load():
    """Test explicit assembly loading using standard CLR tools."""
    from System.Reflection import Assembly
    import System, sys

    assembly = Assembly.LoadWithPartialName('System.Data')
    assert assembly is not None

    import System.Data
    assert 'System.Data' in sys.modules

    assembly = Assembly.LoadWithPartialName('SpamSpamSpamSpamEggsAndSpam')
    assert assembly is None


def test_implicit_load_already_valid_namespace():
    """Test implicit assembly load over an already valid namespace."""
    # In this case, the mscorlib assembly (loaded by default) defines
    # a number of types in the System namespace. There is also a System
    # assembly, which is _not_ loaded by default, which also contains
    # types in the System namespace. The desired behavior is for the
    # Python runtime to "do the right thing", allowing types from both
    # assemblies to be found in the System module implicitly.
    import System
    assert is_clr_class(System.UriBuilder)


def test_import_non_existant_module():
    """Test import failure for a non-existent module."""
    with pytest.raises(ImportError):
        import System.SpamSpamSpam


def test_lookup_no_namespace_type():
    """Test lookup of types without a qualified namespace."""
    import Python.Test
    import clr
    assert is_clr_class(clr.NoNamespaceType)


def test_module_lookup_recursion():
    """Test for recursive lookup handling."""

    with pytest.raises(ImportError):
        from System import System

    with pytest.raises(AttributeError):
        import System
        _ = System.System


def test_module_get_attr():
    """Test module getattr behavior."""
    import System

    int_type = System.Int32
    assert is_clr_class(int_type)

    module = System.Xml
    assert is_clr_module(module)

    with pytest.raises(AttributeError):
        _ = System.Spam

    with pytest.raises(TypeError):
        _ = getattr(System, 1)


def test_module_attr_abuse():
    """Test handling of attempts to set module attributes."""

    # It would be safer to use a dict-proxy as the __dict__ for CLR
    # modules, but as of Python 2.3 some parts of the CPython runtime
    # like dir() will fail if a module dict is not a real dictionary.

    def test():
        import System
        System.__dict__['foo'] = 0
        return 1

    assert test()


def test_module_type_abuse():
    """Test handling of attempts to break the module type."""
    import System
    mtype = type(System)

    with pytest.raises(TypeError):
        mtype.__getattribute__(0, 'spam')

    with pytest.raises(TypeError):
        mtype.__setattr__(0, 'spam', 1)

    with pytest.raises(TypeError):
        mtype.__repr__(0)


def test_clr_list_assemblies():
    from clr import ListAssemblies
    verbose = list(ListAssemblies(True))
    short = list(ListAssemblies(False))
    assert u'mscorlib' in short
    assert u'System' in short
    assert u'Culture=' in verbose[0]
    assert u'Version=' in verbose[0]


def test_clr_add_reference():
    from clr import AddReference
    from System.IO import FileNotFoundException
    for name in ("System", "Python.Runtime"):
        assy = AddReference(name)
        assy_name = assy.GetName().Name
        assert assy_name == name

    with pytest.raises(FileNotFoundException):
        AddReference("somethingtotallysilly")

def test_clr_get_clr_type():
    """Test clr.GetClrType()."""
    from clr import GetClrType
    import System
    from System import IComparable
    from System import ArgumentException
    assert GetClrType(System.String).FullName == "System.String"
    comparable = GetClrType(IComparable)
    assert comparable.FullName == "System.IComparable"
    assert comparable.IsInterface
    assert GetClrType(int).FullName == "System.Int32"
    assert GetClrType(str).FullName == "System.String"
    assert GetClrType(float).FullName == "System.Double"
    dblarr = System.Array[System.Double]
    assert GetClrType(dblarr).FullName == "System.Double[]"

    with pytest.raises(TypeError):
        GetClrType(1)
    with pytest.raises(TypeError):
        GetClrType("thiswillfail")

def test_assembly_load_thread_safety():
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

def test_assembly_load_recursion_bug():
    """Test fix for recursion bug documented in #627"""
    from System.Configuration import ConfigurationManager
    content = dir(ConfigurationManager)
    assert len(content) > 5, content
