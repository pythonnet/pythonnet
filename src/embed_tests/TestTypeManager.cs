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

            // Mark it read-execute, now we can't write anymore.
            // We can't actually test access protectoin under Windows,
            // because AccessViolationException is assumed to mean we're in a
            // corrupted state:
            //   https://stackoverflow.com/questions/3469368/how-to-handle-accessviolationexception
            mapper.SetReadExec(page, len);
            Assert.That(Marshal.ReadInt64(page), Is.EqualTo(17));
            if (Runtime.Runtime.OperatingSystem != Runtime.Runtime.OperatingSystemType.Windows)
            {
                // Mono throws NRE instead of AccessViolationException for some reason.
                Assert.That(() => { Marshal.WriteInt64(page, 73); }, Throws.TypeOf<System.NullReferenceException>());
                Assert.That(Marshal.ReadInt64(page), Is.EqualTo(17));
            }

            Runtime.Runtime.Shutdown();
        }
    }
}
