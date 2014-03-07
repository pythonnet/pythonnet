// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using Python.Runtime;

namespace Python.Test {

    //========================================================================
    // Supports units tests for exposing docstrings from C# to Python
    //========================================================================

    // Classes with a constructor have their docstring set to the ctor signature.
    // Test if a class has an explicit doc string it gets set correctly.
    [DocStringAttribute("DocWithCtorTest Class")]
    public class DocWithCtorTest {

        public DocWithCtorTest() {
        }

        [DocStringAttribute("DocWithCtorTest TestMethod")]
        public void TestMethod() {
        }

        [DocStringAttribute("DocWithCtorTest StaticTestMethod")]
        public static void StaticTestMethod() {
        }

    }

    public class DocWithCtorNoDocTest
    {
        public DocWithCtorNoDocTest(bool x) {
        }

        public void TestMethod(double a, int b) {
        }

        public static void StaticTestMethod(double a, int b) {
        }
    }

    [DocStringAttribute("DocWithoutCtorTest Class")]
    public class DocWithoutCtorTest {

        [DocStringAttribute("DocWithoutCtorTest TestMethod")]
        public void TestMethod() {
        }

        [DocStringAttribute("DocWithoutCtorTest StaticTestMethod")]
        public static void StaticTestMethod() {
        }

    }

}
