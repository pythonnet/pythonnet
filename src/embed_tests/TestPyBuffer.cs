using System;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using Python.Runtime;
using Python.Runtime.Codecs;

namespace Python.EmbeddingTest {
    class TestPyBuffer
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
        public void TestBufferWrite()
        {
            string bufferTestString = "hello world! !$%&/()=?";
            string bufferTestString2 = "h llo world! !$%&/()=?";

            using var _ = Py.GIL();

            using var pythonArray = ByteArrayFromAsciiString(bufferTestString);

            using (PyBuffer buf = pythonArray.GetBuffer(PyBUF.WRITABLE))
            {
                byte[] managedArray = { (byte)' ' };
                buf.Write(managedArray, 0, managedArray.Length, 1);
            }

            string result = pythonArray.InvokeMethod("decode", "utf-8".ToPython()).As<string>();
            Assert.IsTrue(result == bufferTestString2);
        }

        [Test]
        public void TestBufferRead()
        {
            string bufferTestString = "hello world! !$%&/()=?";

            using var _ = Py.GIL();

            using var pythonArray = ByteArrayFromAsciiString(bufferTestString);
            byte[] managedArray = new byte[bufferTestString.Length];

            using (PyBuffer buf = pythonArray.GetBuffer())
            {
                managedArray[0] = (byte)' ';
                buf.Read(managedArray, 1, managedArray.Length - 1, 1);
            }

            string result = new UTF8Encoding().GetString(managedArray);
            Assert.IsTrue(result == " " + bufferTestString.Substring(1));
        }

        [Test]
        public void ArrayHasBuffer()
        {
            var array = new[,] {{1, 2}, {3,4}};
            var memoryView = PythonEngine.Eval("memoryview");
            var mem = memoryView.Invoke(array.ToPython());
            Assert.AreEqual(1, mem[(0, 0).ToPython()].As<int>());
            Assert.AreEqual(array[1,0], mem[(1, 0).ToPython()].As<int>());
        }

        [Test]
        public void RefCount()
        {
            using var _ = Py.GIL();
            using var arr = ByteArrayFromAsciiString("hello world! !$%&/()=?");

            Assert.AreEqual(1, arr.Refcount);

            using (PyBuffer buf = arr.GetBuffer())
            {
                Assert.AreEqual(2, arr.Refcount);
            }

            Assert.AreEqual(1, arr.Refcount);
        }

        [Test]
        public void Finalization()
        {
            if (Type.GetType("Mono.Runtime") is not null)
            {
                Assert.Inconclusive("test unreliable in Mono");
                return;
            }

            using var _ = Py.GIL();
            using var arr = ByteArrayFromAsciiString("hello world! !$%&/()=?");

            Assert.AreEqual(1, arr.Refcount);

            MakeBufAndLeak(arr);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            Finalizer.Instance.Collect();

            Assert.AreEqual(1, arr.Refcount);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void MakeBufAndLeak(PyObject bufProvider)
        {
            PyBuffer buf = bufProvider.GetBuffer();
        }

        static PyObject ByteArrayFromAsciiString(string str)
        {
            using var scope = Py.CreateScope();
            return Runtime.Runtime.PyByteArray_FromStringAndSize(str).MoveToPyObject();
        }
    }
}
