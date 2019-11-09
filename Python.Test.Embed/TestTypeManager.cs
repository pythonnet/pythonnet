using NUnit.Framework;
using Python.Runtime;
using System.Runtime.InteropServices;

namespace Python.EmbeddingTest
{
    class TestTypeManager
    {
        [SetUp]
        public static void Init()
        {
            Runtime.Runtime.Initialize();
        }

        [TearDown]
        public static void Fini()
        {
            // Don't shut down the runtime: if the python engine was initialized
            // but not shut down by another test, we'd end up in a bad state.
        }

        [Test]
        public static void TestNativeCode()
        {
            Assert.That(() => { var _ = TypeManager.NativeCode.Active; }, Throws.Nothing);
            Assert.That(TypeManager.NativeCode.Active.Code.Length, Is.GreaterThan(0));
        }

        [Test]
        public static void TestMemoryMapping()
        {
            Assert.That(() => { var _ = TypeManager.CreateMemoryMapper(); }, Throws.Nothing);
            var mapper = TypeManager.CreateMemoryMapper();

            // Allocate a read-write page.
            int len = 12;
            var page = mapper.MapWriteable(len);
            Assert.That(() => { Marshal.WriteInt64(page, 17); }, Throws.Nothing);
            Assert.That(Marshal.ReadInt64(page), Is.EqualTo(17));

            // Mark it read-execute. We can still read, haven't changed any values.
            mapper.SetReadExec(page, len);
            Assert.That(Marshal.ReadInt64(page), Is.EqualTo(17));

            // Test that we can't write to the protected page.
            //
            // We can't actually test access protection under Microsoft
            // versions of .NET, because AccessViolationException is assumed to
            // mean we're in a corrupted state:
            //   https://stackoverflow.com/questions/3469368/how-to-handle-accessviolationexception
            //
            // We can test under Mono but it throws NRE instead of AccessViolationException.
            //
            // We can't use compiler flags because we compile with MONO_LINUX
            // while running on the Microsoft .NET Core during continuous
            // integration tests.
            /* if (System.Type.GetType ("Mono.Runtime") != null)
            {
                // Mono throws NRE instead of AccessViolationException for some reason.
                Assert.That(() => { Marshal.WriteInt64(page, 73); }, Throws.TypeOf<System.NullReferenceException>());
                Assert.That(Marshal.ReadInt64(page), Is.EqualTo(17));
            } */
        }
    }
}
