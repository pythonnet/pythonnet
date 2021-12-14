namespace Python.EmbeddingTest
{
    using NUnit.Framework;
    using Python.Runtime;

    public class References
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
        public void MoveToPyObject_SetsNull()
        {
            var dict = new PyDict();
            NewReference reference = Runtime.PyDict_Items(dict.Reference);
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
            using NewReference reference = Runtime.PyDict_Items(dict.Reference);
            BorrowedReference borrowed = reference.BorrowOrThrow();
            PythonException.ThrowIfIsNotZero(Runtime.PyList_Reverse(borrowed));
        }
    }
}
