using System;
using System.Collections;

namespace Python.Runtime
{
    /// <summary>
    /// Implements a Python type for managed arrays. This type is essentially
    /// the same as a ClassObject, except that it provides sequence semantics
    /// to support natural array usage (indexing) from Python.
    /// </summary>
    [Serializable]
    internal class ArrayObject : ClassBase
    {
        internal ArrayObject(Type tp) : base(tp)
        {
        }

        internal override bool CanSubclass()
        {
            return false;
        }

        public static IntPtr tp_new(IntPtr tpRaw, IntPtr args, IntPtr kw)
        {
            if (kw != IntPtr.Zero)
            {
                return Exceptions.RaiseTypeError("array constructor takes no keyword arguments");
            }

            var tp = new BorrowedReference(tpRaw);

            var self = GetManagedObject(tp) as ArrayObject;
            if (!self.type.Valid)
            {
                return Exceptions.RaiseTypeError(self.type.DeletedMessage);
            }
            Type arrType = self.type.Value;

            long[] dimensions = new long[Runtime.PyTuple_Size(args)];
            if (dimensions.Length == 0)
            {
                return Exceptions.RaiseTypeError("array constructor requires at least one integer argument or an object convertible to array");
            }
            if (dimensions.Length != 1)
            {
                return CreateMultidimensional(arrType.GetElementType(), dimensions,
                         shapeTuple: new BorrowedReference(args),
                         pyType: tp)
                       .DangerousMoveToPointerOrNull();
            }

            IntPtr op = Runtime.PyTuple_GetItem(args, 0);

            // create single dimensional array
            if (Runtime.PyInt_Check(op))
            {
                dimensions[0] = Runtime.PyLong_AsSignedSize_t(op);
                if (dimensions[0] == -1 && Exceptions.ErrorOccurred())
                {
                    Exceptions.Clear();
                }
                else
                {
                    return NewInstance(arrType.GetElementType(), tp, dimensions)
                           .DangerousMoveToPointerOrNull();
                }
            }
            object result;

            // this implements casting to Array[T]
            if (!Converter.ToManaged(op, arrType, out result, true))
            {
                return IntPtr.Zero;
            }
            return CLRObject.GetInstHandle(result, tp)
                   .DangerousGetAddress();
        }

        static NewReference CreateMultidimensional(Type elementType, long[] dimensions, BorrowedReference shapeTuple, BorrowedReference pyType)
        {
            for (int dimIndex = 0; dimIndex < dimensions.Length; dimIndex++)
            {
                BorrowedReference dimObj = Runtime.PyTuple_GetItem(shapeTuple, dimIndex);
                PythonException.ThrowIfIsNull(dimObj);

                if (!Runtime.PyInt_Check(dimObj))
                {
                    Exceptions.RaiseTypeError("array constructor expects integer dimensions");
                    return default;
                }

                dimensions[dimIndex] = Runtime.PyLong_AsSignedSize_t(dimObj);
                if (dimensions[dimIndex] == -1 && Exceptions.ErrorOccurred())
                {
                    Exceptions.RaiseTypeError("array constructor expects integer dimensions");
                    return default;
                }
            }

            return NewInstance(elementType, pyType, dimensions);
        }

        static NewReference NewInstance(Type elementType, BorrowedReference arrayPyType, long[] dimensions)
        {
            object result;
            try
            {
                result = Array.CreateInstance(elementType, dimensions);
            }
            catch (ArgumentException badArgument)
            {
                Exceptions.SetError(Exceptions.ValueError, badArgument.Message);
                return default;
            }
            catch (OverflowException overflow)
            {
                Exceptions.SetError(overflow);
                return default;
            }
            catch (NotSupportedException notSupported)
            {
                Exceptions.SetError(notSupported);
                return default;
            }
            catch (OutOfMemoryException oom)
            {
                Exceptions.SetError(Exceptions.MemoryError, oom.Message);
                return default;
            }
            return CLRObject.GetInstHandle(result, arrayPyType);
        }


        /// <summary>
        /// Implements __getitem__ for array types.
        /// </summary>
        public new static IntPtr mp_subscript(IntPtr ob, IntPtr idx)
        {
            var obj = (CLRObject)GetManagedObject(ob);
            var items = obj.inst as Array;
            Type itemType = obj.inst.GetType().GetElementType();
            int rank = items.Rank;
            int index;
            object value;

            // Note that CLR 1.0 only supports int indexes - methods to
            // support long indices were introduced in 1.1. We could
            // support long indices automatically, but given that long
            // indices are not backward compatible and a relative edge
            // case, we won't bother for now.

            // Single-dimensional arrays are the most common case and are
            // cheaper to deal with than multi-dimensional, so check first.

            if (rank == 1)
            {
                if (!Runtime.PyInt_Check(idx))
                {
                    return RaiseIndexMustBeIntegerError(idx);
                }
                index = Runtime.PyInt_AsLong(idx);

                if (Exceptions.ErrorOccurred())
                {
                    return Exceptions.RaiseTypeError("invalid index value");
                }

                if (index < 0)
                {
                    index = items.Length + index;
                }

                try
                {
                    value = items.GetValue(index);
                }
                catch (IndexOutOfRangeException)
                {
                    Exceptions.SetError(Exceptions.IndexError, "array index out of range");
                    return IntPtr.Zero;
                }

                return Converter.ToPython(value, itemType);
            }

            // Multi-dimensional arrays can be indexed a la: list[1, 2, 3].

            if (!Runtime.PyTuple_Check(idx))
            {
                Exceptions.SetError(Exceptions.TypeError, "invalid index value");
                return IntPtr.Zero;
            }

            var count = Runtime.PyTuple_Size(idx);

            var args = new int[count];

            for (var i = 0; i < count; i++)
            {
                IntPtr op = Runtime.PyTuple_GetItem(idx, i);
                if (!Runtime.PyInt_Check(op))
                {
                    return RaiseIndexMustBeIntegerError(op);
                }
                index = Runtime.PyInt_AsLong(op);

                if (Exceptions.ErrorOccurred())
                {
                    return Exceptions.RaiseTypeError("invalid index value");
                }

                if (index < 0)
                {
                    index = items.GetLength(i) + index;
                }

                args.SetValue(index, i);
            }

            try
            {
                value = items.GetValue(args);
            }
            catch (IndexOutOfRangeException)
            {
                Exceptions.SetError(Exceptions.IndexError, "array index out of range");
                return IntPtr.Zero;
            }

            return Converter.ToPython(value, itemType);
        }


        /// <summary>
        /// Implements __setitem__ for array types.
        /// </summary>
        public static new int mp_ass_subscript(IntPtr ob, IntPtr idx, IntPtr v)
        {
            var obj = (CLRObject)GetManagedObject(ob);
            var items = obj.inst as Array;
            Type itemType = obj.inst.GetType().GetElementType();
            int rank = items.Rank;
            int index;
            object value;

            if (items.IsReadOnly)
            {
                Exceptions.RaiseTypeError("array is read-only");
                return -1;
            }

            if (!Converter.ToManaged(v, itemType, out value, true))
            {
                return -1;
            }

            if (rank == 1)
            {
                if (!Runtime.PyInt_Check(idx))
                {
                    RaiseIndexMustBeIntegerError(idx);
                    return -1;
                }
                index = Runtime.PyInt_AsLong(idx);

                if (Exceptions.ErrorOccurred())
                {
                    Exceptions.RaiseTypeError("invalid index value");
                    return -1;
                }

                if (index < 0)
                {
                    index = items.Length + index;
                }

                try
                {
                    items.SetValue(value, index);
                }
                catch (IndexOutOfRangeException)
                {
                    Exceptions.SetError(Exceptions.IndexError, "array index out of range");
                    return -1;
                }

                return 0;
            }

            if (!Runtime.PyTuple_Check(idx))
            {
                Exceptions.RaiseTypeError("invalid index value");
                return -1;
            }

            var count = Runtime.PyTuple_Size(idx);
            var args = new int[count];

            for (var i = 0; i < count; i++)
            {
                IntPtr op = Runtime.PyTuple_GetItem(idx, i);
                if (!Runtime.PyInt_Check(op))
                {
                    RaiseIndexMustBeIntegerError(op);
                    return -1;
                }
                index = Runtime.PyInt_AsLong(op);

                if (Exceptions.ErrorOccurred())
                {
                    Exceptions.RaiseTypeError("invalid index value");
                    return -1;
                }

                if (index < 0)
                {
                    index = items.GetLength(i) + index;
                }

                args.SetValue(index, i);
            }

            try
            {
                items.SetValue(value, args);
            }
            catch (IndexOutOfRangeException)
            {
                Exceptions.SetError(Exceptions.IndexError, "array index out of range");
                return -1;
            }

            return 0;
        }

        private static IntPtr RaiseIndexMustBeIntegerError(IntPtr idx)
        {
            string tpName = Runtime.PyObject_GetTypeName(idx);
            return Exceptions.RaiseTypeError($"array index has type {tpName}, expected an integer");
        }

        /// <summary>
        /// Implements __contains__ for array types.
        /// </summary>
        public static int sq_contains(IntPtr ob, IntPtr v)
        {
            var obj = (CLRObject)GetManagedObject(ob);
            Type itemType = obj.inst.GetType().GetElementType();
            var items = obj.inst as IList;
            object value;

            if (!Converter.ToManaged(v, itemType, out value, false))
            {
                return 0;
            }

            if (items.Contains(value))
            {
                return 1;
            }

            return 0;
        }
    }
}
