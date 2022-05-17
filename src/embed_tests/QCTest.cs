using System;
using System.Collections.Generic;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    class QCTests
    {
        private static dynamic pythonSuperInitInt;
        private static dynamic pythonSuperInitDefault;
        private static dynamic pythonSuperInitNone;
        private static dynamic pythonSuperInitNotCallingBase;

        private static dynamic withArgs_PythonSuperInitNotCallingBase;
        private static dynamic withArgs_PythonSuperInitDefault;
        private static dynamic withArgs_PythonSuperInitInt;

        private static dynamic containsTest;
        private static dynamic module;
        private static string testModule = @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")
from Python.EmbeddingTest import *
class PythonModule(Algo):
    def TestA(self):
        try:
            self.EmitInsights(Insight.Group(Insight()))
            return True
        except:
            return False

def ContainsTest(key, collection):
    if key in collection.Keys:
        return True
    return False

class WithArgs_PythonSuperInitNotCallingBase(SuperInit):
    def __init__(self, jose):
        return

class WithArgs_PythonSuperInitDefault(SuperInit):
    def __init__(self, jose):
        super().__init__()

class WithArgs_PythonSuperInitInt(SuperInit):
    def __init__(self, jose):
        super().__init__(jose)

class PythonSuperInitNotCallingBase(SuperInit):
    def __init__(self):
        return

class PythonSuperInitDefault(SuperInit):
    def __init__(self):
        super().__init__()

class PythonSuperInitInt(SuperInit):
    def __init__(self):
        super().__init__(1)

class PythonSuperInitNone(SuperInit):
    def jose(self):
        return 1
";

        [OneTimeSetUp]
        public void Setup()
        {
            PythonEngine.Initialize();
            var pyModule = PyModule.FromString("module", testModule);
            containsTest = pyModule.GetAttr("ContainsTest");
            module = pyModule.GetAttr("PythonModule").Invoke();

            pythonSuperInitInt = pyModule.GetAttr("PythonSuperInitInt");
            pythonSuperInitDefault = pyModule.GetAttr("PythonSuperInitDefault");
            pythonSuperInitNone = pyModule.GetAttr("PythonSuperInitNone");
            pythonSuperInitNotCallingBase = pyModule.GetAttr("PythonSuperInitNotCallingBase");

            withArgs_PythonSuperInitNotCallingBase = pyModule.GetAttr("WithArgs_PythonSuperInitNotCallingBase");
            withArgs_PythonSuperInitDefault = pyModule.GetAttr("WithArgs_PythonSuperInitDefault");
            withArgs_PythonSuperInitInt = pyModule.GetAttr("WithArgs_PythonSuperInitInt");
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            PythonEngine.Shutdown();
        }

        [Test]
        /// Test case for issue with params
        /// Highlights case where params argument is a CLR object wrapped in Python
        /// https://quantconnect.slack.com/archives/G51920EN4/p1615418516028900
        public void ParamTest()
        {
            var output = (bool)module.TestA();
            Assert.IsTrue(output);
        }

        [TestCase("AAPL", false)]
        [TestCase("SPY", true)]
        public void ContainsTest(string key, bool expected)
        {
            var dic = new Dictionary<string, object> { { "SPY", new object() } };
            Assert.AreEqual(expected, (bool)containsTest(key, dic));
        }

        [Test]
        public void WithArgs_NoBaseConstructorCall()
        {
            using (Py.GIL())
            {
                var instance = withArgs_PythonSuperInitNotCallingBase(1);
                // this is true because we call the constructor always
                Assert.IsTrue((bool)instance.CalledInt);
                Assert.IsFalse((bool)instance.CalledDefault);
            }
        }

        [Test]
        public void WithArgs_IntConstructor()
        {
            using (Py.GIL())
            {
                var instance = withArgs_PythonSuperInitInt(1);
                Assert.IsTrue((bool)instance.CalledInt);
                Assert.IsFalse((bool)instance.CalledDefault);
            }
        }

        [Test]
        public void WithArgs_DefaultConstructor()
        {
            using (Py.GIL())
            {
                var instance = withArgs_PythonSuperInitDefault(1);
                Assert.IsTrue((bool)instance.CalledInt);
                Assert.IsTrue((bool)instance.CalledDefault);
            }
        }

        [Test]
        public void NoArgs_NoBaseConstructorCall()
        {
            using (Py.GIL())
            {
                var instance = pythonSuperInitNotCallingBase();
                Assert.IsFalse((bool)instance.CalledInt);
                // this is true because we call the default constructor always
                Assert.IsTrue((bool)instance.CalledDefault);
            }
        }

        [Test]
        public void NoArgs_IntConstructor()
        {
            using (Py.GIL())
            {
                var instance = pythonSuperInitInt();
                Assert.IsTrue((bool)instance.CalledInt);
                // this is true because we call the default constructor always
                Assert.IsTrue((bool)instance.CalledDefault);
            }
        }

        [Test]
        public void NoArgs_DefaultConstructor()
        {
            using (Py.GIL())
            {
                var instance = pythonSuperInitNone();
                Assert.IsFalse((bool)instance.CalledInt);
                Assert.IsTrue((bool)instance.CalledDefault);
            }
        }

        [Test]
        public void NoArgs_NoConstructor()
        {
            using (Py.GIL())
            {
                var instance = pythonSuperInitDefault.Invoke();

                Assert.IsFalse((bool)instance.CalledInt);
                Assert.IsTrue((bool)instance.CalledDefault);
            }
        }
    }

    public class Algo
    {
        /// <param name="insight">The insight to be emitted</s>
        public void EmitInsights(Insight insight)
        {
            EmitInsights(new[] { insight });
        }

        /// <param name="insights">The array of insights to be emitted</param>
        public void EmitInsights(params Insight[] insights)
        {
            foreach (var insight in insights)
            {
                Console.WriteLine(insight.info);
            }
        }

    }

    public class SuperInit
    {
        public bool CalledInt { get; private set; }
        public bool CalledDefault { get; private set; }
        public SuperInit(int a)
        {
            CalledInt = true;
        }
        public SuperInit()
        {
            CalledDefault = true;
        }
    }

    public class Insight
    {
        public string info;
        public Insight()
        {
            info = "pepe";
        }

        /// <param name="insight">The insight to be grouped</param>
        public static IEnumerable<Insight> Group(Insight insight) => Group(new[] { insight });

        /// <param name="insights">The insights to be grouped</param>
        public static IEnumerable<Insight> Group(params Insight[] insights)
        {
            if (insights == null)
            {
                throw new ArgumentNullException(nameof(insights));
            }

            return insights;
        }
    }
}
