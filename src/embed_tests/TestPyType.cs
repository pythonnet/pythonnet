using System.Runtime.InteropServices;
using System.Text;

using NUnit.Framework;

using Python.Runtime;
using Python.Runtime.Native;

namespace Python.EmbeddingTest
{
    public class TestPyType
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
        public void CanCreateHeapType()
        {
            const string name = "nÁmæ";
            const string docStr = "dÁcæ";

            using var doc = new StrPtr(docStr, Encoding.UTF8);
            var spec = new TypeSpec(
                name: name,
                basicSize: Util.ReadInt32(Runtime.Runtime.PyBaseObjectType, TypeOffset.tp_basicsize),
                slots: new TypeSpec.Slot[] {
                    new (TypeSlotID.tp_doc, doc.RawPointer),
                },
                TypeFlags.Default | TypeFlags.HeapType
            );

            using var type = new PyType(spec);
            Assert.AreEqual(name, type.GetAttr("__name__").As<string>());
            Assert.AreEqual(name, type.Name);
            Assert.AreEqual(docStr, type.GetAttr("__doc__").As<string>());
        }
    }
}
