using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    class QCTests
    {
        private static dynamic module;
        private static string testModule = @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")
from Python.EmbeddingTest import Algo, Insight
class PythonModule(Algo):
    def TestA(self):
        try:
            self.EmitInsights(Insight.Group(Insight()))
            return True
        except:
            return False
";

        [OneTimeSetUp]
        public void Setup()
        {
            PythonEngine.Initialize();
            module = PythonEngine.ModuleFromString("module", testModule).GetAttr("PythonModule").Invoke();
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
