# Copyright (c) 2001, 2002 Zope Corporation and Contributors.
#
# All Rights Reserved.
#
# This software is subject to the provisions of the Zope Public License,
# Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
# THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
# WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
# FOR A PARTICULAR PURPOSE.

import sys, os, string, unittest, types


class ModuleTests(unittest.TestCase):
    """Test CLR modules and the CLR import hook."""

    def isCLRModule(self, object):
        return type(object).__name__ == 'ModuleObject'


    def isCLRClass(self, object):
        return type(object).__name__ == 'CLR Metatype' # for now

    def testAAAImportHookWorks(self):
        """Test that the import hook works correctly both using the
           included runtime and an external runtime. This must be
           the first test run in the unit tests!"""

        from CLR.System import String


    def testModuleInterface(self):
        """Test the interface exposed by CLR module objects."""
        import CLR.System as System

        self.assertEquals(type(System.__dict__), type({}))
        self.assertEquals(System.__name__, 'CLR.System')
        self.assertEquals(System.__file__, None)
        self.assertEquals(System.__doc__, None)
        self.failUnless(self.isCLRClass(System.String))
        self.failUnless(self.isCLRClass(System.Int32))


    def testSimpleImport(self):
        """Test simple import."""
        import CLR
        self.failUnless(self.isCLRModule(CLR))
        self.failUnless(CLR.__name__ == 'CLR')

        import sys
        self.failUnless(type(sys) == types.ModuleType)
        self.failUnless(sys.__name__ == 'sys')

        import httplib
        self.failUnless(type(httplib) == types.ModuleType)
        self.failUnless(httplib.__name__ == 'httplib')


    def testSimpleImportWithAlias(self):
        """Test simple import with aliasing."""
        import CLR as myCLR
        self.failUnless(self.isCLRModule(myCLR))
        self.failUnless(myCLR.__name__ == 'CLR')

        import sys as mySys
        self.failUnless(type(mySys) == types.ModuleType)
        self.failUnless(mySys.__name__ == 'sys')

        import httplib as myHttplib
        self.failUnless(type(myHttplib) == types.ModuleType)
        self.failUnless(myHttplib.__name__ == 'httplib')


    def testDottedNameImport(self):
        """Test dotted-name import."""
        import CLR.System
        self.failUnless(self.isCLRModule(CLR.System))
        self.failUnless(CLR.System.__name__ == 'CLR.System')

        import xml.dom
        self.failUnless(type(xml.dom) == types.ModuleType)
        self.failUnless(xml.dom.__name__ == 'xml.dom')


    def testDottedNameImportWithAlias(self):
        """Test dotted-name import with aliasing."""
        import CLR.System as mySystem
        self.failUnless(self.isCLRModule(mySystem))
        self.failUnless(mySystem.__name__ == 'CLR.System')

        import xml.dom as myDom
        self.failUnless(type(myDom) == types.ModuleType)
        self.failUnless(myDom.__name__ == 'xml.dom')


    def testSimpleImportFrom(self):
        """Test simple 'import from'."""
        from CLR import System
        self.failUnless(self.isCLRModule(System))
        self.failUnless(System.__name__ == 'CLR.System')

        from xml import dom
        self.failUnless(type(dom) == types.ModuleType)
        self.failUnless(dom.__name__ == 'xml.dom')


    def testSimpleImportFromWithAlias(self):
        """Test simple 'import from' with aliasing."""
        from CLR import System as mySystem
        self.failUnless(self.isCLRModule(mySystem))
        self.failUnless(mySystem.__name__ == 'CLR.System')

        from xml import dom as myDom
        self.failUnless(type(myDom) == types.ModuleType)
        self.failUnless(myDom.__name__ == 'xml.dom')


    def testDottedNameImportFrom(self):
        """Test dotted-name 'import from'."""
        from CLR.System import Xml
        self.failUnless(self.isCLRModule(Xml))
        self.failUnless(Xml.__name__ == 'CLR.System.Xml')

        from CLR.System.Xml import XmlDocument
        self.failUnless(self.isCLRClass(XmlDocument))
        self.failUnless(XmlDocument.__name__ == 'XmlDocument')

        from xml.dom import pulldom
        self.failUnless(type(pulldom) == types.ModuleType)
        self.failUnless(pulldom.__name__ == 'xml.dom.pulldom')

        from xml.dom.pulldom import PullDOM
        self.failUnless(type(PullDOM) == types.ClassType)
        self.failUnless(PullDOM.__name__ == 'PullDOM')


    def testDottedNameImportFromWithAlias(self):
        """Test dotted-name 'import from' with aliasing."""
        from CLR.System import Xml as myXml
        self.failUnless(self.isCLRModule(myXml))
        self.failUnless(myXml.__name__ == 'CLR.System.Xml')

        from CLR.System.Xml import XmlDocument as myXmlDocument
        self.failUnless(self.isCLRClass(myXmlDocument))
        self.failUnless(myXmlDocument.__name__ == 'XmlDocument')

        from xml.dom import pulldom as myPulldom
        self.failUnless(type(myPulldom) == types.ModuleType)
        self.failUnless(myPulldom.__name__ == 'xml.dom.pulldom')

        from xml.dom.pulldom import PullDOM as myPullDOM
        self.failUnless(type(myPullDOM) == types.ClassType)
        self.failUnless(myPullDOM.__name__ == 'PullDOM')


    def testFromModuleImportStar(self):
        """Test from module import * behavior."""
        # Using 'from x import *' is considered evil generally, more so
        # in this case where you may have hundreds of defined types in
        # a namespace. The intended behavior is that doing 'import *'
        # from a CLR module won't blow up, but it also won't really do
        # anything to get names into your namespace.
        m = __import__('CLR.System.Xml', globals(), locals(), ['*'])
        self.failUnless(m.__name__ == 'CLR.System.Xml')
        self.failUnless(self.isCLRModule(m))


    def testImplicitAssemblyLoad(self):
        """Test implicit assembly loading via import."""

        def test():
            # This should fail until CLR.System.Windows.Forms has been
            # imported or that assembly has been explicitly loaded.
            import CLR.System.Windows

        self.failUnlessRaises(ImportError, test)

        import CLR.System.Windows.Forms as Forms
        self.failUnless(self.isCLRModule(Forms))
        self.failUnless(Forms.__name__ == 'CLR.System.Windows.Forms')

        from CLR.System.Drawing import Graphics
        self.failUnless(self.isCLRClass(Graphics))
        self.failUnless(Graphics.__name__ == 'Graphics')


    def testExplicitAssemblyLoad(self):
        """Test explicit assembly loading using standard CLR tools."""
        from CLR.System.Reflection import Assembly
        import sys

        assembly = Assembly.LoadWithPartialName('System.Data')
        self.failUnless(assembly != None)
        
        import CLR.System.Data
        self.failUnless(sys.modules.has_key('CLR.System.Data'))

        assembly = Assembly.LoadWithPartialName('SpamSpamSpamSpamEggsAndSpam')
        self.failUnless(assembly == None)


    def testImplicitLoadAlreadyValidNamespace(self):
        """Test implicit assembly load over an already valid namespace."""
        # In this case, the mscorlib assembly (loaded by default) defines
        # a number of types in the System namespace. There is also a System
        # assembly, which is _not_ loaded by default, which also contains
        # types in the System namespace. The desired behavior is for the
        # Python runtime to "do the right thing", allowing types from both
        # assemblies to be found in the CLR.System module implicitly.
        import CLR.System
        self.failUnless(self.isCLRClass(CLR.System.UriBuilder))


    def testImportNonExistantModule(self):
        """Test import failure for a non-existant module."""
        def test():
            import CLR.System.SpamSpamSpam

        self.failUnlessRaises(ImportError, test)


    def testLookupNoNamespaceType(self):
        """Test lookup of types without a qualified namespace."""
        import CLR.Python.Test
        import CLR
        self.failUnless(self.isCLRClass(CLR.NoNamespaceType))


    def testModuleLookupRecursion(self):
        """Test for recursive lookup handling."""
        def test1():
            from CLR import CLR

        self.failUnlessRaises(ImportError, test1)

        def test2():
            import CLR
            x = CLR.CLR

        self.failUnlessRaises(AttributeError, test2)


    def testModuleGetAttr(self):
        """Test module getattr behavior."""
        import CLR.System as System

        int_type = System.Int32
        self.failUnless(self.isCLRClass(int_type))
        
        module = System.Xml
        self.failUnless(self.isCLRModule(module))

        def test():
            spam = System.Spam

        self.failUnlessRaises(AttributeError, test)

        def test():
            spam = getattr(System, 1)

        self.failUnlessRaises(TypeError, test)


    def testModuleAttrAbuse(self):
        """Test handling of attempts to set module attributes."""

        # It would be safer to use a dict-proxy as the __dict__ for CLR
        # modules, but as of Python 2.3 some parts of the CPython runtime
        # like dir() will fail if a module dict is not a real dictionary.
        
        def test():
            import CLR.System
            CLR.System.__dict__['foo'] = 0
            return 1

        self.failUnless(test())


    def testModuleTypeAbuse(self):
        """Test handling of attempts to break the module type."""
        import CLR.System as System
        mtype = type(System)

        def test():
            mtype.__getattribute__(0, 'spam')

        self.failUnlessRaises(TypeError, test)

        def test():
            mtype.__setattr__(0, 'spam', 1)

        self.failUnlessRaises(TypeError, test)

        def test():
            mtype.__repr__(0)

        self.failUnlessRaises(TypeError, test)



def test_suite():
    return unittest.makeSuite(ModuleTests)

def main():
    unittest.TextTestRunner().run(test_suite())

if __name__ == '__main__':
    testcase.setup()
    main()

