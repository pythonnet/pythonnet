namespace Python.EmbeddingTest
{
    using System;
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
            string path = @"C:\Users\Sofiane\AppData\Local\Programs\Python\Python38;";
            Environment.SetEnvironmentVariable("PATH", path, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("PYTHONHOME", @"C:\Users\Sofiane\AppData\Local\Programs\Python\Python38", EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("PYTHONPATH ", @"C:\Users\Sofiane\AppData\Local\Programs\Python\Python38\DLLs", EnvironmentVariableTarget.Process);
            PythonEngine.Initialize();
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            PythonEngine.Shutdown();
        }
    }
}
