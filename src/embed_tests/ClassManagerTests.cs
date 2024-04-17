using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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

        public enum SnakeCaseEnum
        {
            EnumValue1,
            EnumValue2,
            EnumValue3
        }

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
            public string PublicStringGetOnlyProperty { get; } = "public_string_get_only_property";
            public static string PublicStaticStringProperty { get; set; } = "public_static_string_property";
            public static string PublicStaticReadonlyStringGetterOnlyProperty { get; } = "public_static_readonly_string_getter_only_property";
            public static string PublicStaticReadonlyStringPrivateSetterProperty { get; private set; } = "public_static_readonly_string_private_setter_property";
            public static string PublicStaticReadonlyStringProtectedSetterProperty { get; protected set; } = "public_static_readonly_string_protected_setter_property";
            public static string PublicStaticReadonlyStringInternalSetterProperty { get; internal set; } = "public_static_readonly_string_internal_setter_property";
            public static string PublicStaticReadonlyStringProtectedInternalSetterProperty { get; protected internal set; } = "public_static_readonly_string_protected_internal_setter_property";
            public static string PublicStaticReadonlyStringExpressionBodiedProperty => "public_static_readonly_string_expression_bodied_property";

            protected string ProtectedStringGetOnlyProperty { get; } = "protected_string_get_only_property";
            protected static string ProtectedStaticStringProperty { get; set; } = "protected_static_string_property";
            protected static string ProtectedStaticReadonlyStringGetterOnlyProperty { get; } = "protected_static_readonly_string_getter_only_property";
            protected static string ProtectedStaticReadonlyStringPrivateSetterProperty { get; private set; } = "protected_static_readonly_string_private_setter_property";
            protected static string ProtectedStaticReadonlyStringExpressionBodiedProperty => "protected_static_readonly_string_expression_bodied_property";

            public event EventHandler<string> PublicStringEvent;
            public static event EventHandler<string> PublicStaticStringEvent;

            public SnakeCaseEnum EnumValue = SnakeCaseEnum.EnumValue2;

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
                decimal? thisIsADecimalParameter,
                bool thisIsABoolParameter,
                DateTime thisIsADateTimeParameter = default)
            {
                // Join all parameters into a single string separated by "-"
                return string.Join("-", thisIsAStringParameter, thisIsACharParameter, thisIsAnIntParameter, thisIsAFloatParameter,
                    thisIsADoubleParameter, thisIsADecimalParameter ?? 123.456m, thisIsABoolParameter, string.Format("{0:MMddyyyy}", thisIsADateTimeParameter));
            }

            public static Action StaticReadonlyActionProperty { get; } = () => Throw();
            public static Action<int> StaticReadonlyActionWithParamsProperty { get; } = (i) => Throw();
            public static Func<int> StaticReadonlyFuncProperty { get; } = () =>
            {
                Throw();
                return 42;
            };
            public static Func<int, int> StaticReadonlyFuncWithParamsProperty { get; } = (i) =>
            {
                Throw();
                return i * 2;
            };

            public static Action StaticReadonlyExpressionBodiedActionProperty => () => Throw();
            public static Action<int> StaticReadonlyExpressionBodiedActionWithParamsProperty => (i) => Throw();
            public static Func<int> StaticReadonlyExpressionBodiedFuncProperty => () =>
            {
                Throw();
                return 42;
            };
            public static Func<int, int> StaticReadonlyExpressionBodiedFuncWithParamsProperty => (i) =>
            {
                Throw();
                return i * 2;
            };

            public static readonly Action StaticReadonlyActionField = () => Throw();
            public static readonly Action<int> StaticReadonlyActionWithParamsField = (i) => Throw();
            public static readonly Func<int> StaticReadonlyFuncField = () =>
            {
                Throw();
                return 42;
            };
            public static readonly Func<int, int> StaticReadonlyFuncWithParamsField = (i) =>
            {
                Throw();
                return i * 2;
            };

            public static readonly Action StaticReadonlyExpressionBodiedActionField = () => Throw();
            public static readonly Action<int> StaticReadonlyExpressionBodiedActionWithParamsField = (i) => Throw();
            public static readonly Func<int> StaticReadonlyExpressionBodiedFuncField = () =>
            {
                Throw();
                return 42;
            };
            public static readonly Func<int, int> StaticReadonlyExpressionBodiedFuncWithParamsField = (i) =>
            {
                Throw();
                return i * 2;
            };

            private static void Throw() => throw new Exception("Pepe");

            public static string GenericMethodBindingStatic<T>(int arg1, SnakeCaseEnum enumValue)
            {
                return "GenericMethodBindingStatic";
            }

            public string GenericMethodBinding<T>(int arg1, SnakeCaseEnum enumValue = SnakeCaseEnum.EnumValue3)
            {
                return "GenericMethodBinding" + arg1;
            }
        }

        [TestCase("generic_method_binding_static", "GenericMethodBindingStatic")]
        [TestCase("generic_method_binding", "GenericMethodBinding1")]
        [TestCase("generic_method_binding2", "GenericMethodBinding2")]
        [TestCase("generic_method_binding3", "GenericMethodBinding3")]
        public void GenericMethodBinding(string targetMethod, string expectedReturn)
        {
            using (Py.GIL())
            {
                var module = PyModule.FromString("module", $@"
from clr import AddReference
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

def generic_method_binding_static(value):
    return ClassManagerTests.SnakeCaseNamesTesClass.generic_method_binding_static[bool](1, enum_value=ClassManagerTests.SnakeCaseEnum.EnumValue1)

def generic_method_binding(value):
    return value.generic_method_binding[bool](1, enum_value=ClassManagerTests.SnakeCaseEnum.EnumValue1)

def generic_method_binding2(value):
    return value.generic_method_binding[bool](2, ClassManagerTests.SnakeCaseEnum.EnumValue1)

def generic_method_binding3(value):
    return value.generic_method_binding[bool](3)
                    ");

                using var obj = new SnakeCaseNamesTesClass().ToPython();
                var result = module.InvokeMethod(targetMethod, new[] { obj }).As<string>();

                Assert.AreEqual(expectedReturn, result);
            }
        }

        [TestCase("StaticReadonlyActionProperty", "static_readonly_action_property", new object[] { })]
        [TestCase("StaticReadonlyActionWithParamsProperty", "static_readonly_action_with_params_property", new object[] { 42 })]
        [TestCase("StaticReadonlyFuncProperty", "static_readonly_func_property", new object[] { })]
        [TestCase("StaticReadonlyFuncWithParamsProperty", "static_readonly_func_with_params_property", new object[] { 42 })]
        [TestCase("StaticReadonlyExpressionBodiedActionProperty", "static_readonly_expression_bodied_action_property", new object[] { })]
        [TestCase("StaticReadonlyExpressionBodiedActionWithParamsProperty", "static_readonly_expression_bodied_action_with_params_property", new object[] { 42 })]
        [TestCase("StaticReadonlyExpressionBodiedFuncProperty", "static_readonly_expression_bodied_func_property", new object[] { })]
        [TestCase("StaticReadonlyExpressionBodiedFuncWithParamsProperty", "static_readonly_expression_bodied_func_with_params_property", new object[] { 42 })]
        [TestCase("StaticReadonlyActionField", "static_readonly_action_field", new object[] { })]
        [TestCase("StaticReadonlyActionWithParamsField", "static_readonly_action_with_params_field", new object[] { 42 })]
        [TestCase("StaticReadonlyFuncField", "static_readonly_func_field", new object[] { })]
        [TestCase("StaticReadonlyFuncWithParamsField", "static_readonly_func_with_params_field", new object[] { 42 })]
        [TestCase("StaticReadonlyExpressionBodiedActionField", "static_readonly_expression_bodied_action_field", new object[] { })]
        [TestCase("StaticReadonlyExpressionBodiedActionWithParamsField", "static_readonly_expression_bodied_action_with_params_field", new object[] { 42 })]
        [TestCase("StaticReadonlyExpressionBodiedFuncField", "static_readonly_expression_bodied_func_field", new object[] { })]
        [TestCase("StaticReadonlyExpressionBodiedFuncWithParamsField", "static_readonly_expression_bodied_func_with_params_field", new object[] { 42 })]
        public void StaticReadonlyCallableFieldsAndPropertiesAreBothUpperAndLowerCased(string propertyName, string snakeCasedName, object[] args)
        {
            using var obj = new SnakeCaseNamesTesClass().ToPython();

            var lowerCasedName = snakeCasedName.ToLowerInvariant();
            var upperCasedName = snakeCasedName.ToUpperInvariant();

            var memberInfo = typeof(SnakeCaseNamesTesClass).GetMember(propertyName).First();
            var callableType = memberInfo switch
            {
                PropertyInfo propertyInfo => propertyInfo.PropertyType,
                FieldInfo fieldInfo => fieldInfo.FieldType,
                _ => throw new InvalidOperationException()
            };

            var property = obj.GetAttr(propertyName).AsManagedObject(callableType);
            var lowerCasedProperty = obj.GetAttr(lowerCasedName).AsManagedObject(callableType);
            var upperCasedProperty = obj.GetAttr(upperCasedName).AsManagedObject(callableType);

            Assert.IsNotNull(property);
            Assert.IsNotNull(property as MulticastDelegate);
            Assert.AreSame(property, lowerCasedProperty);
            Assert.AreSame(property, upperCasedProperty);

            var call = () =>
            {
                try
                {
                    (property as Delegate).DynamicInvoke(args);
                }
                catch (TargetInvocationException e)
                {
                    throw e.InnerException;
                }
            };

            var exception = Assert.Throws<Exception>(() => call());
            Assert.AreEqual("Pepe", exception.Message);
        }

        [TestCase("PublicStaticReadonlyStringField", "public_static_readonly_string_field")]
        [TestCase("PublicStaticReadonlyStringGetterOnlyProperty", "public_static_readonly_string_getter_only_property")]
        public void NonCallableStaticReadonlyFieldsAndPropertiesAreOnlyUpperCased(string propertyName, string snakeCasedName)
        {
            using var obj = new SnakeCaseNamesTesClass().ToPython();
            var lowerCasedName = snakeCasedName.ToLowerInvariant();
            var upperCasedName = snakeCasedName.ToUpperInvariant();

            Assert.IsTrue(obj.HasAttr(propertyName));
            Assert.IsTrue(obj.HasAttr(upperCasedName));
            Assert.IsFalse(obj.HasAttr(lowerCasedName));
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
        [TestCase("PublicStaticStringField", "public_static_string_field")]
        [TestCase("PublicReadonlyStringField", "public_readonly_string_field")]
        // Constants
        [TestCase("PublicConstStringField", "PUBLIC_CONST_STRING_FIELD")]
        [TestCase("PublicStaticReadonlyStringField", "PUBLIC_STATIC_READONLY_STRING_FIELD")]
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
        [TestCase("PublicStringGetOnlyProperty", "public_string_get_only_property")]
        [TestCase("PublicStaticStringProperty", "public_static_string_property")]
        [TestCase("PublicStaticReadonlyStringPrivateSetterProperty", "public_static_readonly_string_private_setter_property")]
        [TestCase("PublicStaticReadonlyStringProtectedSetterProperty", "public_static_readonly_string_protected_setter_property")]
        [TestCase("PublicStaticReadonlyStringInternalSetterProperty", "public_static_readonly_string_internal_setter_property")]
        [TestCase("PublicStaticReadonlyStringProtectedInternalSetterProperty", "public_static_readonly_string_protected_internal_setter_property")]
        [TestCase("ProtectedStringGetOnlyProperty", "protected_string_get_only_property")]
        [TestCase("ProtectedStaticStringProperty", "protected_static_string_property")]
        [TestCase("ProtectedStaticReadonlyStringPrivateSetterProperty", "protected_static_readonly_string_private_setter_property")]
        // Constants
        [TestCase("PublicStaticReadonlyStringGetterOnlyProperty", "PUBLIC_STATIC_READONLY_STRING_GETTER_ONLY_PROPERTY")]
        [TestCase("PublicStaticReadonlyStringExpressionBodiedProperty", "PUBLIC_STATIC_READONLY_STRING_EXPRESSION_BODIED_PROPERTY")]
        [TestCase("ProtectedStaticReadonlyStringGetterOnlyProperty", "PROTECTED_STATIC_READONLY_STRING_GETTER_ONLY_PROPERTY")]
        [TestCase("ProtectedStaticReadonlyStringExpressionBodiedProperty", "PROTECTED_STATIC_READONLY_STRING_EXPRESSION_BODIED_PROPERTY")]

        public void BindsSnakeCaseClassProperties(string originalPropertyName, string snakeCasePropertyName)
        {
            using var obj = new SnakeCaseNamesTesClass().ToPython();
            var expectedValue = originalPropertyName switch
            {
                "PublicStringProperty" => "public_string_property",
                "PublicStringGetOnlyProperty" => "public_string_get_only_property",
                "PublicStaticStringProperty" => "public_static_string_property",
                "PublicStaticReadonlyStringPrivateSetterProperty" => "public_static_readonly_string_private_setter_property",
                "PublicStaticReadonlyStringProtectedSetterProperty" => "public_static_readonly_string_protected_setter_property",
                "PublicStaticReadonlyStringInternalSetterProperty" => "public_static_readonly_string_internal_setter_property",
                "PublicStaticReadonlyStringProtectedInternalSetterProperty" => "public_static_readonly_string_protected_internal_setter_property",
                "PublicStaticReadonlyStringGetterOnlyProperty" => "public_static_readonly_string_getter_only_property",
                "PublicStaticReadonlyStringExpressionBodiedProperty" => "public_static_readonly_string_expression_bodied_property",
                "ProtectedStringGetOnlyProperty" => "protected_string_get_only_property",
                "ProtectedStaticStringProperty" => "protected_static_string_property",
                "ProtectedStaticReadonlyStringGetterOnlyProperty" => "protected_static_readonly_string_getter_only_property",
                "ProtectedStaticReadonlyStringPrivateSetterProperty" => "protected_static_readonly_string_private_setter_property",
                "ProtectedStaticReadonlyStringExpressionBodiedProperty" => "protected_static_readonly_string_expression_bodied_property",
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
                var expectedResult = "string-c-1-2-3-4.0-True-01052013";
                yield return new TestCaseData("JoinToString", args, namedArgs, expectedResult);

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
                yield return new TestCaseData("join_to_string", args, namedArgs, expectedResult);

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
                yield return new TestCaseData("JoinToString", args, namedArgs, expectedResult);

                // 2.2. Snake-cased method name:
                namedArgs = new Dictionary<string, object>()
                {
                    { "this_is_a_double_parameter", doubleParam },
                    { "this_is_a_decimal_parameter", decimalParam },
                    { "this_is_a_bool_parameter", boolParam },
                    { "this_is_a_date_time_parameter", dateTimeParam }
                };
                yield return new TestCaseData("join_to_string", args, namedArgs, expectedResult);

                // 3. Nullable args:
                namedArgs = new Dictionary<string, object>()
                {
                    { "thisIsADoubleParameter", doubleParam },
                    { "thisIsADecimalParameter", null },
                    { "thisIsABoolParameter", boolParam },
                    { "thisIsADateTimeParameter", dateTimeParam }
                };
                expectedResult = "string-c-1-2-3-123.456-True-01052013";
                yield return new TestCaseData("JoinToString", args, namedArgs, expectedResult);

                // 4. Parameters with default values:
                namedArgs = new Dictionary<string, object>()
                {
                    { "this_is_a_double_parameter", doubleParam },
                    { "this_is_a_decimal_parameter", decimalParam },
                    { "this_is_a_bool_parameter", boolParam },
                    // Purposefully omitting the DateTime parameter so the default value is used
                };
                expectedResult = "string-c-1-2-3-4.0-True-01010001";
                yield return new TestCaseData("join_to_string", args, namedArgs, expectedResult);
            }
        }

        [TestCaseSource(nameof(SnakeCasedNamedArgsTestCases))]
        public void CanCallSnakeCasedMethodWithSnakeCasedNamedArguments(string methodName, object[] args, Dictionary<string, object> namedArgs,
            string expectedResult)
        {
            using var obj = new SnakeCaseNamesTesClass().ToPython();

            var pyArgs = args.Select(a => a.ToPython()).ToArray();
            using var pyNamedArgs = new PyDict();
            foreach (var (key, value) in namedArgs)
            {
                pyNamedArgs[key] = value.ToPython();
            }

            var result = obj.InvokeMethod(methodName, pyArgs, pyNamedArgs).As<string>();

            Assert.AreEqual(expectedResult, result);
        }

        [Test]
        public void BindsEnumValuesWithPEPStyleNaming([Values] bool useSnakeCased)
        {
            using (Py.GIL())
            {
                var module = PyModule.FromString("module", $@"
from clr import AddReference
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

def SetEnumValue1(obj):
    obj.EnumValue = ClassManagerTests.SnakeCaseEnum.EnumValue1

def SetEnumValue2(obj):
    obj.EnumValue = ClassManagerTests.SnakeCaseEnum.EnumValue2

def SetEnumValue3(obj):
    obj.EnumValue = ClassManagerTests.SnakeCaseEnum.EnumValue3

def SetEnumValue1SnakeCase(obj):
    obj.enum_value = ClassManagerTests.SnakeCaseEnum.ENUM_VALUE_1

def SetEnumValue2SnakeCase(obj):
    obj.enum_value = ClassManagerTests.SnakeCaseEnum.ENUM_VALUE_2

def SetEnumValue3SnakeCase(obj):
    obj.enum_value = ClassManagerTests.SnakeCaseEnum.ENUM_VALUE_3
                    ");

                using var obj = new SnakeCaseNamesTesClass().ToPython();

                if (useSnakeCased)
                {
                    module.InvokeMethod("SetEnumValue1SnakeCase", obj);
                    Assert.AreEqual(SnakeCaseEnum.EnumValue1, obj.GetAttr("enum_value").As<SnakeCaseEnum>());
                    module.InvokeMethod("SetEnumValue2SnakeCase", obj);
                    Assert.AreEqual(SnakeCaseEnum.EnumValue2, obj.GetAttr("enum_value").As<SnakeCaseEnum>());
                    module.InvokeMethod("SetEnumValue3SnakeCase", obj);
                    Assert.AreEqual(SnakeCaseEnum.EnumValue3, obj.GetAttr("enum_value").As<SnakeCaseEnum>());
                }
                else
                {
                    module.InvokeMethod("SetEnumValue1", obj);
                    Assert.AreEqual(SnakeCaseEnum.EnumValue1, obj.GetAttr("EnumValue").As<SnakeCaseEnum>());
                    module.InvokeMethod("SetEnumValue2", obj);
                    Assert.AreEqual(SnakeCaseEnum.EnumValue2, obj.GetAttr("EnumValue").As<SnakeCaseEnum>());
                    module.InvokeMethod("SetEnumValue3", obj);
                    Assert.AreEqual(SnakeCaseEnum.EnumValue3, obj.GetAttr("EnumValue").As<SnakeCaseEnum>());
                }
            }
        }

        private class AlreadyDefinedSnakeCaseMemberTestBaseClass
        {
            private int private_field = 123;
            public int PrivateField = 333;

            public virtual int SomeIntProperty { get; set; } = 123;

            public int some_int_property { get; set; } = 321;

            public virtual int AnotherIntProperty { get; set; } = 456;

            public int another_int_property()
            {
                return 654;
            }

            public dynamic a(AlreadyDefinedSnakeCaseMemberTestBaseClass a)
            {
                throw new Exception("a(AlreadyDefinedSnakeCaseMemberTestBaseClass)");
            }

            public int a()
            {
                throw new Exception("a()");
            }

            public int get_value()
            {
                throw new Exception("get_value()");
            }

            public virtual int get_value(int x)
            {
                throw new Exception("get_value(int x)");
            }

            public virtual int get_value_2(int x)
            {
                throw new Exception("get_value_2(int x)");
            }

            public int get_value_3(int x)
            {
                throw new Exception("get_value_3(int x)");
            }

            public int GetValue(int x)
            {
                throw new Exception("GetValue(int x)");
            }

            public virtual int GetValue(int x, int y)
            {
                throw new Exception("GetValue(int x, int y)");
            }

            public virtual int GetValue2(int x)
            {
                throw new Exception("GetValue2(int x)");
            }

            public int GetValue3(int x)
            {
                throw new Exception("GetValue3(int x)");
            }
        }

        private class AlreadyDefinedSnakeCaseMemberTestDerivedClass : AlreadyDefinedSnakeCaseMemberTestBaseClass
        {
            public int SomeIntProperty { get; set; } = 111;

            public override int AnotherIntProperty { get; set; } = 222;

            public int A()
            {
                throw new Exception("A()");
            }
            public PyObject A(PyObject a)
            {
                throw new Exception("A(PyObject)");
            }
            public override int get_value(int x)
            {
                throw new Exception("override get_value(int x)");
            }

            public override int GetValue(int x, int y)
            {
                throw new Exception("override GetValue(int x, int y)");
            }

            public override int GetValue2(int x)
            {
                throw new Exception("override GetValue2(int x)");
            }

            public new int GetValue3(int x)
            {
                throw new Exception("new GetValue3(int x)");
            }
        }

        [TestCase(typeof(AlreadyDefinedSnakeCaseMemberTestBaseClass), "get_value", new object[] { 2, 3 }, "GetValue(int x, int y)")]
        // 1 int arg, binds to the original c# class get_value(int x)
        [TestCase(typeof(AlreadyDefinedSnakeCaseMemberTestBaseClass), "get_value", new object[] { 2 }, "get_value(int x)")]
        // 2 int args, binds to the snake-cased overriden GetValue(int x, int y)
        [TestCase(typeof(AlreadyDefinedSnakeCaseMemberTestDerivedClass), "get_value", new object[] { 2, 3 }, "override GetValue(int x, int y)")]
        [TestCase(typeof(AlreadyDefinedSnakeCaseMemberTestDerivedClass), "get_value", new object[] { 2 }, "override get_value(int x)")]
        [TestCase(typeof(AlreadyDefinedSnakeCaseMemberTestDerivedClass), "get_value", new object[] { }, "get_value()")]
        [TestCase(typeof(AlreadyDefinedSnakeCaseMemberTestDerivedClass), "A", new object[] { }, "A()")]
        [TestCase(typeof(AlreadyDefinedSnakeCaseMemberTestDerivedClass), "a", new object[] { }, "a()")]
        [TestCase(typeof(AlreadyDefinedSnakeCaseMemberTestDerivedClass), "GetValue2", new object[] { 2 }, "override GetValue2(int x)")]
        [TestCase(typeof(AlreadyDefinedSnakeCaseMemberTestDerivedClass), "GetValue3", new object[] { 2 }, "new GetValue3(int x)")]
        // original beats fake
        [TestCase(typeof(AlreadyDefinedSnakeCaseMemberTestDerivedClass), "get_value_2", new object[] { 2 }, "get_value_2(int x)")]
        [TestCase(typeof(AlreadyDefinedSnakeCaseMemberTestDerivedClass), "get_value_3", new object[] { 2 }, "get_value_3(int x)")]

        [TestCase(typeof(AlreadyDefinedSnakeCaseMemberTestDerivedClass), "a", new object[] { "AlreadyDefinedSnakeCaseMemberTestBaseClass" }, "a(AlreadyDefinedSnakeCaseMemberTestBaseClass)")]
        // A(PyObject) is real
        [TestCase(typeof(AlreadyDefinedSnakeCaseMemberTestDerivedClass), "A", new object[] { "AlreadyDefinedSnakeCaseMemberTestBaseClass" }, "A(PyObject)")]
        [TestCase(typeof(AlreadyDefinedSnakeCaseMemberTestDerivedClass), "a", new object[] { "Type" }, "A(PyObject)")]
        [TestCase(typeof(AlreadyDefinedSnakeCaseMemberTestDerivedClass), "A", new object[] { "Type" }, "A(PyObject)")]
        [TestCase(typeof(AlreadyDefinedSnakeCaseMemberTestDerivedClass), "A", new object[] { "Type" }, "A(PyObject)")]
        public void BindsSnakeCasedMethodAsOverload(Type type, string methodName, object[] args, string expectedMessage)
        {
            if (args.Length == 1)
            {
                if (args[0] is "AlreadyDefinedSnakeCaseMemberTestBaseClass")
                {
                    args = new object[] { new AlreadyDefinedSnakeCaseMemberTestBaseClass() };
                }
                else if (args[0] is "Type")
                {
                    args = new object[] { typeof(string) };
                }
            }

            var obj = Activator.CreateInstance(type);
            using var pyObj = obj.ToPython();

            using var method = pyObj.GetAttr(methodName);
            var pyArgs = args.Select(x => x.ToPython()).ToArray();

            var exception = Assert.Throws<Exception>(() => method.Invoke(pyArgs));
            Assert.AreEqual(expectedMessage, exception.Message);

            foreach (var x in pyArgs)
            {
                x.Dispose();
            }
        }

        [Test]
        public void DoesntBindSnakeCasedMemberIfAlreadyOriginallyDefinedAsProperty()
        {
            var obj = new AlreadyDefinedSnakeCaseMemberTestBaseClass();
            using var pyObj = obj.ToPython();

            Assert.AreEqual(123, pyObj.GetAttr("SomeIntProperty").As<int>());
            Assert.AreEqual(321, pyObj.GetAttr("some_int_property").As<int>());
        }

        [Test]
        public void DoesntBindSnakeCasedMemberIfAlreadyOriginallyDefinedAsMethod()
        {
            var obj = new AlreadyDefinedSnakeCaseMemberTestBaseClass();
            using var pyObj = obj.ToPython();

            Assert.AreEqual(456, pyObj.GetAttr("AnotherIntProperty").As<int>());

            using var method = pyObj.GetAttr("another_int_property");
            Assert.IsTrue(method.IsCallable());
            Assert.AreEqual(654, method.Invoke().As<int>());
        }

        [Test]
        public void DoesntBindSnakeCasedMemberIfAlreadyOriginallyDefinedAsPropertyInBaseClass()
        {
            var obj = new AlreadyDefinedSnakeCaseMemberTestDerivedClass();
            using var pyObj = obj.ToPython();

            Assert.AreEqual(111, pyObj.GetAttr("SomeIntProperty").As<int>());
            Assert.AreEqual(321, pyObj.GetAttr("some_int_property").As<int>());
        }

        [Test]
        public void DoesntBindSnakeCasedMemberIfAlreadyOriginallyDefinedAsMethodInBaseClass()
        {
            var obj = new AlreadyDefinedSnakeCaseMemberTestDerivedClass();
            using var pyObj = obj.ToPython();

            Assert.AreEqual(222, pyObj.GetAttr("AnotherIntProperty").As<int>());

            using var method = pyObj.GetAttr("another_int_property");
            Assert.IsTrue(method.IsCallable());
            Assert.AreEqual(654, method.Invoke().As<int>());
        }

        [Test]
        public void BindsMemberWithSnakeCasedNameMatchingExistingPrivateMember()
        {
            using var obj = new AlreadyDefinedSnakeCaseMemberTestBaseClass().ToPython();

            Assert.AreEqual(333, obj.GetAttr("private_field").As<int>());
        }

        private abstract class AlreadyDefinedSnakeCaseMemberTestBaseAbstractClass
        {
            public abstract int AbstractProperty { get; }

            public virtual int SomeIntProperty { get; set; } = 123;

            public int some_int_property { get; set; } = 321;

            public virtual int AnotherIntProperty { get; set; } = 456;

            public int another_int_property()
            {
                return 654;
            }
        }

        private class AlreadyDefinedSnakeCaseMemberTestDerivedFromAbstractClass : AlreadyDefinedSnakeCaseMemberTestBaseAbstractClass
        {
            public override int AbstractProperty => 0;

            public int SomeIntProperty { get; set; } = 333;

            public int AnotherIntProperty { get; set; } = 444;
        }

        [Test]
        public void DoesntBindSnakeCasedMemberIfAlreadyOriginallyDefinedAsPropertyInBaseAbstractClass()
        {
            var obj = new AlreadyDefinedSnakeCaseMemberTestDerivedFromAbstractClass();
            using var pyObj = obj.ToPython();

            Assert.AreEqual(333, pyObj.GetAttr("SomeIntProperty").As<int>());
            Assert.AreEqual(321, pyObj.GetAttr("some_int_property").As<int>());
        }

        [Test]
        public void DoesntBindSnakeCasedMemberIfAlreadyOriginallyDefinedAsMethodInBaseAbstractClass()
        {
            var obj = new AlreadyDefinedSnakeCaseMemberTestDerivedFromAbstractClass();
            using var pyObj = obj.ToPython();

            Assert.AreEqual(444, pyObj.GetAttr("AnotherIntProperty").As<int>());

            using var method = pyObj.GetAttr("another_int_property");
            Assert.IsTrue(method.IsCallable());
            Assert.AreEqual(654, method.Invoke().As<int>());
        }

        public class Class1
        {
        }

        private class TestClass1
        {
            public dynamic get(Class1 s)
            {
                return "dynamic get(Class1 s)";
            }
        }

        private class TestClass2 : TestClass1
        {
            public PyObject Get(PyObject o)
            {
                return "PyObject Get(PyObject o)".ToPython();
            }

            public dynamic Get(Type t)
            {
                return "dynamic Get(Type t)";
            }
        }

        [Test]
        public void BindsCorrectOverloadForClassName()
        {
            using var obj = new TestClass2().ToPython();

            var result = obj.GetAttr("get").Invoke(new Class1().ToPython()).As<string>();
            Assert.AreEqual("dynamic get(Class1 s)", result);

            result = obj.GetAttr("get").Invoke(new TestClass1().ToPython()).As<string>();
            Assert.AreEqual("PyObject Get(PyObject o)", result);

            using (Py.GIL())
            {
                // Passing type name directly instead of typeof(Class1) from C#
                var module = PyModule.FromString("module", $@"
from clr import AddReference
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

def call(instance):
    return instance.get(ClassManagerTests.Class1)
                    ");

                result = module.GetAttr("call").Invoke(obj).As<string>();
                Assert.AreEqual("PyObject Get(PyObject o)", result);
            }
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
