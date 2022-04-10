using System;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest;

public class ExtensionTypes
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
    public void WeakrefIsNone_AfterBoundMethodIsGone()
    {
        using var makeref = Py.Import("weakref").GetAttr("ref");
        var boundMethod = new UriBuilder().ToPython().GetAttr(nameof(UriBuilder.GetHashCode));
        var weakref = makeref.Invoke(boundMethod);
        boundMethod.Dispose();
        Assert.IsTrue(weakref.Invoke().IsNone());
    }
}
