using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    [TestFixture]
    public class TestUtil
    {
        [TestCase("TestCamelCaseString", "test_camel_case_string")]
        [TestCase("testCamelCaseString", "test_camel_case_string")]
        [TestCase("TestCamelCaseString123 ", "test_camel_case_string123")]
        [TestCase("_testCamelCaseString123", "_test_camel_case_string123")]
        [TestCase("TestCCS", "test_ccs")]
        [TestCase("testCCS", "test_ccs")]
        [TestCase("CCSTest", "ccs_test")]
        [TestCase("test_CamelCaseString", "test_camel_case_string")]
        public void ConvertsNameToSnakeCase(string name, string expected)
        {
            Assert.AreEqual(expected, name.ToSnakeCase());
        }
    }
}
