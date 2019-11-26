using Python.Runtime;

namespace Python.Test
{
    /// <summary>
    /// Supports units tests for exposing docstrings from C# to Python
    /// </summary>
    /// <remarks>
    /// Classes with a constructor have their docstring set to the ctor signature.
    /// Test if a class has an explicit doc string it gets set correctly.
    /// </remarks>
    [DocString("DocWithCtorTest Class")]
    public class DocWithCtorTest
    {
        public DocWithCtorTest()
        {
        }

        [DocString("DocWithCtorTest TestMethod")]
        public void TestMethod()
        {
        }

        [DocString("DocWithCtorTest StaticTestMethod")]
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

    [DocString("DocWithoutCtorTest Class")]
    public class DocWithoutCtorTest
    {
        [DocString("DocWithoutCtorTest TestMethod")]
        public void TestMethod()
        {
        }

        [DocString("DocWithoutCtorTest StaticTestMethod")]
        public static void StaticTestMethod()
        {
        }
    }
}
