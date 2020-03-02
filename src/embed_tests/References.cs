namespace Python.EmbeddingTest
{
    using NUnit.Framework;
    using Python.Runtime;

    public class References
    {
        private Py.GILState _gs;

        [SetUp]
        public void SetUp()
        {
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
    }
}
