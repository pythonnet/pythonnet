using System;
using System.Collections.Generic;
using NUnit.Framework;
using Python.Runtime;
using Python.Runtime.Codecs;

namespace Python.EmbeddingTest
{
    public class NumPyTests
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            PythonEngine.Initialize();
            TupleCodec<ValueTuple>.Register();
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            PythonEngine.Shutdown();
        }

        [Test]
        public void TestReadme()
        {
            dynamic np;
            try
            {
                np = Py.Import("numpy");
            }
            catch (PythonException)
            {
                Assert.Inconclusive("Numpy or dependency not installed");
                return;
            }
            
            Assert.AreEqual("1.0", np.cos(np.pi * 2).ToString());

            dynamic sin = np.sin;
            StringAssert.StartsWith("-0.95892", sin(5).ToString());

            double c = (double)(np.cos(5) + sin(5));
            Assert.AreEqual(-0.675262, c, 0.01);

            dynamic a = np.array(new List<float> { 1, 2, 3 });
            Assert.AreEqual("float64", a.dtype.ToString());

            dynamic b = np.array(new List<float> { 6, 5, 4 }, Py.kw("dtype", np.int32));
            Assert.AreEqual("int32", b.dtype.ToString());

            Assert.AreEqual("[ 6. 10. 12.]", (a * b).ToString().Replace("  ", " "));
        }

        [Test]
        public void MultidimensionalNumPyArray()
        {
            PyObject np;
            try {
                np = Py.Import("numpy");
            } catch (PythonException) {
                Assert.Inconclusive("Numpy or dependency not installed");
                return;
            }

            var array = new[,] { { 1, 2 }, { 3, 4 } };
            var ndarray = np.InvokeMethod("asarray", array.ToPython());
            Assert.AreEqual((2,2), ndarray.GetAttr("shape").As<(int,int)>());
            Assert.AreEqual(1, ndarray[(0, 0).ToPython()].InvokeMethod("__int__").As<int>());
            Assert.AreEqual(array[1, 0], ndarray[(1, 0).ToPython()].InvokeMethod("__int__").As<int>());
        }

        [Test]
        public void Int64Array()
        {
            PyObject np;
            try
            {
                np = Py.Import("numpy");
            }
            catch (PythonException)
            {
                Assert.Inconclusive("Numpy or dependency not installed");
                return;
            }

            var array = new long[,] { { 1, 2 }, { 3, 4 } };
            var ndarray = np.InvokeMethod("asarray", array.ToPython());
            Assert.AreEqual((2, 2), ndarray.GetAttr("shape").As<(int, int)>());
            Assert.AreEqual(1, ndarray[(0, 0).ToPython()].InvokeMethod("__int__").As<long>());
            Assert.AreEqual(array[1, 0], ndarray[(1, 0).ToPython()].InvokeMethod("__int__").As<long>());
        }
    }
}
