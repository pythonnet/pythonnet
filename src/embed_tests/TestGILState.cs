using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest;

public class TestGILState : BaseFixture
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
}
