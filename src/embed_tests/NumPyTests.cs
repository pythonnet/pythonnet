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
            TupleCodec<ValueTuple>.Register();
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            PyObjectConversions.Reset();
        }

        [Test]
        public void TestReadme()
        {
            Assert.That(np.cos(np.pi * 2).ToString(), Is.EqualTo("1.0"));

            dynamic sin = np.sin;
            StringAssert.StartsWith("-0.95892", sin(5).ToString());

            double c = (double)(np.cos(5) + sin(5));
            Assert.That(c, Is.EqualTo(-0.675262).Within(0.01));

            dynamic a = np.array(new List<float> { 1, 2, 3 });
            Assert.That(a.dtype.ToString(), Is.EqualTo("float64"));

            dynamic b = np.array(new List<float> { 6, 5, 4 }, Py.kw("dtype", np.int32));
            Assert.That(b.dtype.ToString(), Is.EqualTo("int32"));

            Assert.That((a * b).ToString().Replace("  ", " "), Is.EqualTo("[ 6. 10. 12.]"));
        }

        [Test]
        public void MultidimensionalNumPyArray()
        {
            var array = new[,] { { 1, 2 }, { 3, 4 } };
            var ndarray = np.InvokeMethod("asarray", array.ToPython());
            Assert.That(ndarray.GetAttr("shape").As<(int, int)>(), Is.EqualTo((2, 2)));
            Assert.That(ndarray[(0, 0).ToPython()].InvokeMethod("__int__").As<int>(), Is.EqualTo(1));
            Assert.That(ndarray[(1, 0).ToPython()].InvokeMethod("__int__").As<int>(), Is.EqualTo(array[1, 0]));
        }

        [Test]
        public void Int64Array()
        {
            var array = new long[,] { { 1, 2 }, { 3, 4 } };
            var ndarray = np.InvokeMethod("asarray", array.ToPython());
            Assert.That(ndarray.GetAttr("shape").As<(int, int)>(), Is.EqualTo((2, 2)));
            Assert.That(ndarray[(0, 0).ToPython()].InvokeMethod("__int__").As<long>(), Is.EqualTo(1));
            Assert.That(ndarray[(1, 0).ToPython()].InvokeMethod("__int__").As<long>(), Is.EqualTo(array[1, 0]));
        }

        [Test]
        public void VarArg()
        {
            dynamic zX = np.array(new[,] { { 1, 2, 3 }, { 4, 5, 6 }, { 8, 9, 0 } });
            dynamic grad = np.gradient(zX, 4.0, 5.0);
            dynamic grad2 = np.InvokeMethod("gradient", new PyObject[] {zX, new PyFloat(4.0), new PyFloat(5.0)});

            Assert.AreEqual(4.125, grad[0].sum().__float__().As<double>(), 0.001);
            Assert.AreEqual(-1.2, grad[1].sum().__float__().As<double>(), 0.001);
            Assert.AreEqual(4.125, grad2[0].sum().__float__().As<double>(), 0.001);
            Assert.AreEqual(-1.2, grad2[1].sum().__float__().As<double>(), 0.001);
        }

#pragma warning disable IDE1006
        dynamic np
        {
            get
            {
                try
                {
                    return Py.Import("numpy");
                }
                catch (PythonException)
                {
                    Assert.Inconclusive("Numpy or dependency not installed");
                    return null;
                }
            }
        }

    }
}
