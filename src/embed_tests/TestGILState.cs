namespace Python.EmbeddingTest
{
    using NUnit.Framework;
    using Python.Runtime;

    public class TestGILState
    {
        /// <summary>
        /// Ensure, that calling <see cref="Py.GILState.Dispose"/> multiple times is safe
        /// </summary>
        [Test]
        public void CanDisposeMultipleTimes()
        {
            using (var gilState = Py.GIL())
            {
                for(int i = 0; i < 50; i++)
                    gilState.Dispose();
            }
        }

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
    }
}
