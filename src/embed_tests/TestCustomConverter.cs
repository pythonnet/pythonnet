using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestCustomConverter
    {

        private PyScope ps;
        private PyObject numpy;
        [OneTimeSetUp]
        public void SetUp()
        {
            SetupConverter();
            PythonEngine.Initialize();
            using (Py.GIL())
            {
                ps = Py.CreateScope("test");
                numpy = Py.Import("numpy");
            }
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            PythonEngine.Shutdown();
        }


        #region Setup Converter
        public void SetupConverter()
        {
            CustomConverter.RegisterConverterToPython<int[]>(ConvertIntArrayToNumpyArray);
        }

        public PyObject ConvertIntArrayToNumpyArray(object obj)
        {
            var list = new PyList();
            foreach(var data in (Array)obj)
            {
                list.Append(new PyInt((int)data));
            }

            using (Py.GIL())
            {
                return numpy.InvokeMethod("array", list);
            }
        }

        #endregion

        #region ArrayMethod
        public Array IntegerArray()
        {
            return new int[] { 1, 2, 3, 4 };
        }
        #endregion

        [Test]
        public void ShouldConvertIntArrayToNumpyArray()
        {

            var list = IntegerArray();
            PyObject numpyArray = list.ToPython();

            Assert.AreEqual("<class 'numpy.ndarray'>", numpyArray.GetAttr("__class__").ToString());
            Assert.AreEqual(list.Length, numpyArray.InvokeMethod("__len__").As<int>());
            for(int index=0; index< numpyArray.InvokeMethod("__len__").As<int>(); index++)
            {
                Assert.AreEqual(list.GetValue(index), numpyArray.InvokeMethod("__getitem__", new PyInt(index)).As<int>());
            }

            var a = 8;
         
        }
    }
}
