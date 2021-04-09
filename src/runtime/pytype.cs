#nullable enable
using System;
using System.Runtime.InteropServices;

using Python.Runtime.Native;

namespace Python.Runtime
{
    public class PyType : PyObject
    {
        /// <summary>Creates heap type object from the <paramref name="spec"/>.</summary>
        public PyType(TypeSpec spec, PyTuple? bases = null) : base(FromSpec(spec, bases)) { }
        /// <summary>Wraps an existing type object.</summary>
        public PyType(PyObject o) : base(FromObject(o)) { }

        /// <summary>Checks if specified object is a Python type.</summary>
        public static bool IsType(PyObject value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));

            return Runtime.PyType_Check(value.obj);
        }

        private static BorrowedReference FromObject(PyObject o)
        {
            if (o is null) throw new ArgumentNullException(nameof(o));
            if (!IsType(o)) throw new ArgumentException("object is not a type");

            return o.Reference;
        }

        private static IntPtr FromSpec(TypeSpec spec, PyTuple? bases = null)
        {
            if (spec is null) throw new ArgumentNullException(nameof(spec));

            if ((spec.Flags & TypeFlags.HeapType) == 0)
                throw new NotSupportedException("Only heap types are supported");

            var nativeSpec = new NativeTypeSpec(spec);
            var basesRef = bases is null ? default : bases.Reference;
            var result = Runtime.PyType_FromSpecWithBases(in nativeSpec, basesRef);

            PythonException.ThrowIfIsNull(result);

            nativeSpec.Dispose();

            return result.DangerousMoveToPointer();
        }
    }
}
