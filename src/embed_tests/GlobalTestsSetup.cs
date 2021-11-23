using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{

    // As the SetUpFixture, the OneTimeTearDown of this class is executed after
    // all tests have run.
    [SetUpFixture]
    public partial class GlobalTestsSetup
    {
        [OneTimeSetUp]
        public void GlobalSetup()
        {
            Finalizer.Instance.ErrorHandler += FinalizerErrorHandler;
        }

        private void FinalizerErrorHandler(object sender, Finalizer.ErrorArgs e)
        {
            if (e.Error is RuntimeShutdownException)
            {
                // allow objects to leak after the python runtime run
                // they were created in is gone
                e.Handled = true;
            }
        }

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
