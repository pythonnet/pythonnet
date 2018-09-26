using NUnit.Framework;
using Python.Runtime;
using System.Runtime.InteropServices;

namespace Python.EmbeddingTest
{
    class TestTypeManager
    {
        [Test]
        public static void TestNativeCode()
        {
            Runtime.Runtime.Initialize();

            Assert.That(() => { var _ = TypeManager.NativeCode.Active; }, Throws.Nothing);
            Assert.That(TypeManager.NativeCode.Active.Code.Length, Is.GreaterThan(0));

            Runtime.Runtime.Shutdown();
        }

        [Test]
        public static void TestMemoryMapping()
        {
            Runtime.Runtime.Initialize();

            Assert.That(() => { var _ = TypeManager.CreateMemoryMapper(); }, Throws.Nothing);
            var mapper = TypeManager.CreateMemoryMapper();

            // Allocate a read-write page.
            int len = 12;
            var page = mapper.MapWriteable(len);
            Assert.That(() => { Marshal.WriteInt64(page, 17); }, Throws.Nothing);
            Assert.That(Marshal.ReadInt64(page), Is.EqualTo(17));

            // Mark it read-execute, now we can't write anymore (I'm not testing we can execute).
            // We should be getting AccessViolationException, but Mono translates
            // SIGSEGV to NullReferenceException instead, so just check for some exception.
            mapper.SetReadExec(page, len);
            Assert.That(Marshal.ReadInt64(page), Is.EqualTo(17));
            Assert.That(() => { Marshal.WriteInt64(page, 18); }, Throws.Exception);
            Assert.That(Marshal.ReadInt64(page), Is.EqualTo(17));

            Runtime.Runtime.Shutdown();
        }
    }
}
