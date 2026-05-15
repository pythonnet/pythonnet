using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using NUnit.Framework;
using Python.Runtime;
using Python.Runtime.Codecs;

namespace Python.EmbeddingTest {
    class TestPyBuffer
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            TupleCodec<ValueTuple>.Register();
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            PyObjectConversions.Reset();
        }

        [Test]
        public void TestBufferWrite()
        {
            string bufferTestString = "hello world! !$%&/()=?";
            string bufferTestString2 = "h llo world! !$%&/()=?";

            using var _ = Py.GIL();

            using var pythonArray = ByteArrayFromAsciiString(bufferTestString);

            using (PyBuffer buf = pythonArray.GetBuffer(PyBUF.WRITABLE))
            {
                byte[] managedArray = { (byte)' ' };
                buf.Write(managedArray, 0, managedArray.Length, 1);
            }

            string result = pythonArray.InvokeMethod("decode", "utf-8".ToPython()).As<string>();
            Assert.That(result == bufferTestString2, Is.True);
        }

        [Test]
        public void TestBufferRead()
        {
            string bufferTestString = "hello world! !$%&/()=?";

            using var _ = Py.GIL();

            using var pythonArray = ByteArrayFromAsciiString(bufferTestString);
            byte[] managedArray = new byte[bufferTestString.Length];

            using (PyBuffer buf = pythonArray.GetBuffer())
            {
                managedArray[0] = (byte)' ';
                buf.Read(managedArray, 1, managedArray.Length - 1, 1);
            }

            string result = new UTF8Encoding().GetString(managedArray);
            Assert.That(result == " " + bufferTestString.Substring(1), Is.True);
        }

        [Test]
        public void ArrayHasBuffer()
        {
            var array = new[,] {{1, 2}, {3,4}};
            var memoryView = PythonEngine.Eval("memoryview");
            var mem = memoryView.Invoke(array.ToPython());
            Assert.That(mem[(0, 0).ToPython()].As<int>(), Is.EqualTo(1));
            Assert.That(mem[(1, 0).ToPython()].As<int>(), Is.EqualTo(array[1, 0]));
        }

        [Test]
        public void RefCount()
        {
            using var _ = Py.GIL();
            using var arr = ByteArrayFromAsciiString("hello world! !$%&/()=?");

            Assert.That(arr.Refcount, Is.EqualTo(1));

            using (PyBuffer buf = arr.GetBuffer())
            {
                Assert.That(arr.Refcount, Is.EqualTo(2));
            }

            Assert.That(arr.Refcount, Is.EqualTo(1));
        }

        [Test]
        public void Finalization()
        {
            if (Type.GetType("Mono.Runtime") is not null)
            {
                Assert.Inconclusive("test unreliable in Mono");
                return;
            }

            using var _ = Py.GIL();
            using var arr = ByteArrayFromAsciiString("hello world! !$%&/()=?");

            Assert.That(arr.Refcount, Is.EqualTo(1));

            MakeBufAndLeak(arr);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            Finalizer.Instance.Collect();

            Assert.That(arr.Refcount, Is.EqualTo(1));
        }

        [Test]
        public void MultidimensionalNumPyArray()
        {
            var ndarray = np.arange(24).reshape(1,2,3,4).T;
            PyObject ndim = ndarray.ndim;
            PyObject shape = ndarray.shape;
            PyObject strides = ndarray.strides;
            PyObject contiguous = ndarray.flags["C_CONTIGUOUS"];

            using PyBuffer buf = ndarray.GetBuffer(PyBUF.STRIDED);

            Assert.Multiple(() =>
            {
                Assert.That(buf.Dimensions, Is.EqualTo(ndim.As<int>()));
                Assert.That(buf.Shape, Is.EqualTo(shape.As<long[]>()));
                Assert.That(buf.Strides, Is.EqualTo(strides.As<long[]>()));
                Assert.That(buf.IsContiguous(BufferOrderStyle.C), Is.EqualTo(contiguous.As<bool>()));
            });
        }

        [Test]
        public void ConcurrentDispose()
        {
            // Two threads racing on Dispose() must not double-release the view —
            // Interlocked.Exchange on disposedValue gates PyBuffer_Release.
            // Smoke test: no crash, exception, or buffer-protocol violation.
            using var _ = Py.GIL();
            using var arr = ByteArrayFromAsciiString("hello world! !$%&/()=?");

            const int iterations = 200;
            for (int i = 0; i < iterations; i++)
            {
                PyBuffer buf = arr.GetBuffer();

                IntPtr ts = PythonEngine.BeginAllowThreads();
                using var barrier = new Barrier(2);
                Exception captured = null;
                Action race = () =>
                {
                    try
                    {
                        barrier.SignalAndWait();
                        using (Py.GIL()) buf.Dispose();
                    }
                    catch (Exception ex) { Interlocked.CompareExchange(ref captured, ex, null); }
                };
                var t1 = new Thread(() => race());
                var t2 = new Thread(() => race());
                t1.Start(); t2.Start();
                t1.Join(); t2.Join();
                PythonEngine.EndAllowThreads(ts);

                if (captured != null) throw captured;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void MakeBufAndLeak(PyObject bufProvider)
        {
            PyBuffer buf = bufProvider.GetBuffer();
        }

        static PyObject ByteArrayFromAsciiString(string str)
        {
            using var scope = Py.CreateScope();
            return Runtime.Runtime.PyByteArray_FromStringAndSize(str).MoveToPyObject();
        }

        dynamic np
        {
            get
            {
                try
                {
                    return Py.Import("numpy");
                }
                catch (PythonException)
                {
                    Assert.Inconclusive("Numpy or dependency not installed");
                    return null;
                }
            }
        }
    }
}
