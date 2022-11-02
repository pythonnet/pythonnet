using System;
using System.Diagnostics;
using System.Runtime.Serialization;

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

        internal PyType(PyType o)
            : base(o is not null ? o.Reference : throw new ArgumentNullException(nameof(o)))
        {
        }

        internal PyType(BorrowedReference reference, bool prevalidated = false) : base(reference)
        {
            if (prevalidated) return;

            if (!Runtime.PyType_Check(this))
                throw new ArgumentException("object is not a type");
        }

        internal PyType(in StolenReference reference, bool prevalidated = false) : base(reference)
        {
            if (prevalidated) return;

            if (!Runtime.PyType_Check(this))
                throw new ArgumentException("object is not a type");
        }

        protected PyType(SerializationInfo info, StreamingContext context) : base(info, context) { }

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
                    RawPointer = Util.ReadIntPtr(this, TypeOffset.tp_name),
                };
                return namePtr.ToString(System.Text.Encoding.UTF8)!;
            }
        }

        /// <summary>Returns <c>true</c> when type is fully initialized</summary>
        public bool IsReady => Flags.HasFlag(TypeFlags.Ready);

        internal TypeFlags Flags
        {
            get => (TypeFlags)Util.ReadCLong(this, TypeOffset.tp_flags);
            set => Util.WriteCLong(this, TypeOffset.tp_flags, (long)value);
        }

        internal PyDict Dict => new(Util.ReadRef(this, TypeOffset.tp_dict));

        internal PyTuple MRO => new(GetMRO(this));

        /// <summary>Checks if specified object is a Python type.</summary>
        public static bool IsType(PyObject value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));

            return Runtime.PyType_Check(value.obj);
        }
        /// <summary>Checks if specified object is a Python type.</summary>
        internal static bool IsType(BorrowedReference value)
        {
            return Runtime.PyType_Check(value);
        }

        /// <summary>
        /// Gets <see cref="PyType"/>, which represents the specified CLR type.
        /// </summary>
        public static PyType Get(Type clrType)
        {
            if (clrType is null)
            {
                throw new ArgumentNullException(nameof(clrType));
            }

            return new PyType(ClassManager.GetClass(clrType));
        }

        internal BorrowedReference BaseReference
        {
            get => GetBase(Reference);
            set => Runtime.ReplaceReference(this, TypeOffset.tp_base, new NewReference(value).Steal());
        }

        internal IntPtr GetSlot(TypeSlotID slot)
        {
            IntPtr result = Runtime.PyType_GetSlot(this.Reference, slot);
            return Exceptions.ErrorCheckIfNull(result);
        }

        internal static TypeFlags GetFlags(BorrowedReference type)
        {
            Debug.Assert(TypeOffset.tp_flags > 0);
            return (TypeFlags)Util.ReadCLong(type, TypeOffset.tp_flags);
        }
        internal static void SetFlags(BorrowedReference type, TypeFlags flags)
        {
            Debug.Assert(TypeOffset.tp_flags > 0);
            Util.WriteCLong(type, TypeOffset.tp_flags, (long)flags);
        }

        internal static BorrowedReference GetBase(BorrowedReference type)
        {
            Debug.Assert(IsType(type));
            return Util.ReadRef(type, TypeOffset.tp_base);
        }

        internal static BorrowedReference GetBases(BorrowedReference type)
        {
            Debug.Assert(IsType(type));
            return Util.ReadRef(type, TypeOffset.tp_bases);
        }

        internal static BorrowedReference GetMRO(BorrowedReference type)
        {
            Debug.Assert(IsType(type));
            return Util.ReadRef(type, TypeOffset.tp_mro);
        }

        private static BorrowedReference FromObject(PyObject o)
        {
            if (o is null) throw new ArgumentNullException(nameof(o));
            if (!IsType(o)) throw new ArgumentException("object is not a type");

            return o.Reference;
        }

        private static StolenReference FromSpec(TypeSpec spec, PyTuple? bases = null)
        {
            if (spec is null) throw new ArgumentNullException(nameof(spec));

            if ((spec.Flags & TypeFlags.HeapType) == 0)
                throw new NotSupportedException("Only heap types are supported");

            using var nativeSpec = new NativeTypeSpec(spec);
            var basesRef = bases is null ? default : bases.Reference;
            var result = Runtime.PyType_FromSpecWithBases(in nativeSpec, basesRef);
            // Runtime.PyErr_Print();
            return result.StealOrThrow();
        }
    }
}
