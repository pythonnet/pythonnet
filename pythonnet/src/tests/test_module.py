# ===========================================================================
# This software is subject to the provisions of the Zope Public License,
# Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
# THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
# WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
# FOR A PARTICULAR PURPOSE.
# ===========================================================================

import clr
clr.AddReference('Python.Test')
clr.AddReference('System.Data')

# testImplicitAssemblyLoad() passes on deprecation warning; perfect! #
##clr.AddReference('System.Windows.Forms')
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
        self.assertTrue(self.isCLRRootModule(clr))

    def testPreloadVar(self):
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

    def testModuleInterface(self):
        """Test the interface exposed by CLR module objects."""
        import System
        self.assertEquals(type(System.__dict__), type({}))
        self.assertEquals(System.__name__, 'System')
        self.assertEquals(System.__file__, None)
        self.assertEquals(System.__doc__, None)
        self.assertTrue(self.isCLRClass(System.String))
        self.assertTrue(self.isCLRClass(System.Int32))


    def testSimpleImport(self):
        """Test simple import."""
        import System
        self.assertTrue(self.isCLRModule(System))
        self.assertTrue(System.__name__ == 'System')

        import sys
        self.assertTrue(type(sys) == types.ModuleType)
        self.assertTrue(sys.__name__ == 'sys')

        import httplib
        self.assertTrue(type(httplib) == types.ModuleType)
        self.assertTrue(httplib.__name__ == 'httplib')


    def testSimpleImportWithAlias(self):
        """Test simple import with aliasing."""
        import System as mySystem
        self.assertTrue(self.isCLRModule(mySystem))
        self.assertTrue(mySystem.__name__ == 'System')

        import sys as mySys
        self.assertTrue(type(mySys) == types.ModuleType)
        self.assertTrue(mySys.__name__ == 'sys')

        import httplib as myHttplib
        self.assertTrue(type(myHttplib) == types.ModuleType)
        self.assertTrue(myHttplib.__name__ == 'httplib')


    def testDottedNameImport(self):
        """Test dotted-name import."""
        import System.Reflection
        self.assertTrue(self.isCLRModule(System.Reflection))
        self.assertTrue(System.Reflection.__name__ == 'System.Reflection')

        import xml.dom
        self.assertTrue(type(xml.dom) == types.ModuleType)
        self.assertTrue(xml.dom.__name__ == 'xml.dom')


    def testMultipleDottedNameImport(self):
        """Test an import bug with multiple dotted imports."""
        import System.Data
        self.assertTrue(self.isCLRModule(System.Data))
        self.assertTrue(System.Data.__name__ == 'System.Data')
        import System.Data
        self.assertTrue(self.isCLRModule(System.Data))
        self.assertTrue(System.Data.__name__ == 'System.Data')

        
    def testDottedNameImportWithAlias(self):
        """Test dotted-name import with aliasing."""
        import System.Reflection as SysRef
        self.assertTrue(self.isCLRModule(SysRef))
        self.assertTrue(SysRef.__name__ == 'System.Reflection')

        import xml.dom as myDom
        self.assertTrue(type(myDom) == types.ModuleType)
        self.assertTrue(myDom.__name__ == 'xml.dom')


    def testSimpleImportFrom(self):
        """Test simple 'import from'."""
        from System import Reflection
        self.assertTrue(self.isCLRModule(Reflection))
        self.assertTrue(Reflection.__name__ == 'System.Reflection')

        from xml import dom
        self.assertTrue(type(dom) == types.ModuleType)
        self.assertTrue(dom.__name__ == 'xml.dom')


    def testSimpleImportFromWithAlias(self):
        """Test simple 'import from' with aliasing."""
        from System import Collections as Coll
        self.assertTrue(self.isCLRModule(Coll))
        self.assertTrue(Coll.__name__ == 'System.Collections')

        from xml import dom as myDom
        self.assertTrue(type(myDom) == types.ModuleType)
        self.assertTrue(myDom.__name__ == 'xml.dom')


    def testDottedNameImportFrom(self):
        """Test dotted-name 'import from'."""
        from System.Collections import Specialized
        self.assertTrue(self.isCLRModule(Specialized))
        self.assertTrue(
            Specialized.__name__ == 'System.Collections.Specialized'
            )

        from System.Collections.Specialized import StringCollection
        self.assertTrue(self.isCLRClass(StringCollection))
        self.assertTrue(StringCollection.__name__ == 'StringCollection')

        from xml.dom import pulldom
        self.assertTrue(type(pulldom) == types.ModuleType)
        self.assertTrue(pulldom.__name__ == 'xml.dom.pulldom')

        from xml.dom.pulldom import PullDOM
        self.assertTrue(type(PullDOM) == types.ClassType)
        self.assertTrue(PullDOM.__name__ == 'PullDOM')


    def testDottedNameImportFromWithAlias(self):
        """Test dotted-name 'import from' with aliasing."""
        from System.Collections import Specialized as Spec
        self.assertTrue(self.isCLRModule(Spec))
        self.assertTrue(Spec.__name__ == 'System.Collections.Specialized')

        from System.Collections.Specialized import StringCollection as SC
        self.assertTrue(self.isCLRClass(SC))
        self.assertTrue(SC.__name__ == 'StringCollection')

        from xml.dom import pulldom as myPulldom
        self.assertTrue(type(myPulldom) == types.ModuleType)
        self.assertTrue(myPulldom.__name__ == 'xml.dom.pulldom')

        from xml.dom.pulldom import PullDOM as myPullDOM
        self.assertTrue(type(myPullDOM) == types.ClassType)
        self.assertTrue(myPullDOM.__name__ == 'PullDOM')


    def testFromModuleImportStar(self):
        """Test from module import * behavior."""
        count = len(locals().keys())
        m = __import__('System.Xml', globals(), locals(), ['*'])
        self.assertTrue(m.__name__ == 'System.Xml')
        self.assertTrue(self.isCLRModule(m))
        self.assertTrue(len(locals().keys()) > count + 1)


    def testImplicitAssemblyLoad(self):
        """Test implicit assembly loading via import."""

        def test():
            # This should fail until System.Windows.Forms has been
            # imported or that assembly has been explicitly loaded.
            import System.Windows

        # The test fails when the project is compiled with MS VS 2005. Dunno why :(
        self.assertRaises(ImportError, test)

        clr.AddReference("System.Windows.Forms")
        import System.Windows.Forms as Forms
        self.assertTrue(self.isCLRModule(Forms))
        self.assertTrue(Forms.__name__ == 'System.Windows.Forms')
        from System.Windows.Forms import Form
        self.assertTrue(self.isCLRClass(Form))
        self.assertTrue(Form.__name__ == 'Form')


    def testExplicitAssemblyLoad(self):
        """Test explicit assembly loading using standard CLR tools."""
        from System.Reflection import Assembly
        import System, sys

        assembly = Assembly.LoadWithPartialName('System.Data')
        self.assertTrue(assembly != None)
        
        import System.Data
        self.assertTrue(sys.modules.has_key('System.Data'))

        assembly = Assembly.LoadWithPartialName('SpamSpamSpamSpamEggsAndSpam')
        self.assertTrue(assembly == None)


    def testImplicitLoadAlreadyValidNamespace(self):
        """Test implicit assembly load over an already valid namespace."""
        # In this case, the mscorlib assembly (loaded by default) defines
        # a number of types in the System namespace. There is also a System
        # assembly, which is _not_ loaded by default, which also contains
        # types in the System namespace. The desired behavior is for the
        # Python runtime to "do the right thing", allowing types from both
        # assemblies to be found in the System module implicitly.
        import System
        self.assertTrue(self.isCLRClass(System.UriBuilder))


    def testImportNonExistantModule(self):
        """Test import failure for a non-existant module."""
        def test():
            import System.SpamSpamSpam

        self.assertTrue(ImportError, test)


    def testLookupNoNamespaceType(self):
        """Test lookup of types without a qualified namespace."""
        import Python.Test
        import clr
        self.assertTrue(self.isCLRClass(clr.NoNamespaceType))


    def testModuleLookupRecursion(self):
        """Test for recursive lookup handling."""
        def test1():
            from System import System

        self.assertTrue(ImportError, test1)

        def test2():
            import System
            x = System.System

        self.assertTrue(AttributeError, test2)


    def testModuleGetAttr(self):
        """Test module getattr behavior."""
        import System

        int_type = System.Int32
        self.assertTrue(self.isCLRClass(int_type))
        
        module = System.Xml
        self.assertTrue(self.isCLRModule(module))

        def test():
            spam = System.Spam

        self.assertTrue(AttributeError, test)

        def test():
            spam = getattr(System, 1)

        self.assertTrue(TypeError, test)


    def testModuleAttrAbuse(self):
        """Test handling of attempts to set module attributes."""

        # It would be safer to use a dict-proxy as the __dict__ for CLR
        # modules, but as of Python 2.3 some parts of the CPython runtime
        # like dir() will fail if a module dict is not a real dictionary.
        
        def test():
            import System
            System.__dict__['foo'] = 0
            return 1

        self.assertTrue(test())


    def testModuleTypeAbuse(self):
        """Test handling of attempts to break the module type."""
        import System
        mtype = type(System)

        def test():
            mtype.__getattribute__(0, 'spam')

        self.assertTrue(TypeError, test)

        def test():
            mtype.__setattr__(0, 'spam', 1)

        self.assertTrue(TypeError, test)

        def test():
            mtype.__repr__(0)

        self.assertTrue(TypeError, test)

    def test_ClrListAssemblies(self):
        from clr import ListAssemblies 
        verbose = list(ListAssemblies(True))
        short = list(ListAssemblies(False))
        self.assertTrue(u'mscorlib' in short)
        self.assertTrue(u'System' in short)
        self.assertTrue('Culture=' in verbose[0])
        self.assertTrue('Version=' in verbose[0])

    def test_ClrAddReference(self):
        from clr import AddReference
        from System.IO import FileNotFoundException
        for name in ("System", "Python.Runtime"):
            mod = AddReference(name) 
            self.assertEqual(mod.__name__, name)
        
        self.assertRaises(FileNotFoundException, 
            AddReference, "somethingtotallysilly")


def test_suite():
    return unittest.makeSuite(ModuleTests)

def main():
    unittest.TextTestRunner().run(test_suite())

if __name__ == '__main__':
    main()

