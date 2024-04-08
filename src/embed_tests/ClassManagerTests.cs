using System;
using System.Collections.Generic;
using System.Linq;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class ClassManagerTests
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            PythonEngine.Initialize();
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            PythonEngine.Shutdown();
        }

        [Test]
        public void NestedClassDerivingFromParent()
        {
            var f = new NestedTestContainer().ToPython();
            f.GetAttr(nameof(NestedTestContainer.Bar));
        }

        #region Snake case naming tests

        public class SnakeCaseNamesTesClass
        {
            // Purposely long names to test snake case conversion

            public string PublicStringField = "public_string_field";
            public const string PublicConstStringField = "public_const_string_field";
            public readonly string PublicReadonlyStringField = "public_readonly_string_field";
            public static string PublicStaticStringField = "public_static_string_field";
            public static readonly string PublicStaticReadonlyStringField = "public_static_readonly_string_field";

            public static string SettablePublicStaticStringField = "settable_public_static_string_field";

            public string PublicStringProperty { get; set; } = "public_string_property";
            public static string PublicStaticStringProperty { get; set; } = "public_static_string_property";

            public event EventHandler<string> PublicStringEvent;
            public static event EventHandler<string> PublicStaticStringEvent;

            public void InvokePublicStringEvent(string value)
            {
                PublicStringEvent?.Invoke(this, value);
            }

            public static void InvokePublicStaticStringEvent(string value)
            {
                PublicStaticStringEvent?.Invoke(null, value);
            }

            public int AddNumbersAndGetHalf(int a, int b)
            {
                return (a + b) / 2;
            }

            public static int AddNumbersAndGetHalf_Static(int a, int b)
            {
                return (a + b) / 2;
            }

            public string JoinToString(string thisIsAStringParameter,
                char thisIsACharParameter,
                int thisIsAnIntParameter,
                float thisIsAFloatParameter,
                double thisIsADoubleParameter,
                decimal thisIsADecimalParameter,
                bool thisIsABoolParameter,
                DateTime thisIsADateTimeParameter)
            {
                // Join all parameters into a single string separated by "-"
                return string.Join("-", thisIsAStringParameter, thisIsACharParameter, thisIsAnIntParameter, thisIsAFloatParameter,
                    thisIsADoubleParameter, thisIsADecimalParameter, thisIsABoolParameter, string.Format("{0:MMddyyyy}", thisIsADateTimeParameter));
            }
        }

        [TestCase("AddNumbersAndGetHalf", "add_numbers_and_get_half")]
        [TestCase("AddNumbersAndGetHalf_Static", "add_numbers_and_get_half_static")]
        public void BindsSnakeCaseClassMethods(string originalMethodName, string snakeCaseMethodName)
        {
            using var obj = new SnakeCaseNamesTesClass().ToPython();
            using var a = 10.ToPython();
            using var b = 20.ToPython();

            var originalMethodResult = obj.InvokeMethod(originalMethodName, a, b).As<int>();
            var snakeCaseMethodResult = obj.InvokeMethod(snakeCaseMethodName, a, b).As<int>();

            Assert.AreEqual(15, originalMethodResult);
            Assert.AreEqual(originalMethodResult, snakeCaseMethodResult);
        }

        [TestCase("PublicStringField", "public_string_field")]
        [TestCase("PublicConstStringField", "public_const_string_field")]
        [TestCase("PublicReadonlyStringField", "public_readonly_string_field")]
        [TestCase("PublicStaticStringField", "public_static_string_field")]
        [TestCase("PublicStaticReadonlyStringField", "public_static_readonly_string_field")]
        public void BindsSnakeCaseClassFields(string originalFieldName, string snakeCaseFieldName)
        {
            using var obj = new SnakeCaseNamesTesClass().ToPython();

            var expectedValue = originalFieldName switch
            {
                "PublicStringField" => "public_string_field",
                "PublicConstStringField" => "public_const_string_field",
                "PublicReadonlyStringField" => "public_readonly_string_field",
                "PublicStaticStringField" => "public_static_string_field",
                "PublicStaticReadonlyStringField" => "public_static_readonly_string_field",
                _ => throw new ArgumentException("Invalid field name")
            };

            var originalFieldValue = obj.GetAttr(originalFieldName).As<string>();
            var snakeCaseFieldValue = obj.GetAttr(snakeCaseFieldName).As<string>();

            Assert.AreEqual(expectedValue, originalFieldValue);
            Assert.AreEqual(expectedValue, snakeCaseFieldValue);
        }

        [Test]
        public void CanSetFieldUsingSnakeCaseName()
        {
            var obj = new SnakeCaseNamesTesClass();
            using var pyObj = obj.ToPython();

            // Try with the original field name
            var newValue1 = "new value 1";
            using var pyNewValue1 = newValue1.ToPython();
            pyObj.SetAttr("PublicStringField", pyNewValue1);
            Assert.AreEqual(newValue1, obj.PublicStringField);

            // Try with the snake case field name
            var newValue2 = "new value 2";
            using var pyNewValue2 = newValue2.ToPython();
            pyObj.SetAttr("public_string_field", pyNewValue2);
            Assert.AreEqual(newValue2, obj.PublicStringField);
        }

        [Test]
        public void CanSetStaticFieldUsingSnakeCaseName()
        {
            using (Py.GIL())
            {
                var module = PyModule.FromString("module", $@"
from clr import AddReference
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

def SetCamelCaseStaticProperty(value):
    ClassManagerTests.SnakeCaseNamesTesClass.PublicStaticStringField = value

def SetSnakeCaseStaticProperty(value):
    ClassManagerTests.SnakeCaseNamesTesClass.public_static_string_field = value
                    ");

                // Try with the original field name
                var newValue1 = "new value 1";
                using var pyNewValue1 = newValue1.ToPython();
                module.InvokeMethod("SetCamelCaseStaticProperty", pyNewValue1);
                Assert.AreEqual(newValue1, SnakeCaseNamesTesClass.PublicStaticStringField);

                // Try with the snake case field name
                var newValue2 = "new value 2";
                using var pyNewValue2 = newValue2.ToPython();
                module.InvokeMethod("SetSnakeCaseStaticProperty", pyNewValue2);
                Assert.AreEqual(newValue2, SnakeCaseNamesTesClass.PublicStaticStringField);
            }
        }

        [TestCase("PublicStringProperty", "public_string_property")]
        [TestCase("PublicStaticStringProperty", "public_static_string_property")]
        public void BindsSnakeCaseClassProperties(string originalPropertyName, string snakeCasePropertyName)
        {
            using var obj = new SnakeCaseNamesTesClass().ToPython();
            var expectedValue = originalPropertyName switch
            {
                "PublicStringProperty" => "public_string_property",
                "PublicStaticStringProperty" => "public_static_string_property",
                _ => throw new ArgumentException("Invalid property name")
            };

            var originalPropertyValue = obj.GetAttr(originalPropertyName).As<string>();
            var snakeCasePropertyValue = obj.GetAttr(snakeCasePropertyName).As<string>();

            Assert.AreEqual(expectedValue, originalPropertyValue);
            Assert.AreEqual(expectedValue, snakeCasePropertyValue);
        }

        [Test]
        public void CanSetPropertyUsingSnakeCaseName()
        {
            var obj = new SnakeCaseNamesTesClass();
            using var pyObj = obj.ToPython();

            // Try with the original property name
            var newValue1 = "new value 1";
            using var pyNewValue1 = newValue1.ToPython();
            pyObj.SetAttr("PublicStringProperty", pyNewValue1);
            Assert.AreEqual(newValue1, obj.PublicStringProperty);

            // Try with the snake case property name
            var newValue2 = "new value 2";
            using var pyNewValue2 = newValue2.ToPython();
            pyObj.SetAttr("public_string_property", pyNewValue2);
            Assert.AreEqual(newValue2, obj.PublicStringProperty);
        }

        [Test]
        public void CanSetStaticPropertyUsingSnakeCaseName()
        {
            using (Py.GIL())
            {
                var module = PyModule.FromString("module", $@"
from clr import AddReference
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

def SetCamelCaseStaticProperty(value):
    ClassManagerTests.SnakeCaseNamesTesClass.PublicStaticStringProperty = value

def SetSnakeCaseStaticProperty(value):
    ClassManagerTests.SnakeCaseNamesTesClass.public_static_string_property = value
                    ");

                // Try with the original property name
                var newValue1 = "new value 1";
                using var pyNewValue1 = newValue1.ToPython();
                module.InvokeMethod("SetCamelCaseStaticProperty", pyNewValue1);
                Assert.AreEqual(newValue1, SnakeCaseNamesTesClass.PublicStaticStringProperty);

                // Try with the snake case property name
                var newValue2 = "new value 2";
                using var pyNewValue2 = newValue2.ToPython();
                module.InvokeMethod("SetSnakeCaseStaticProperty", pyNewValue2);
                Assert.AreEqual(newValue2, SnakeCaseNamesTesClass.PublicStaticStringProperty);
            }
        }

        [TestCase("PublicStringEvent")]
        [TestCase("public_string_event")]
        public void BindsSnakeCaseEvents(string eventName)
        {
            var obj = new SnakeCaseNamesTesClass();
            using var pyObj = obj.ToPython();

            var value = "";
            var eventHandler = new EventHandler<string>((sender, arg) => { value = arg; });

            // Try with the original event name
            using (Py.GIL())
            {
                var module = PyModule.FromString("module", $@"
def AddEventHandler(obj, handler):
    obj.{eventName} += handler

def RemoveEventHandler(obj, handler):
    obj.{eventName} -= handler
                    ");

                using var pyEventHandler = eventHandler.ToPython();

                module.InvokeMethod("AddEventHandler", pyObj, pyEventHandler);
                obj.InvokePublicStringEvent("new value 1");
                Assert.AreEqual("new value 1", value);

                module.InvokeMethod("RemoveEventHandler", pyObj, pyEventHandler);
                obj.InvokePublicStringEvent("new value 2");
                Assert.AreEqual("new value 1", value); // Should not have changed
            }
        }

        [TestCase("PublicStaticStringEvent")]
        [TestCase("public_static_string_event")]
        public void BindsSnakeCaseStaticEvents(string eventName)
        {
            var value = "";
            var eventHandler = new EventHandler<string>((sender, arg) => { value = arg; });

            // Try with the original event name
            using (Py.GIL())
            {
                var module = PyModule.FromString("module", $@"
from clr import AddReference
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

def AddEventHandler(handler):
    ClassManagerTests.SnakeCaseNamesTesClass.{eventName} += handler

def RemoveEventHandler(handler):
    ClassManagerTests.SnakeCaseNamesTesClass.{eventName} -= handler
                    ");

                using var pyEventHandler = eventHandler.ToPython();

                module.InvokeMethod("AddEventHandler", pyEventHandler);
                SnakeCaseNamesTesClass.InvokePublicStaticStringEvent("new value 1");
                Assert.AreEqual("new value 1", value);

                module.InvokeMethod("RemoveEventHandler", pyEventHandler);
                SnakeCaseNamesTesClass.InvokePublicStaticStringEvent("new value 2");
                Assert.AreEqual("new value 1", value); // Should not have changed
            }
        }

        private static IEnumerable<TestCaseData> SnakeCasedNamedArgsTestCases
        {
            get
            {
                var stringParam = "string";
                var charParam = 'c';
                var intParam = 1;
                var floatParam = 2.0f;
                var doubleParam = 3.0;
                var decimalParam = 4.0m;
                var boolParam = true;
                var dateTimeParam = new DateTime(2013, 01, 05);

                // 1. All kwargs:

                // 1.1. Original method name:
                var args = Array.Empty<object>();
                var namedArgs = new Dictionary<string, object>()
            {
                { "thisIsAStringParameter", stringParam },
                { "thisIsACharParameter", charParam },
                { "thisIsAnIntParameter", intParam },
                { "thisIsAFloatParameter", floatParam },
                { "thisIsADoubleParameter", doubleParam },
                { "thisIsADecimalParameter", decimalParam },
                { "thisIsABoolParameter", boolParam },
                { "thisIsADateTimeParameter", dateTimeParam }
            };
                yield return new TestCaseData("JoinToString", args, namedArgs);

                // 1.2. Snake-cased method name:
                namedArgs = new Dictionary<string, object>()
            {
                { "this_is_a_string_parameter", stringParam },
                { "this_is_a_char_parameter", charParam },
                { "this_is_an_int_parameter", intParam },
                { "this_is_a_float_parameter", floatParam },
                { "this_is_a_double_parameter", doubleParam },
                { "this_is_a_decimal_parameter", decimalParam },
                { "this_is_a_bool_parameter", boolParam },
                { "this_is_a_date_time_parameter", dateTimeParam }
            };
                yield return new TestCaseData("join_to_string", args, namedArgs);

                // 2. Some args and some kwargs:

                // 2.1. Original method name:
                args = new object[] { stringParam, charParam, intParam, floatParam };
                namedArgs = new Dictionary<string, object>()
            {
                { "thisIsADoubleParameter", doubleParam },
                { "thisIsADecimalParameter", decimalParam },
                { "thisIsABoolParameter", boolParam },
                { "thisIsADateTimeParameter", dateTimeParam }
            };
                yield return new TestCaseData("JoinToString", args, namedArgs);

                // 2.2. Snake-cased method name:
                namedArgs = new Dictionary<string, object>()
            {
                { "this_is_a_double_parameter", doubleParam },
                { "this_is_a_decimal_parameter", decimalParam },
                { "this_is_a_bool_parameter", boolParam },
                { "this_is_a_date_time_parameter", dateTimeParam }
            };
                yield return new TestCaseData("join_to_string", args, namedArgs);
            }
        }

        [TestCaseSource(nameof(SnakeCasedNamedArgsTestCases))]
        public void CanCallSnakeCasedMethodWithSnakeCasedNamedArguments(string methodName, object[] args, Dictionary<string, object> namedArgs)
        {
            using var obj = new SnakeCaseNamesTesClass().ToPython();

            var pyArgs = args.Select(a => a.ToPython()).ToArray();
            using var pyNamedArgs = new PyDict();
            foreach (var (key, value) in namedArgs)
            {
                pyNamedArgs[key] = value.ToPython();
            }

            var result = obj.InvokeMethod(methodName, pyArgs, pyNamedArgs).As<string>();

            Assert.AreEqual("string-c-1-2-3-4.0-True-01052013", result);
        }

        #endregion
    }

    public class NestedTestParent
    {
        public class Nested : NestedTestParent
        {
        }
    }

    public class NestedTestContainer
    {
        public NestedTestParent Bar = new NestedTestParent.Nested();
    }
}
