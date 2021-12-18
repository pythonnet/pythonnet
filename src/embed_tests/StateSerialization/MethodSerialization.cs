using System.IO;
using System.Reflection;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest.StateSerialization;

public class MethodSerialization
{
    [Test]
    public void GenericRoundtrip()
    {
        var method = typeof(MethodTestHost).GetMethod(nameof(MethodTestHost.Generic));
        var maybeMethod = new MaybeMethodBase<MethodBase>(method);
        var restored = SerializationRoundtrip(maybeMethod);
        Assert.IsTrue(restored.Valid);
        Assert.AreEqual(method, restored.Value);
    }

    static T SerializationRoundtrip<T>(T item)
    {
        using var buf = new MemoryStream();
        var formatter = RuntimeData.CreateFormatter();
        formatter.Serialize(buf, item);
        buf.Position = 0;
        return (T)formatter.Deserialize(buf);
    }
}

public class MethodTestHost
{
    public void Generic<T>(T item, T[] array, ref T @ref) { }
}
