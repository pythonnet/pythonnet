using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    [SetUpFixture]
    public class TestsSuite
    {
        [OneTimeTearDown]
        public void FinalCleanup()
        {
            if (PythonEngine.IsInitialized)
            {
                PythonEngine.Shutdown();
            }
        }
    }
}
