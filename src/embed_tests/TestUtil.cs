using System.Reflection;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    [TestFixture]
    public class TestUtil
    {
        private static BindingFlags _bindingFlags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

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

        [TestCase("TestNonConstField1", "test_non_const_field1")]
        [TestCase("TestNonConstField2", "test_non_const_field2")]
        [TestCase("TestNonConstField3", "test_non_const_field3")]
        [TestCase("TestNonConstField4", "test_non_const_field4")]
        public void ConvertsNonConstantFieldsToSnakeCase(string fieldName, string expected)
        {
            var fi = typeof(TestClass).GetField(fieldName, _bindingFlags);
            Assert.AreEqual(expected, fi.ToSnakeCase());
        }

        [TestCase("TestConstField1", "TEST_CONST_FIELD1")]
        [TestCase("TestConstField2", "TEST_CONST_FIELD2")]
        [TestCase("TestConstField3", "TEST_CONST_FIELD3")]
        [TestCase("TestConstField4", "TEST_CONST_FIELD4")]
        public void ConvertsConstantFieldsToFullCapitalCase(string fieldName, string expected)
        {
            var fi = typeof(TestClass).GetField(fieldName, _bindingFlags);
            Assert.AreEqual(expected, fi.ToSnakeCase());
        }

        [TestCase("TestNonConstProperty1", "test_non_const_property1")]
        [TestCase("TestNonConstProperty2", "test_non_const_property2")]
        [TestCase("TestNonConstProperty3", "test_non_const_property3")]
        [TestCase("TestNonConstProperty4", "test_non_const_property4")]
        [TestCase("TestNonConstProperty5", "test_non_const_property5")]
        [TestCase("TestNonConstProperty6", "test_non_const_property6")]
        [TestCase("TestNonConstProperty7", "test_non_const_property7")]
        [TestCase("TestNonConstProperty8", "test_non_const_property8")]
        [TestCase("TestNonConstProperty9", "test_non_const_property9")]
        [TestCase("TestNonConstProperty10", "test_non_const_property10")]
        [TestCase("TestNonConstProperty11", "test_non_const_property11")]
        [TestCase("TestNonConstProperty12", "test_non_const_property12")]
        [TestCase("TestNonConstProperty13", "test_non_const_property13")]
        [TestCase("TestNonConstProperty14", "test_non_const_property14")]
        [TestCase("TestNonConstProperty15", "test_non_const_property15")]
        [TestCase("TestNonConstProperty16", "test_non_const_property16")]
        public void ConvertsNonConstantPropertiesToSnakeCase(string propertyName, string expected)
        {
            var pi = typeof(TestClass).GetProperty(propertyName, _bindingFlags);
            Assert.AreEqual(expected, pi.ToSnakeCase());
        }

        [TestCase("TestConstProperty1", "TEST_CONST_PROPERTY1")]
        [TestCase("TestConstProperty2", "TEST_CONST_PROPERTY2")]
        [TestCase("TestConstProperty3", "TEST_CONST_PROPERTY3")]
        public void ConvertsConstantPropertiesToFullCapitalCase(string propertyName, string expected)
        {
            var pi = typeof(TestClass).GetProperty(propertyName, _bindingFlags);
            Assert.AreEqual(expected, pi.ToSnakeCase());
        }

        private class TestClass
        {
            public string TestNonConstField1 = "TestNonConstField1";
            protected string TestNonConstField2 = "TestNonConstField2";
            public static string TestNonConstField3 = "TestNonConstField3";
            protected static string TestNonConstField4 = "TestNonConstField4";

            public const string TestConstField1 = "TestConstField1";
            protected const string TestConstField2 = "TestConstField2";
            public static readonly string TestConstField3 = "TestConstField3";
            protected static readonly string TestConstField4 = "TestConstField4";

            public string TestNonConstProperty1 { get; set; } = "TestNonConstProperty1";
            protected string TestNonConstProperty2 { get; set; } = "TestNonConstProperty2";
            public string TestNonConstProperty3 { get; } = "TestNonConstProperty3";
            protected string TestNonConstProperty4 { get; } = "TestNonConstProperty4";
            public string TestNonConstProperty5 { get; private set; } = "TestNonConstProperty5";
            protected string TestNonConstProperty6 { get; private set; } = "TestNonConstProperty6";
            public string TestNonConstProperty7 { get; protected set; } = "TestNonConstProperty7";
            public string TestNonConstProperty8 { get; internal set; } = "TestNonConstProperty8";
            public string TestNonConstProperty9 { get; protected internal set; } = "TestNonConstProperty9";
            public static string TestNonConstProperty10 { get; set; } = "TestNonConstProperty10";
            protected static string TestNonConstProperty11 { get; set; } = "TestNonConstProperty11";
            public static string TestNonConstProperty12 { get; private set; } = "TestNonConstProperty12";
            protected static string TestNonConstProperty13 { get; private set; } = "TestNonConstProperty13";
            public static string TestNonConstProperty14 { get; protected set; } = "TestNonConstProperty14";
            public static string TestNonConstProperty15 { get; internal set; } = "TestNonConstProperty15";
            public static string TestNonConstProperty16 { get; protected internal set; } = "TestNonConstProperty16";


            public static string TestConstProperty1 => "TestConstProperty1";
            public static string TestConstProperty2 { get; } = "TestConstProperty2";
            protected static string TestConstProperty3 { get; } = "TestConstProperty3";
        }
    }
}
