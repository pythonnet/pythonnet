using Python.Runtime;
using NUnit.Framework;

namespace Python.EmbeddingTest.NeedsReinit;

public class StopAndRestartEngine
{
    bool WasInitialized = false;

    [OneTimeSetUp]
    public void Setup()
    {
        WasInitialized = PythonEngine.IsInitialized;
        if (WasInitialized)
        {
            PythonEngine.Shutdown();
        }
    }

    [OneTimeTearDown]
    public void Teardown()
    {
        if (WasInitialized && !PythonEngine.IsInitialized)
        {
            PythonEngine.Initialize();
        }
    }
}
