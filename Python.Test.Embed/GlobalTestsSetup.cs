using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{

    // As the SetUpFixture, the OneTimeTearDown of this class is executed after
    // all tests have run.
    [SetUpFixture]
    public class GlobalTestsSetup
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
