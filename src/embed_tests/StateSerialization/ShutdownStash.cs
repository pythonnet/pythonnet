using System;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest.StateSerialization;

/// <summary>
/// Regression for https://github.com/pythonnet/pythonnet/issues/2282.
/// When BinaryFormatter cannot run, Shutdown must not throw and must not
/// leave a sys.clr_data capsule behind.
/// </summary>
public class ShutdownStash
{
    [Test]
    public void ShutdownLeavesNoClrData()
    {
        if (!PythonEngine.IsInitialized)
        {
            PythonEngine.Initialize();
        }

        Assert.That(RuntimeData.CreateFormatter(), Is.InstanceOf<NoopFormatter>());

        using (Py.GIL())
        {
            using var wrapped = new UriBuilder().ToPython();
            Assert.That(RuntimeData.HasStashData(), Is.False);
        }

        try
        {
            PythonEngine.Shutdown();

            using (Py.GIL())
            {
                Assert.That(RuntimeData.HasStashData(), Is.False);
            }
        }
        finally
        {
            // Shutdown leaves the interpreter alive. A second Shutdown in this
            // process then deallocates types from the previous runtime and
            // trips MetaType.tp_dealloc. Finish the interpreter first so the
            // suite, and GlobalTestsSetup, start from a new one.
            if (Runtime.Runtime.Py_IsInitialized() != 0)
            {
                Runtime.Runtime.Py_Finalize();
            }
            if (!PythonEngine.IsInitialized)
            {
                PythonEngine.Initialize();
            }
        }
    }
}
