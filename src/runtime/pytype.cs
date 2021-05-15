#nullable enable
using System;
using System.Runtime.InteropServices;

using Python.Runtime.Native;

namespace Python.Runtime
{
    [Serializable]
    public class PyType : PyObject
    {
        /// <summary>Creates heap type object from the <paramref name="spec"/>.</summary>
        public PyType(TypeSpec spec, PyTuple? bases = null) : base(FromSpec(spec, bases)) { }
        /// <summary>Wraps an existing type object.</summary>
        public PyType(PyObject o) : base(FromObject(o)) { }

        internal PyType(BorrowedReference reference) : base(reference)
        {
            if (!Runtime.PyType_Check(this.Handle))
                throw new ArgumentException("object is not a type");
        }

        internal PyType(StolenReference reference) : base(EnsureIsType(in reference))
        {
        }

        internal new static PyType? FromNullableReference(BorrowedReference reference)
            => reference == null
                ? null
                : new PyType(new NewReference(reference).Steal());

        internal static PyType FromReference(BorrowedReference reference)
            => FromNullableReference(reference) ?? throw new ArgumentNullException(nameof(reference));

        public string Name
        {
            get
            {
                var namePtr = new StrPtr
                {
                    RawPointer = Marshal.ReadIntPtr(Handle, TypeOffset.tp_name),
                };
                return namePtr.ToString(System.Text.Encoding.UTF8)!;
            }
        }

        /// <summary>Checks if specified object is a Python type.</summary>
        public static bool IsType(PyObject value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));

            return Runtime.PyType_Check(value.obj);
        }

        internal IntPtr GetSlot(TypeSlotID slot)
        {
            IntPtr result = Runtime.PyType_GetSlot(this.Reference, slot);
            return Exceptions.ErrorCheckIfNull(result);
        }

        private static IntPtr EnsureIsType(in StolenReference reference)
        {
            IntPtr address = reference.DangerousGetAddressOrNull();
            if (address == IntPtr.Zero)
                throw new ArgumentNullException(nameof(reference));
            return EnsureIsType(address);
        }

        private static IntPtr EnsureIsType(IntPtr ob)
            => Runtime.PyType_Check(ob)
                ? ob
                : throw new ArgumentException("object is not a type");

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
