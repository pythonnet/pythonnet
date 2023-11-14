using System;
using System.Diagnostics;
using System.Threading;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest;

public class Events
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
    public void UsingDoesNotLeak()
    {
        using var scope = Py.CreateScope();
        scope.Exec(@"
import gc

from Python.EmbeddingTest import ClassWithEventHandler

def event_handler():
    pass

for _ in range(2000):
    example = ClassWithEventHandler()
    example.LeakEvent += event_handler
    example.LeakEvent -= event_handler
    del example

gc.collect()
");
        Runtime.Runtime.TryCollectingGarbage(10);
        Assert.AreEqual(0, ClassWithEventHandler.alive);
    }
}

public class ClassWithEventHandler
{
    internal static int alive;

    public event EventHandler LeakEvent;
    private Array arr;  // dummy array to exacerbate memory leak

    public ClassWithEventHandler()
    {
        Interlocked.Increment(ref alive);
        this.arr = new int[800];
    }

    ~ClassWithEventHandler()
    {
        Interlocked.Decrement(ref alive);
    }
}
