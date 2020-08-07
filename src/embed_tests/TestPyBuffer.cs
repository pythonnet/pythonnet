using System.Text;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest {
    class TestPyBuffer
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
        public void TestBufferWrite()
        {
            string bufferTestString = "hello world! !$%&/()=?";

            using (Py.GIL())
            {
                using (var scope = Py.CreateScope())
                {
                    scope.Exec($"arr = bytearray({bufferTestString.Length})");
                    PyObject pythonArray = scope.Get("arr");
                    byte[] managedArray = new UTF8Encoding().GetBytes(bufferTestString);

                    using (PyBuffer buf = pythonArray.GetBuffer())
                    {
                        buf.Write(managedArray, 0, managedArray.Length);
                    }

                    string result = scope.Eval("arr.decode('utf-8')").ToString();
                    Assert.IsTrue(result == bufferTestString);
                }
            }
        }

        [Test]
        public void TestBufferRead()
        {
            string bufferTestString = "hello world! !$%&/()=?";

            using (Py.GIL())
            {
                using (var scope = Py.CreateScope())
                {
                    scope.Exec($"arr = b'{bufferTestString}'");
                    PyObject pythonArray = scope.Get("arr");
                    byte[] managedArray = new byte[bufferTestString.Length];

                    using (PyBuffer buf = pythonArray.GetBuffer())
                    {
                        buf.Read(managedArray, 0, managedArray.Length);
                    }

                    string result = new UTF8Encoding().GetString(managedArray);
                    Assert.IsTrue(result == bufferTestString);
                }
            }
        }
    }
}
