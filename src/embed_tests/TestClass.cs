using System;
using System.Runtime.InteropServices;

using NUnit.Framework;

using Python.Runtime;

using PyRuntime = Python.Runtime.Runtime;

namespace Python.EmbeddingTest
{
    public class TestClass
    {
        public class MyClass
        {
        }

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
        public void WeakRefForClrObject()
        {
            var obj = new MyClass();
            using var scope = Py.CreateScope();
            scope.Set("clr_obj", obj);
            scope.Exec(@"
import weakref
ref = weakref.ref(clr_obj)
");
            using PyObject pyobj = scope.Get("clr_obj");
            ValidateAttachedGCHandle(obj, pyobj.Handle);
        }

        [Test]
        public void WeakRefForSubClass()
        {
            using (var scope = Py.CreateScope())
            {
                scope.Exec(@"
from Python.EmbeddingTest import TestClass
import weakref

class Sub(TestClass.MyClass):
    pass

obj = Sub()
ref = weakref.ref(obj)
");
                using (PyObject pyobj = scope.Get("obj"))
                {
                    IntPtr op = pyobj.Handle;
                    IntPtr type = PyRuntime.PyObject_TYPE(op);
                    IntPtr clrHandle = Marshal.ReadIntPtr(op, ObjectOffset.magic(type));
                    var clobj = (CLRObject)GCHandle.FromIntPtr(clrHandle).Target;
                    Assert.IsTrue(clobj.inst is MyClass);
                }
            }
        }

        private static void ValidateAttachedGCHandle(object obj, IntPtr op)
        {
            IntPtr type = PyRuntime.PyObject_TYPE(op);
            IntPtr clrHandle = Marshal.ReadIntPtr(op, ObjectOffset.magic(type));
            var clobj = (CLRObject)GCHandle.FromIntPtr(clrHandle).Target;
            Assert.True(ReferenceEquals(clobj.inst, obj));
        }
    }
}
