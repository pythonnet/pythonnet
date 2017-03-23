# -*- coding: utf-8 -*-
# TODO: Complete removal of methods below. Similar to test_module

"""Backward-compatibility tests for deprecated features."""

import types

import pytest

from ._compat import ClassType, PY2, PY3, range
from .utils import is_clr_class, is_clr_module, is_clr_root_module


# Tests for old-style CLR-prefixed module naming.
def test_simple_import():
    """Test simple import."""
    import CLR
    assert is_clr_root_module(CLR)
    assert CLR.__name__ == 'clr'

    import sys
    assert isinstance(sys, types.ModuleType)
    assert sys.__name__ == 'sys'

    if PY3:
        import http.client
        assert isinstance(http.client, types.ModuleType)
        assert http.client.__name__ == 'http.client'

    elif PY2:
        import httplib
        assert isinstance(httplib, types.ModuleType)
        assert httplib.__name__ == 'httplib'


def test_simple_import_with_alias():
    """Test simple import with aliasing."""
    import CLR as myCLR
    assert is_clr_root_module(myCLR)
    assert myCLR.__name__ == 'clr'

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
    import CLR.System
    assert is_clr_module(CLR.System)
    assert CLR.System.__name__ == 'System'

    import System
    assert is_clr_module(System)
    assert System.__name__ == 'System'

    assert System is CLR.System

    import xml.dom
    assert isinstance(xml.dom, types.ModuleType)
    assert xml.dom.__name__ == 'xml.dom'


def test_dotted_name_import_with_alias():
    """Test dotted-name import with aliasing."""
    import CLR.System as myCLRSystem
    assert is_clr_module(myCLRSystem)
    assert myCLRSystem.__name__ == 'System'

    import System as mySystem
    assert is_clr_module(mySystem)
    assert mySystem.__name__ == 'System'

    assert mySystem is myCLRSystem

    import xml.dom as myDom
    assert isinstance(myDom, types.ModuleType)
    assert myDom.__name__ == 'xml.dom'


def test_simple_import_from():
    """Test simple 'import from'."""
    from CLR import System
    assert is_clr_module(System)
    assert System.__name__ == 'System'

    from xml import dom
    assert isinstance(dom, types.ModuleType)
    assert dom.__name__ == 'xml.dom'


def test_simple_import_from_with_alias():
    """Test simple 'import from' with aliasing."""
    from CLR import System as mySystem
    assert is_clr_module(mySystem)
    assert mySystem.__name__ == 'System'

    from xml import dom as myDom
    assert isinstance(myDom, types.ModuleType)
    assert myDom.__name__ == 'xml.dom'


def test_dotted_name_import_from():
    """Test dotted-name 'import from'."""
    from CLR.System import Xml
    assert is_clr_module(Xml)
    assert Xml.__name__ == 'System.Xml'

    from CLR.System.Xml import XmlDocument
    assert is_clr_class(XmlDocument)
    assert XmlDocument.__name__ == 'XmlDocument'

    from xml.dom import pulldom
    assert isinstance(pulldom, types.ModuleType)
    assert pulldom.__name__ == 'xml.dom.pulldom'

    from xml.dom.pulldom import PullDOM
    assert isinstance(PullDOM, ClassType)
    assert PullDOM.__name__ == 'PullDOM'


def test_dotted_name_import_from_with_alias():
    """Test dotted-name 'import from' with aliasing."""
    from CLR.System import Xml as myXml
    assert is_clr_module(myXml)
    assert myXml.__name__ == 'System.Xml'

    from CLR.System.Xml import XmlDocument as myXmlDocument
    assert is_clr_class(myXmlDocument)
    assert myXmlDocument.__name__ == 'XmlDocument'

    from xml.dom import pulldom as myPulldom
    assert isinstance(myPulldom, types.ModuleType)
    assert myPulldom.__name__ == 'xml.dom.pulldom'

    from xml.dom.pulldom import PullDOM as myPullDOM
    assert isinstance(myPullDOM, ClassType)
    assert myPullDOM.__name__ == 'PullDOM'


def test_from_module_import_star():
    """Test from module import * behavior."""
    count = len(locals().keys())
    m = __import__('CLR.System.Management', globals(), locals(), ['*'])
    assert m.__name__ == 'System.Management'
    assert is_clr_module(m)
    assert len(locals().keys()) > count + 1

    m2 = __import__('System.Management', globals(), locals(), ['*'])
    assert m2.__name__ == 'System.Management'
    assert is_clr_module(m2)
    assert len(locals().keys()) > count + 1

    assert m is m2


def test_explicit_assembly_load():
    """Test explicit assembly loading using standard CLR tools."""
    from CLR.System.Reflection import Assembly
    from CLR import System
    import sys

    assembly = Assembly.LoadWithPartialName('System.Data')
    assert assembly is not None

    import CLR.System.Data
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
    # assemblies to be found in the CLR.System module implicitly.
    import CLR.System
    assert is_clr_class(CLR.System.UriBuilder)


def test_import_non_existant_module():
    """Test import failure for a non-existent module."""
    with pytest.raises(ImportError):
        import System.SpamSpamSpam

    with pytest.raises(ImportError):
        import CLR.System.SpamSpamSpam


def test_lookup_no_namespace_type():
    """Test lookup of types without a qualified namespace."""
    import CLR.Python.Test
    import CLR
    assert is_clr_class(CLR.NoNamespaceType)


def test_module_lookup_recursion():
    """Test for recursive lookup handling."""
    with pytest.raises(ImportError):
        from CLR import CLR

    with pytest.raises(AttributeError):
        import CLR
        _ = CLR.CLR


def test_module_get_attr():
    """Test module getattr behavior."""
    import CLR.System as System

    int_type = System.Int32
    assert is_clr_class(int_type)

    module = System.Xml
    assert is_clr_module(module)

    with pytest.raises(AttributeError):
        _ = System.Spam

    with pytest.raises(TypeError):
        _ = getattr(System, 1)


def test_multiple_imports():
    # import CLR did raise a Seg Fault once
    # test if the Exceptions.warn() method still causes it
    for _ in range(100):
        import CLR
        _ = CLR
