using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest.StateSerialization;

[NonParallelizable]
public class ModuleSerialization
{
    [TestCase(false)]
    [TestCase(true)]
    public void NamespaceModuleRoundtrip(bool loadNames)
    {
        var formatter = RuntimeData.CreateFormatter();
        if (formatter is NoopFormatter)
        {
            Assert.Inconclusive("NoopFormatter in use, cannot perform serialization test.");
        }

        using var gil = Py.GIL();
        using var pyModule = ModuleObject.Create("System").MoveToPyObject();
        var module = (ModuleObject)ManagedType.GetManagedObject(pyModule)!;
        using (var int32 = module.GetAttribute("Int32", true))
        {
            Assert.That(int32.IsNull(), Is.False);
        }

        string[] names = null;
        if (loadNames)
        {
            using var all = pyModule.GetAttr("__all__");
            names = all.As<string[]>();
            Assert.That(names, Does.Contain("Int32"));
        }

        for (int i = 0; i < 2; i++)
        {
            var context = module.Save(pyModule);
            using var buffer = new MemoryStream();
            formatter.Serialize(buffer, new object[] { module, context });
            buffer.Position = 0;
            var restored = (object[])formatter.Deserialize(buffer);
            ExtensionType.tp_clear(pyModule);
            module = (ModuleObject)restored[0];
            module.Load(pyModule, (Dictionary<string, object>)restored[1]);

            using (var int32 = module.GetAttribute("Int32", true))
            {
                Assert.That(int32.IsNull(), Is.False);
            }
            using (var collections = module.GetAttribute("Collections", true))
            {
                Assert.That(collections.IsNull(), Is.False);
            }

            using var all = pyModule.GetAttr("__all__");
            var restoredNames = all.As<string[]>();
            Assert.That(restoredNames, Does.Contain("Int32"));
            Assert.That(restoredNames, Is.Unique);
            if (names is not null)
            {
                Assert.That(restoredNames, Is.EquivalentTo(names));
            }
            names = restoredNames;
        }
    }
}
