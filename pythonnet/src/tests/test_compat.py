# ===========================================================================
# This software is subject to the provisions of the Zope Public License,
# Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
# THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
# WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
# FOR A PARTICULAR PURPOSE.
# ===========================================================================

import sys, os, string, unittest, types


class CompatibilityTests(unittest.TestCase):
    """
    Backward-compatibility tests for deprecated features.
    """

    def isCLRModule(self, object):
        return type(object).__name__ == 'ModuleObject'

    def isCLRRootModule(self, object):
        return type(object).__name__ == 'CLRModule'
        
    def isCLRClass(self, object):
        return type(object).__name__ == 'CLR Metatype' # for now

    # Tests for old-style CLR-prefixed module naming.

    def testSimpleImport(self):
        """Test simple import."""
        import CLR
        self.failUnless(self.isCLRRootModule(CLR))
        self.failUnless(CLR.__name__ == 'clr')

        import sys
        self.failUnless(type(sys) == types.ModuleType)
        self.failUnless(sys.__name__ == 'sys')

        import httplib
        self.failUnless(type(httplib) == types.ModuleType)
        self.failUnless(httplib.__name__ == 'httplib')


    def testSimpleImportWithAlias(self):
        """Test simple import with aliasing."""
        import CLR as myCLR
        self.failUnless(self.isCLRRootModule(myCLR))
        self.failUnless(myCLR.__name__ == 'clr')

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
        self.failUnless(CLR.System.__name__ == 'System')
        
        import System
        self.failUnless(self.isCLRModule(System))
        self.failUnless(System.__name__ == 'System')
        
        self.failUnless(System is CLR.System)

        import xml.dom
        self.failUnless(type(xml.dom) == types.ModuleType)
        self.failUnless(xml.dom.__name__ == 'xml.dom')


    def testDottedNameImportWithAlias(self):
        """Test dotted-name import with aliasing."""
        import CLR.System as myCLRSystem
        self.failUnless(self.isCLRModule(myCLRSystem))
        self.failUnless(myCLRSystem.__name__ == 'System')

        import System as mySystem
        self.failUnless(self.isCLRModule(mySystem))
        self.failUnless(mySystem.__name__ == 'System')

        self.failUnless(mySystem is myCLRSystem)
        
        import xml.dom as myDom
        self.failUnless(type(myDom) == types.ModuleType)
        self.failUnless(myDom.__name__ == 'xml.dom')


    def testSimpleImportFrom(self):
        """Test simple 'import from'."""
        from CLR import System
        self.failUnless(self.isCLRModule(System))
        self.failUnless(System.__name__ == 'System')

        from xml import dom
        self.failUnless(type(dom) == types.ModuleType)
        self.failUnless(dom.__name__ == 'xml.dom')


    def testSimpleImportFromWithAlias(self):
        """Test simple 'import from' with aliasing."""
        from CLR import System as mySystem
        self.failUnless(self.isCLRModule(mySystem))
        self.failUnless(mySystem.__name__ == 'System')

        from xml import dom as myDom
        self.failUnless(type(myDom) == types.ModuleType)
        self.failUnless(myDom.__name__ == 'xml.dom')


    def testDottedNameImportFrom(self):
        """Test dotted-name 'import from'."""
        from CLR.System import Xml
        self.failUnless(self.isCLRModule(Xml))
        self.failUnless(Xml.__name__ == 'System.Xml')

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
        self.failUnless(myXml.__name__ == 'System.Xml')

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
        count = len(locals().keys())
        m = __import__('CLR.System.Management', globals(), locals(), ['*'])
        self.failUnless(m.__name__ == 'System.Management')
        self.failUnless(self.isCLRModule(m))
        self.failUnless(len(locals().keys()) > count + 1)

        m2 = __import__('System.Management', globals(), locals(), ['*'])
        self.failUnless(m2.__name__ == 'System.Management')
        self.failUnless(self.isCLRModule(m2))
        self.failUnless(len(locals().keys()) > count + 1)
        
        self.failUnless(m is m2)

    def testExplicitAssemblyLoad(self):
        """Test explicit assembly loading using standard CLR tools."""
        from CLR.System.Reflection import Assembly
        from CLR import System
        import sys

        assembly = Assembly.LoadWithPartialName('System.Data')
        self.failUnless(assembly != None)
        
        import CLR.System.Data
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
        # assemblies to be found in the CLR.System module implicitly.
        import CLR.System
        self.failUnless(self.isCLRClass(CLR.System.UriBuilder))


    def testImportNonExistantModule(self):
        """Test import failure for a non-existant module."""
        def test():
            import System.SpamSpamSpam

        def testclr():
            import CLR.System.SpamSpamSpam

        self.failUnlessRaises(ImportError, test)
        self.failUnlessRaises(ImportError, testclr)

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
        
    def test000MultipleImports(self):
        # import CLR did raise a Seg Fault once
        # test if the Exceptions.warn() method still causes it
        for n in range(100):
            import CLR


def test_suite():
    return unittest.makeSuite(CompatibilityTests)

def main():
    unittest.TextTestRunner().run(test_suite())

if __name__ == '__main__':
    main()

