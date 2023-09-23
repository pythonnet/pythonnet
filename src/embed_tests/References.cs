using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest;

public class References : BaseFixture
{
    [Test]
    public void MoveToPyObject_SetsNull()
    {
        using var dict = new PyDict();
        NewReference reference = Runtime.Runtime.PyDict_Items(dict.Reference);
        try
        {
            Assert.IsFalse(reference.IsNull());

            using (reference.MoveToPyObject())
                Assert.IsTrue(reference.IsNull());
        }
        finally
        {
            reference.Dispose();
        }
    }

    [Test]
    public void CanBorrowFromNewReference()
    {
        using var dict = new PyDict();
        using NewReference reference = Runtime.Runtime.PyDict_Items(dict.Reference);
        BorrowedReference borrowed = reference.BorrowOrThrow();
        PythonException.ThrowIfIsNotZero(Runtime.Runtime.PyList_Reverse(borrowed));
    }
}
