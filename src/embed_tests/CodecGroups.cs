namespace Python.EmbeddingTest
{
    using System;
    using System.Linq;
    using NUnit.Framework;
    using Python.Runtime;
    using Python.Runtime.Codecs;

    public class CodecGroups
    {
        [Test]
        public void GetEncodersByType()
        {
            var encoder1 = new ObjectToEncoderInstanceEncoder<Uri>();
            var encoder2 = new ObjectToEncoderInstanceEncoder<Uri>();
            var group = new EncoderGroup {
                new ObjectToEncoderInstanceEncoder<Tuple<int>>(),
                encoder1,
                encoder2,
            };

            var got = group.GetEncoders(typeof(Uri)).ToArray();
            CollectionAssert.AreEqual(new[]{encoder1, encoder2}, got);
        }

        [Test]
        public void CanEncode()
        {
            var group = new EncoderGroup {
                new ObjectToEncoderInstanceEncoder<Tuple<int>>(),
                new ObjectToEncoderInstanceEncoder<Uri>(),
            };

            Assert.IsTrue(group.CanEncode(typeof(Tuple<int>)));
            Assert.IsTrue(group.CanEncode(typeof(Uri)));
            Assert.IsFalse(group.CanEncode(typeof(string)));
        }

        [Test]
        public void Encodes()
        {
            var encoder0 = new ObjectToEncoderInstanceEncoder<Tuple<int>>();
            var encoder1 = new ObjectToEncoderInstanceEncoder<Uri>();
            var encoder2 = new ObjectToEncoderInstanceEncoder<Uri>();
            var group = new EncoderGroup {
                encoder0,
                encoder1,
                encoder2,
            };

            var uri = group.TryEncode(new Uri("data:"));
            var clrObject = (CLRObject)ManagedType.GetManagedObject(uri);
            Assert.AreSame(encoder1, clrObject.inst);
            Assert.AreNotSame(encoder2, clrObject.inst);

            var tuple = group.TryEncode(Tuple.Create(1));
            clrObject = (CLRObject)ManagedType.GetManagedObject(tuple);
            Assert.AreSame(encoder0, clrObject.inst);
        }

        [Test]
        public void GetDecodersByTypes()
        {
            var pyint = new PyInt(10).GetPythonType();
            var pyfloat = new PyFloat(10).GetPythonType();
            var pystr = new PyString("world").GetPythonType();
            var decoder1 = new DecoderReturningPredefinedValue<long>(pyint, decodeResult: 42);
            var decoder2 = new DecoderReturningPredefinedValue<string>(pyfloat, decodeResult: "atad:");
            var group = new DecoderGroup {
                decoder1,
                decoder2,
            };

            var decoder = group.GetDecoder(pyfloat, typeof(string));
            Assert.AreSame(decoder2, decoder);
            decoder = group.GetDecoder(pystr, typeof(string));
            Assert.IsNull(decoder);
            decoder = group.GetDecoder(pyint, typeof(long));
            Assert.AreSame(decoder1, decoder);
        }
        [Test]
        public void CanDecode()
        {
            var pyint = new PyInt(10).GetPythonType();
            var pyfloat = new PyFloat(10).GetPythonType();
            var pystr = new PyString("world").GetPythonType();
            var decoder1 = new DecoderReturningPredefinedValue<long>(pyint, decodeResult: 42);
            var decoder2 = new DecoderReturningPredefinedValue<string>(pyfloat, decodeResult: "atad:");
            var group = new DecoderGroup {
                decoder1,
                decoder2,
            };

            Assert.IsTrue(group.CanDecode(pyint, typeof(long)));
            Assert.IsFalse(group.CanDecode(pyint, typeof(int)));
            Assert.IsTrue(group.CanDecode(pyfloat, typeof(string)));
            Assert.IsFalse(group.CanDecode(pystr, typeof(string)));
        }

        [Test]
        public void Decodes()
        {
            var pyint = new PyInt(10).GetPythonType();
            var pyfloat = new PyFloat(10).GetPythonType();
            var decoder1 = new DecoderReturningPredefinedValue<long>(pyint, decodeResult: 42);
            var decoder2 = new DecoderReturningPredefinedValue<string>(pyfloat, decodeResult: "atad:");
            var group = new DecoderGroup {
                decoder1,
                decoder2,
            };

            Assert.IsTrue(group.TryDecode(new PyInt(10), out long longResult));
            Assert.AreEqual(42, longResult);
            Assert.IsTrue(group.TryDecode(new PyFloat(10), out string strResult));
            Assert.AreSame("atad:", strResult);

            Assert.IsFalse(group.TryDecode(new PyInt(10), out int _));
        }

        [SetUp]
        public void SetUp()
        {
            PythonEngine.Initialize();
        }

        [TearDown]
        public void Dispose()
        {
            PythonEngine.Shutdown();
        }
    }
}
