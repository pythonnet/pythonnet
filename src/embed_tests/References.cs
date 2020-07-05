namespace Python.EmbeddingTest
{
    using System;
    using NUnit.Framework;
    using Python.Runtime;

    public class References
    {
        private Py.GILState _gs;

        [SetUp]
        public void SetUp()
        {
            string path = @"C:\Users\Sofiane\AppData\Local\Programs\Python\Python38;";
            Environment.SetEnvironmentVariable("PATH", path, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("PYTHONHOME", @"C:\Users\Sofiane\AppData\Local\Programs\Python\Python38", EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("PYTHONPATH ", @"C:\Users\Sofiane\AppData\Local\Programs\Python\Python38\DLLs", EnvironmentVariableTarget.Process);
            _gs = Py.GIL();
        }

        [TearDown]
        public void Dispose()
        {
            _gs.Dispose();
        }

        [Test]
        public void MoveToPyObject_SetsNull()
        {
            var dict = new PyDict();
            NewReference reference = Runtime.PyDict_Items(dict.Handle);
            try
            {
                Assert.IsFalse(reference.IsNull());

                using (reference.MoveToPyObject())
                    Assert.IsTrue(reference.IsNull());
            }
            finally
            {
                reference.Dispose();
            }
        }

        [Test]
        public void CanBorrowFromNewReference()
        {
            var dict = new PyDict();
            NewReference reference = Runtime.PyDict_Items(dict.Handle);
            try
            {
                PythonException.ThrowIfIsNotZero(Runtime.PyList_Reverse(reference));
            }
            finally
            {
                reference.Dispose();
            }
        }
    }
}
