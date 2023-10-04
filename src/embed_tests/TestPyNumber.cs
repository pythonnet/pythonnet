using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest;

public class TestPyNumber : BaseFixture
{
    [Test]
    public void IsNumberTypeTrue()
    {
        var i = new PyInt(1);
        Assert.True(PyNumber.IsNumberType(i));
    }

    [Test]
    public void IsNumberTypeFalse()
    {
        var s = new PyString("Foo");
        Assert.False(PyNumber.IsNumberType(s));
    }
}
