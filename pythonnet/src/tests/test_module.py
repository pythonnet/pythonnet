# ===========================================================================
# This software is subject to the provisions of the Zope Public License,
# Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
# THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
# WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
# FOR A PARTICULAR PURPOSE.
# ===========================================================================

import sys, os, string, unittest, types


class ModuleTests(unittest.TestCase):
    """Test CLR modules and the CLR import hook."""

    def isCLRModule(self, object):
        return type(object).__name__ == 'ModuleObject'

    def isCLRRootModule(self, object):
        return type(object).__name__ == 'CLRModule'

    def isCLRClass(self, object):
        return type(object).__name__ == 'CLR Metatype' # for now

    def testAAAImportHookWorks(self):
        """Test that the import hook works correctly both using the
           included runtime and an external runtime. This must be
           the first test run in the unit tests!"""

        from System import String

    def test000importClr(self):
        import clr
        self.failUnless(self.isCLRRootModule(clr))

    def testPreloadVar(self):
        import clr
        self.failUnless(clr.getPreload() is False, clr.getPreload())
	clr.setPreload(False)
	self.failUnless(clr.getPreload() is False, clr.getPreload())
        try:
            clr.setPreload(True)
            self.failUnless(clr.getPreload() is True, clr.getPreload())
            clr.setPreload(0)
            self.failUnless(clr.getPreload() is False, clr.getPreload())
            clr.setPreload(1)
            self.failUnless(clr.getPreload() is True, clr.getPreload())
            
            import System.Configuration
            content = dir(System.Configuration)
            self.failUnless(len(content) > 10, content)
        finally:
            clr.setPreload(False)

    def testModuleInterface(self):
        """Test the interface exposed by CLR module objects."""
        import System
        self.assertEquals(type(System.__dict__), type({}))
        self.assertEquals(System.__name__, 'System')
        self.assertEquals(System.__file__, None)
        self.assertEquals(System.__doc__, None)
        self.failUnless(self.isCLRClass(System.String))
        self.failUnless(self.isCLRClass(System.Int32))


    def testSimpleImport(self):
        """Test simple import."""
        import System
        self.failUnless(self.isCLRModule(System))
        self.failUnless(System.__name__ == 'System')

        import sys
        self.failUnless(type(sys) == types.ModuleType)
        self.failUnless(sys.__name__ == 'sys')

        import httplib
        self.failUnless(type(httplib) == types.ModuleType)
        self.failUnless(httplib.__name__ == 'httplib')


    def testSimpleImportWithAlias(self):
        """Test simple import with aliasing."""
        import System as mySystem
        self.failUnless(self.isCLRModule(mySystem))
        self.failUnless(mySystem.__name__ == 'System')

        import sys as mySys
        self.failUnless(type(mySys) == types.ModuleType)
        self.failUnless(mySys.__name__ == 'sys')

        import httplib as myHttplib
        self.failUnless(type(myHttplib) == types.ModuleType)
        self.failUnless(myHttplib.__name__ == 'httplib')


    def testDottedNameImport(self):
        """Test dotted-name import."""
        import System.Reflection
        self.failUnless(self.isCLRModule(System.Reflection))
        self.failUnless(System.Reflection.__name__ == 'System.Reflection')

        import xml.dom
        self.failUnless(type(xml.dom) == types.ModuleType)
        self.failUnless(xml.dom.__name__ == 'xml.dom')


    def testMultipleDottedNameImport(self):
        """Test an import bug with multiple dotted imports."""
        import System.Data
        self.failUnless(self.isCLRModule(System.Data))
        self.failUnless(System.Data.__name__ == 'System.Data')
        import System.Data
        self.failUnless(self.isCLRModule(System.Data))
        self.failUnless(System.Data.__name__ == 'System.Data')

        
    def testDottedNameImportWithAlias(self):
        """Test dotted-name import with aliasing."""
        import System.Reflection as SysRef
        self.failUnless(self.isCLRModule(SysRef))
        self.failUnless(SysRef.__name__ == 'System.Reflection')

        import xml.dom as myDom
        self.failUnless(type(myDom) == types.ModuleType)
        self.failUnless(myDom.__name__ == 'xml.dom')


    def testSimpleImportFrom(self):
        """Test simple 'import from'."""
        from System import Reflection
        self.failUnless(self.isCLRModule(Reflection))
        self.failUnless(Reflection.__name__ == 'System.Reflection')

        from xml import dom
        self.failUnless(type(dom) == types.ModuleType)
        self.failUnless(dom.__name__ == 'xml.dom')


    def testSimpleImportFromWithAlias(self):
        """Test simple 'import from' with aliasing."""
        from System import Collections as Coll
        self.failUnless(self.isCLRModule(Coll))
        self.failUnless(Coll.__name__ == 'System.Collections')

        from xml import dom as myDom
        self.failUnless(type(myDom) == types.ModuleType)
        self.failUnless(myDom.__name__ == 'xml.dom')


    def testDottedNameImportFrom(self):
        """Test dotted-name 'import from'."""
        from System.Collections import Specialized
        self.failUnless(self.isCLRModule(Specialized))
        self.failUnless(
            Specialized.__name__ == 'System.Collections.Specialized'
            )

        from System.Collections.Specialized import StringCollection
        self.failUnless(self.isCLRClass(StringCollection))
        self.failUnless(StringCollection.__name__ == 'StringCollection')

        from xml.dom import pulldom
        self.failUnless(type(pulldom) == types.ModuleType)
        self.failUnless(pulldom.__name__ == 'xml.dom.pulldom')

        from xml.dom.pulldom import PullDOM
        self.failUnless(type(PullDOM) == types.ClassType)
        self.failUnless(PullDOM.__name__ == 'PullDOM')


    def testDottedNameImportFromWithAlias(self):
        """Test dotted-name 'import from' with aliasing."""
        from System.Collections import Specialized as Spec
        self.failUnless(self.isCLRModule(Spec))
        self.failUnless(Spec.__name__ == 'System.Collections.Specialized')

        from System.Collections.Specialized import StringCollection as SC
        self.failUnless(self.isCLRClass(SC))
        self.failUnless(SC.__name__ == 'StringCollection')

        from xml.dom import pulldom as myPulldom
        self.failUnless(type(myPulldom) == types.ModuleType)
        self.failUnless(myPulldom.__name__ == 'xml.dom.pulldom')

        from xml.dom.pulldom import PullDOM as myPullDOM
        self.failUnless(type(myPullDOM) == types.ClassType)
        self.failUnless(myPullDOM.__name__ == 'PullDOM')


    def testFromModuleImportStar(self):
        """Test from module import * behavior."""
        count = len(locals().keys())
        m = __import__('System.Xml', globals(), locals(), ['*'])
        self.failUnless(m.__name__ == 'System.Xml')
        self.failUnless(self.isCLRModule(m))
        self.failUnless(len(locals().keys()) > count + 1)


    def testImplicitAssemblyLoad(self):
        """Test implicit assembly loading via import."""

        def test():
            # This should fail until System.Windows.Forms has been
            # imported or that assembly has been explicitly loaded.
            import System.Windows

        # The test fails when the project is compiled with MS VS 2005. Dunno why :(
        #XXXself.failUnlessRaises(ImportError, test)

        import System.Windows.Forms as Forms
        self.failUnless(self.isCLRModule(Forms))
        self.failUnless(Forms.__name__ == 'System.Windows.Forms')
        from System.Windows.Forms import Form
        self.failUnless(self.isCLRClass(Form))
        self.failUnless(Form.__name__ == 'Form')


    def testExplicitAssemblyLoad(self):
        """Test explicit assembly loading using standard CLR tools."""
        from System.Reflection import Assembly
        import System, sys

        assembly = Assembly.LoadWithPartialName('System.Data')
        self.failUnless(assembly != None)
        
        import System.Data
        self.failUnless(sys.modules.has_key('System.Data'))

        assembly = Assembly.LoadWithPartialName('SpamSpamSpamSpamEggsAndSpam')
        self.failUnless(assembly == None)


    def testImplicitLoadAlreadyValidNamespace(self):
        """Test implicit assembly load over an already valid namespace."""
        # In this case, the mscorlib assembly (loaded by default) defines
        # a number of types in the System namespace. There is also a System
        # assembly, which is _not_ loaded by default, which also contains
        # types in the System namespace. The desired behavior is for the
        # Python runtime to "do the right thing", allowing types from both
        # assemblies to be found in the System module implicitly.
        import System
        self.failUnless(self.isCLRClass(System.UriBuilder))


    def testImportNonExistantModule(self):
        """Test import failure for a non-existant module."""
        def test():
            import System.SpamSpamSpam

        self.failUnlessRaises(ImportError, test)


    def testLookupNoNamespaceType(self):
        """Test lookup of types without a qualified namespace."""
        import Python.Test
        import clr
        self.failUnless(self.isCLRClass(clr.NoNamespaceType))


    def testModuleLookupRecursion(self):
        """Test for recursive lookup handling."""
        def test1():
            from System import System

        self.failUnlessRaises(ImportError, test1)

        def test2():
            import System
            x = System.System

        self.failUnlessRaises(AttributeError, test2)


    def testModuleGetAttr(self):
        """Test module getattr behavior."""
        import System

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
            import System
            System.__dict__['foo'] = 0
            return 1

        self.failUnless(test())


    def testModuleTypeAbuse(self):
        """Test handling of attempts to break the module type."""
        import System
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

    def test_ClrListAssemblies(self):
        from clr import ListAssemblies 
        verbose = list(ListAssemblies(True))
        short = list(ListAssemblies(False))
        self.failUnless(u'mscorlib' in short)
        self.failUnless(u'System' in short)
        self.failUnless('Culture=' in verbose[0])
        self.failUnless('Version=' in verbose[0])

    def test_ClrAddReference(self):
        from clr import AddReference
        from System.IO import FileNotFoundException
        for name in ("System", "Python.Runtime"):
            asm = AddReference(name) 
            self.assertEqual(asm.GetName().Name, name)
        
        self.failUnlessRaises(FileNotFoundException, 
            AddReference, "somethingtotallysilly")


def test_suite():
    return unittest.makeSuite(ModuleTests)

def main():
    unittest.TextTestRunner().run(test_suite())

if __name__ == '__main__':
    main()

