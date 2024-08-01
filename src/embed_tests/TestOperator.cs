using NUnit.Framework;

using Python.Runtime;
using Python.Runtime.Codecs;

using System;
using System.Linq;
using System.Reflection;

namespace Python.EmbeddingTest
{
    public class TestOperator
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            PythonEngine.Initialize();
            OwnIntCodec.Setup();
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            PythonEngine.Shutdown();
        }

        // Mock Integer class to test math ops on non-native dotnet types
        public readonly struct OwnInt
        {
            private readonly int _value;

            public int Num => _value;

            public OwnInt()
            {
                _value = 0;
            }

            public OwnInt(int value)
            {
                _value = value;
            }

            public override int GetHashCode()
            {
                return unchecked(65535 + _value.GetHashCode());
            }

            public override bool Equals(object obj)
            {
                return obj is OwnInt @object &&
                       Num == @object.Num;
            }

            public static OwnInt operator -(OwnInt p1, OwnInt p2)
            {
                return new OwnInt(p1._value - p2._value);
            }

            public static OwnInt operator +(OwnInt p1, OwnInt p2)
            {
                return new OwnInt(p1._value + p2._value);
            }

            public static OwnInt operator *(OwnInt p1, OwnInt p2)
            {
                return new OwnInt(p1._value * p2._value);
            }

            public static OwnInt operator /(OwnInt p1, OwnInt p2)
            {
                return new OwnInt(p1._value / p2._value);
            }

            public static OwnInt operator %(OwnInt p1, OwnInt p2)
            {
                return new OwnInt(p1._value % p2._value);
            }

            public static OwnInt operator ^(OwnInt p1, OwnInt p2)
            {
                return new OwnInt(p1._value ^ p2._value);
            }

            public static bool operator <(OwnInt p1, OwnInt p2)
            {
                return p1._value < p2._value;
            }

            public static bool operator >(OwnInt p1, OwnInt p2)
            {
                return p1._value > p2._value;
            }

            public static bool operator ==(OwnInt p1, OwnInt p2)
            {
                return p1._value == p2._value;
            }

            public static bool operator !=(OwnInt p1, OwnInt p2)
            {
                return p1._value != p2._value;
            }

            public static OwnInt operator |(OwnInt p1, OwnInt p2)
            {
                return new OwnInt(p1._value | p2._value);
            }

            public static OwnInt operator &(OwnInt p1, OwnInt p2)
            {
                return new OwnInt(p1._value & p2._value);
            }

            public static bool operator <=(OwnInt p1, OwnInt p2)
            {
                return p1._value <= p2._value;
            }

            public static bool operator >=(OwnInt p1, OwnInt p2)
            {
                return p1._value >= p2._value;
            }
        }

        // Codec for mock class above.
        public class OwnIntCodec : IPyObjectDecoder
        {
            public static void Setup()
            {
                PyObjectConversions.RegisterDecoder(new OwnIntCodec());
            }

            public bool CanDecode(PyType objectType, Type targetType)
            {
                return objectType.Name == "int" && targetType == typeof(OwnInt);
            }

            public bool TryDecode<T>(PyObject pyObj, out T value)
            {
                value = (T)(object)new OwnInt(pyObj.As<int>());
                return true;
            }
        }

        public class OperableObject
        {
            public int Num { get; set; }

            public override int GetHashCode()
            {
                return unchecked(159832395 + Num.GetHashCode());
            }

            public override bool Equals(object obj)
            {
                return obj is OperableObject @object &&
                       Num == @object.Num;
            }

            public OperableObject(int num)
            {
                Num = num;
            }

            public static OperableObject operator ~(OperableObject a)
            {
                return new OperableObject(~a.Num);
            }

            public static OperableObject operator +(OperableObject a)
            {
                return new OperableObject(+a.Num);
            }

            public static OperableObject operator -(OperableObject a)
            {
                return new OperableObject(-a.Num);
            }

            public static OperableObject operator +(int a, OperableObject b)
            {
                return new OperableObject(a + b.Num);
            }
            public static OperableObject operator +(OperableObject a, OperableObject b)
            {
                return new OperableObject(a.Num + b.Num);
            }
            public static OperableObject operator +(OperableObject a, int b)
            {
                return new OperableObject(a.Num + b);
            }

            public static OperableObject operator -(int a, OperableObject b)
            {
                return new OperableObject(a - b.Num);
            }
            public static OperableObject operator -(OperableObject a, OperableObject b)
            {
                return new OperableObject(a.Num - b.Num);
            }
            public static OperableObject operator -(OperableObject a, int b)
            {
                return new OperableObject(a.Num - b);
            }

            public static OperableObject operator *(int a, OperableObject b)
            {
                return new OperableObject(a * b.Num);
            }
            public static OperableObject operator *(OperableObject a, OperableObject b)
            {
                return new OperableObject(a.Num * b.Num);
            }
            public static OperableObject operator *(OperableObject a, int b)
            {
                return new OperableObject(a.Num * b);
            }

            public static OperableObject operator /(int a, OperableObject b)
            {
                return new OperableObject(a / b.Num);
            }
            public static OperableObject operator /(OperableObject a, OperableObject b)
            {
                return new OperableObject(a.Num / b.Num);
            }
            public static OperableObject operator /(OperableObject a, int b)
            {
                return new OperableObject(a.Num / b);
            }

            public static OperableObject operator %(int a, OperableObject b)
            {
                return new OperableObject(a % b.Num);
            }
            public static OperableObject operator %(OperableObject a, OperableObject b)
            {
                return new OperableObject(a.Num % b.Num);
            }
            public static OperableObject operator %(OperableObject a, int b)
            {
                return new OperableObject(a.Num % b);
            }

            public static OperableObject operator &(int a, OperableObject b)
            {
                return new OperableObject(a & b.Num);
            }
            public static OperableObject operator &(OperableObject a, OperableObject b)
            {
                return new OperableObject(a.Num & b.Num);
            }
            public static OperableObject operator &(OperableObject a, int b)
            {
                return new OperableObject(a.Num & b);
            }

            public static OperableObject operator |(int a, OperableObject b)
            {
                return new OperableObject(a | b.Num);
            }
            public static OperableObject operator |(OperableObject a, OperableObject b)
            {
                return new OperableObject(a.Num | b.Num);
            }
            public static OperableObject operator |(OperableObject a, int b)
            {
                return new OperableObject(a.Num | b);
            }

            public static OperableObject operator ^(int a, OperableObject b)
            {
                return new OperableObject(a ^ b.Num);
            }
            public static OperableObject operator ^(OperableObject a, OperableObject b)
            {
                return new OperableObject(a.Num ^ b.Num);
            }
            public static OperableObject operator ^(OperableObject a, int b)
            {
                return new OperableObject(a.Num ^ b);
            }

            public static bool operator ==(int a, OperableObject b)
            {
                return (a == b.Num);
            }
            public static bool operator ==(OperableObject a, OperableObject b)
            {
                return (a.Num == b.Num);
            }
            public static bool operator ==(OperableObject a, int b)
            {
                return (a.Num == b);
            }

            public static bool operator !=(int a, OperableObject b)
            {
                return (a != b.Num);
            }
            public static bool operator !=(OperableObject a, OperableObject b)
            {
                return (a.Num != b.Num);
            }
            public static bool operator !=(OperableObject a, int b)
            {
                return (a.Num != b);
            }

            public static bool operator <=(int a, OperableObject b)
            {
                return (a <= b.Num);
            }
            public static bool operator <=(OperableObject a, OperableObject b)
            {
                return (a.Num <= b.Num);
            }
            public static bool operator <=(OperableObject a, int b)
            {
                return (a.Num <= b);
            }

            public static bool operator >=(int a, OperableObject b)
            {
                return (a >= b.Num);
            }
            public static bool operator >=(OperableObject a, OperableObject b)
            {
                return (a.Num >= b.Num);
            }
            public static bool operator >=(OperableObject a, int b)
            {
                return (a.Num >= b);
            }

            public static bool operator >=(OperableObject a, (int, int) b)
            {
                using (Py.GIL())
                {
                    int bNum = b.Item1;
                    return a.Num >= bNum;
                }
            }
            public static bool operator <=(OperableObject a, (int, int) b)
            {
                using (Py.GIL())
                {
                    int bNum = b.Item1;
                    return a.Num <= bNum;
                }
            }

            public static bool operator <(int a, OperableObject b)
            {
                return (a < b.Num);
            }
            public static bool operator <(OperableObject a, OperableObject b)
            {
                return (a.Num < b.Num);
            }
            public static bool operator <(OperableObject a, int b)
            {
                return (a.Num < b);
            }

            public static bool operator >(int a, OperableObject b)
            {
                return (a > b.Num);
            }
            public static bool operator >(OperableObject a, OperableObject b)
            {
                return (a.Num > b.Num);
            }
            public static bool operator >(OperableObject a, int b)
            {
                return (a.Num > b);
            }

            public static OperableObject operator <<(OperableObject a, int offset)
            {
                return new OperableObject(a.Num << offset);
            }

            public static OperableObject operator >>(OperableObject a, int offset)
            {
                return new OperableObject(a.Num >> offset);
            }
        }

        [Test]
        public void SymmetricalOperatorOverloads()
        {
            string name = string.Format("{0}.{1}",
                typeof(OperableObject).DeclaringType.Name,
                typeof(OperableObject).Name);
            string module = MethodBase.GetCurrentMethod().DeclaringType.Namespace;

            PythonEngine.Exec($@"
from {module} import *
cls = {name}
a = cls(-2)
b = cls(10)
c = ~a
assert c.Num == ~a.Num

c = +a
assert c.Num == +a.Num

a = cls(2)
c = -a
assert c.Num == -a.Num

c = a + b
assert c.Num == a.Num + b.Num

c = a - b
assert c.Num == a.Num - b.Num

c = a * b
assert c.Num == a.Num * b.Num

c = a / b
assert c.Num == a.Num // b.Num

c = a % b
assert c.Num == a.Num % b.Num

c = a & b
assert c.Num == a.Num & b.Num

c = a | b
assert c.Num == a.Num | b.Num

c = a ^ b
assert c.Num == a.Num ^ b.Num

c = a == b
assert c == (a.Num == b.Num)

c = a != b
assert c == (a.Num != b.Num)

c = a <= b
assert c == (a.Num <= b.Num)

c = a >= b
assert c == (a.Num >= b.Num)

c = a < b
assert c == (a.Num < b.Num)

c = a > b
assert c == (a.Num > b.Num)
");
        }

        [Test]
        public void EnumOperator()
        {
            PythonEngine.Exec($@"
from System.IO import FileAccess
c = FileAccess.Read | FileAccess.Write");
        }

        [Test]
        public void OperatorOverloadMissingArgument()
        {
            string name = string.Format("{0}.{1}",
                typeof(OperableObject).DeclaringType.Name,
                typeof(OperableObject).Name);
            string module = MethodBase.GetCurrentMethod().DeclaringType.Namespace;

            Assert.Throws<PythonException>(() =>
            PythonEngine.Exec($@"
from {module} import *
cls = {name}
a = cls(2)
b = cls(10)
a.op_Addition()
"));
        }

        [Test]
        public void ForwardOperatorOverloads()
        {
            string name = string.Format("{0}.{1}",
                typeof(OperableObject).DeclaringType.Name,
                typeof(OperableObject).Name);
            string module = MethodBase.GetCurrentMethod().DeclaringType.Namespace;

            PythonEngine.Exec($@"
from {module} import *
cls = {name}
a = cls(2)
b = 10
c = a + b
assert c.Num == a.Num + b

c = a - b
assert c.Num == a.Num - b

c = a * b
assert c.Num == a.Num * b

c = a / b
assert c.Num == a.Num // b

c = a % b
assert c.Num == a.Num % b

c = a & b
assert c.Num == a.Num & b

c = a | b
assert c.Num == a.Num | b

c = a ^ b
assert c.Num == a.Num ^ b

c = a == b
assert c == (a.Num == b)

c = a != b
assert c == (a.Num != b)

c = a <= b
assert c == (a.Num <= b)

c = a >= b
assert c == (a.Num >= b)

c = a < b
assert c == (a.Num < b)

c = a > b
assert c == (a.Num > b)
");
        }

        [Test]
        public void TupleComparisonOperatorOverloads()
        {
                TupleCodec<ValueTuple>.Register();
                string name = string.Format("{0}.{1}",
                typeof(OperableObject).DeclaringType.Name,
                typeof(OperableObject).Name);
            string module = MethodBase.GetCurrentMethod().DeclaringType.Namespace;
                PythonEngine.Exec($@"
from {module} import *
cls = {name}
a = cls(2)
b = (1, 2)

c = a >= b
assert c == (a.Num >= b[0])

c = a <= b
assert c == (a.Num <= b[0])

c = b >= a
assert c == (b[0] >= a.Num)

c = b <= a
assert c == (b[0] <= a.Num)
");
        }


        [Test]
        public void ReverseOperatorOverloads()
        {
            string name = string.Format("{0}.{1}",
                typeof(OperableObject).DeclaringType.Name,
                typeof(OperableObject).Name);
            string module = MethodBase.GetCurrentMethod().DeclaringType.Namespace;

            PythonEngine.Exec($@"
from {module} import *
cls = {name}
a = 2
b = cls(10)

c = a + b
assert c.Num == a + b.Num

c = a - b
assert c.Num == a - b.Num

c = a * b
assert c.Num == a * b.Num

c = a / b
assert c.Num == a // b.Num

c = a % b
assert c.Num == a % b.Num

c = a & b
assert c.Num == a & b.Num

c = a | b
assert c.Num == a | b.Num

c = a ^ b
assert c.Num == a ^ b.Num

c = a == b
assert c == (a == b.Num)

c = a != b
assert c == (a != b.Num)

c = a <= b
assert c == (a <= b.Num)

c = a >= b
assert c == (a >= b.Num)

c = a < b
assert c == (a < b.Num)

c = a > b
assert c == (a > b.Num)
");

        }
        [Test]
        public void ShiftOperatorOverloads()
        {
            string name = string.Format("{0}.{1}",
                typeof(OperableObject).DeclaringType.Name,
                typeof(OperableObject).Name);
            string module = MethodBase.GetCurrentMethod().DeclaringType.Namespace;

            PythonEngine.Exec($@"
from {module} import *
cls = {name}
a = cls(2)
b = cls(10)

c = a << b.Num
assert c.Num == a.Num << b.Num

c = a >> b.Num
assert c.Num == a.Num >> b.Num
");
        }

        [Test]
        public void ReverseOperatorWithCodec()
        {
            string name = string.Format("{0}.{1}",
                typeof(OwnInt).DeclaringType.Name,
                typeof(OwnInt).Name);
            string module = MethodBase.GetCurrentMethod().DeclaringType.Namespace;

            PythonEngine.Exec($@"
from {module} import *
cls = {name}
a = 2
b = cls(10)

c = a + b
assert c.Num == a + b.Num

c = a - b
assert c.Num == a - b.Num

c = a * b
assert c.Num == a * b.Num

c = a / b
assert c.Num == a // b.Num

c = a % b
assert c.Num == a % b.Num

c = a & b
assert c.Num == a & b.Num

c = a | b
assert c.Num == a | b.Num

c = a ^ b
assert c.Num == a ^ b.Num

c = a == b
assert c == (a == b.Num)

c = a != b
assert c == (a != b.Num)

c = a <= b
assert c == (a <= b.Num)

c = a >= b
assert c == (a >= b.Num)

c = a < b
assert c == (a < b.Num)

c = a > b
assert c == (a > b.Num)
");
        }

        [Test]
        public void ForwardOperatorWithCodec()
        {
            string name = string.Format("{0}.{1}",
                 typeof(OwnInt).DeclaringType.Name,
                 typeof(OwnInt).Name);
            string module = MethodBase.GetCurrentMethod().DeclaringType.Namespace;

            PythonEngine.Exec($@"
from {module} import *
cls = {name}
a = cls(2)
b = 10
c = a + b
assert c.Num == a.Num + b

c = a - b
assert c.Num == a.Num - b

c = a * b
assert c.Num == a.Num * b

c = a / b
assert c.Num == a.Num // b

c = a % b
assert c.Num == a.Num % b

c = a & b
assert c.Num == a.Num & b

c = a | b
assert c.Num == a.Num | b

c = a ^ b
assert c.Num == a.Num ^ b

c = a == b
assert c == (a.Num == b)

c = a != b
assert c == (a.Num != b)

c = a <= b
assert c == (a.Num <= b)

c = a >= b
assert c == (a.Num >= b)

c = a < b
assert c == (a.Num < b)

c = a > b
assert c == (a.Num > b)
");
        }
    }
}
