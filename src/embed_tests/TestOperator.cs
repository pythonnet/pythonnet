using NUnit.Framework;

using Python.Runtime;

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
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            PythonEngine.Shutdown();
        }

        public class OperableObject
        {
            public int Num { get; set; }

            public OperableObject(int num)
            {
                Num = num;
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
");
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

c = a & b
assert c.Num == a.Num & b

c = a | b
assert c.Num == a.Num | b

c = a ^ b
assert c.Num == a.Num ^ b
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

c = a & b
assert c.Num == a & b.Num

c = a | b
assert c.Num == a | b.Num

c = a ^ b
assert c.Num == a ^ b.Num
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
    }
}
