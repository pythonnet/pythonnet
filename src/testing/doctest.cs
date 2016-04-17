using Python.Runtime;

namespace Python.Test
{
    //========================================================================
    // Supports units tests for exposing docstrings from C# to Python
    //========================================================================

    // Classes with a constructor have their docstring set to the ctor signature.
    // Test if a class has an explicit doc string it gets set correctly.
    [DocStringAttribute("DocWithCtorTest Class")]
    public class DocWithCtorTest
    {
        public DocWithCtorTest()
        {
        }

        [DocStringAttribute("DocWithCtorTest TestMethod")]
        public void TestMethod()
        {
        }

        [DocStringAttribute("DocWithCtorTest StaticTestMethod")]
        public static void StaticTestMethod()
        {
        }
    }

    public class DocWithCtorNoDocTest
    {
        public DocWithCtorNoDocTest(bool x)
        {
        }

        public void TestMethod(double a, int b)
        {
        }

        public static void StaticTestMethod(double a, int b)
        {
        }
    }

    [DocStringAttribute("DocWithoutCtorTest Class")]
    public class DocWithoutCtorTest
    {
        [DocStringAttribute("DocWithoutCtorTest TestMethod")]
        public void TestMethod()
        {
        }

        [DocStringAttribute("DocWithoutCtorTest StaticTestMethod")]
        public static void StaticTestMethod()
        {
        }
    }
}