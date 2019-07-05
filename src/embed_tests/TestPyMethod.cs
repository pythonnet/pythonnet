using NUnit.Framework;
using Python.Runtime;
using System.Linq;
using System.Reflection;

namespace Python.EmbeddingTest
{
    public class TestPyMethod
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

        public class SampleClass
        {
            public int VoidCall() => 10;

            public int Foo(int a, int b = 10) => a + b;

            public int Foo2(int a = 10, params int[] args)
            {
                return a + args.Sum();
            }
        }

        [Test]
        public void TestVoidCall()
        {
            string name = string.Format("{0}.{1}",
                typeof(SampleClass).DeclaringType.Name,
                typeof(SampleClass).Name);
            string module = MethodBase.GetCurrentMethod().DeclaringType.Namespace;
            PythonEngine.Exec($@"
from {module} import *
SampleClass = {name}
obj = SampleClass()
assert obj.VoidCall() == 10
");
        }

        [Test]
        public void TestDefaultParameter()
        {
            string name = string.Format("{0}.{1}",
                typeof(SampleClass).DeclaringType.Name,
                typeof(SampleClass).Name);
            string module = MethodBase.GetCurrentMethod().DeclaringType.Namespace;

            PythonEngine.Exec($@"
from {module} import *
SampleClass = {name}
obj = SampleClass()
assert obj.Foo(10) == 20
assert obj.Foo(10, 1) == 11

assert obj.Foo2() == 10
assert obj.Foo2(20) == 20
assert obj.Foo2(20, 30) == 50
assert obj.Foo2(20, 30, 50) == 100
");
        }

        public class OperableObject
        {
            public int Num { get; set; }

            public OperableObject(int num)
            {
                Num = num;
            }

            public static OperableObject operator +(OperableObject a, OperableObject b)
            {
                return new OperableObject(a.Num + b.Num);
            }

            public static OperableObject operator -(OperableObject a, OperableObject b)
            {
                return new OperableObject(a.Num - b.Num);
            }

            public static OperableObject operator *(OperableObject a, OperableObject b)
            {
                return new OperableObject(a.Num * b.Num);
            }

            public static OperableObject operator /(OperableObject a, OperableObject b)
            {
                return new OperableObject(a.Num / b.Num);
            }

            public static OperableObject operator &(OperableObject a, OperableObject b)
            {
                return new OperableObject(a.Num & b.Num);
            }

            public static OperableObject operator |(OperableObject a, OperableObject b)
            {
                return new OperableObject(a.Num | b.Num);
            }

            public static OperableObject operator ^(OperableObject a, OperableObject b)
            {
                return new OperableObject(a.Num ^ b.Num);
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
        public void OperatorOverloads()
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
c = a + b
assert c.Num == a.Num + b.Num

c = a - b
assert c.Num == a.Num - b.Num

c = a * b
assert c.Num == a.Num * b.Num

c = a / b
assert c.Num == a.Num // b.Num

c = a & b
assert c.Num == a.Num & b.Num

c = a | b
assert c.Num == a.Num | b.Num

c = a ^ b
assert c.Num == a.Num ^ b.Num

c = a << b.Num
assert c.Num == a.Num << b.Num

c = a >> b.Num
assert c.Num == a.Num >> b.Num
");
        }
    }
}
