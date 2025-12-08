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
            Assert.That(got, Is.EqualTo(new[] { encoder1, encoder2 }).AsCollection);
        }

        [Test]
        public void CanEncode()
        {
            var group = new EncoderGroup {
                new ObjectToEncoderInstanceEncoder<Tuple<int>>(),
                new ObjectToEncoderInstanceEncoder<Uri>(),
            };

            Assert.Multiple(() =>
            {
                Assert.That(group.CanEncode(typeof(Tuple<int>)), Is.True);
                Assert.That(group.CanEncode(typeof(Uri)), Is.True);
                Assert.That(group.CanEncode(typeof(string)), Is.False);
            });

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
            Assert.That(clrObject.inst, Is.SameAs(encoder1));
            Assert.That(clrObject.inst, Is.Not.SameAs(encoder2));

            var tuple = group.TryEncode(Tuple.Create(1));
            clrObject = (CLRObject)ManagedType.GetManagedObject(tuple);
            Assert.That(clrObject.inst, Is.SameAs(encoder0));
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
            Assert.That(decoder, Is.SameAs(decoder2));
            decoder = group.GetDecoder(pystr, typeof(string));
            Assert.That(decoder, Is.Null);
            decoder = group.GetDecoder(pyint, typeof(long));
            Assert.That(decoder, Is.SameAs(decoder1));
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

            Assert.Multiple(() =>
            {
                Assert.That(group.CanDecode(pyint, typeof(long)));
                Assert.That(group.CanDecode(pyint, typeof(int)), Is.False);
                Assert.That(group.CanDecode(pyfloat, typeof(string)));
                Assert.That(group.CanDecode(pystr, typeof(string)), Is.False);
            });

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

            Assert.Multiple(() =>
            {
                Assert.That(group.TryDecode(new PyInt(10), out long longResult));
                Assert.That(longResult, Is.EqualTo(42));
                Assert.That(group.TryDecode(new PyFloat(10), out string strResult));
                Assert.That(strResult, Is.SameAs("atad:"));
                Assert.That(group.TryDecode(new PyInt(10), out int _), Is.False);
            });
        }
    }
}
