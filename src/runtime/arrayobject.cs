using System;
using System.Collections;

namespace Python.Runtime
{
    /// <summary>
    /// Implements a Python type for managed arrays. This type is essentially
    /// the same as a ClassObject, except that it provides sequence semantics
    /// to support natural array usage (indexing) from Python.
    /// </summary>
    internal class ArrayObject : ClassBase
    {
        internal ArrayObject(Type tp) : base(tp)
        {
        }

        internal override bool CanSubclass()
        {
            return false;
        }

        public static IntPtr tp_new(IntPtr tp, IntPtr args, IntPtr kw)
        {
            var self = GetManagedObject(tp) as ArrayObject;
            if (Runtime.PyTuple_Size(args) != 1)
            {
                return Exceptions.RaiseTypeError("array expects 1 argument");
            }
            IntPtr op = Runtime.PyTuple_GetItem(args, 0);
            object result;

            if (!Converter.ToManaged(op, self.type, out result, true))
            {
                return IntPtr.Zero;
            }
            return CLRObject.GetInstHandle(result, tp);
        }


        /// <summary>
        /// Implements __getitem__ for array types.
        /// </summary>
        public static IntPtr mp_subscript(IntPtr ob, IntPtr idx)
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
        public static int mp_ass_subscript(IntPtr ob, IntPtr idx, IntPtr v)
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


        /// <summary>
        /// Implements __len__ for array types.
        /// </summary>
        public static int mp_length(IntPtr ob)
        {
            var self = (CLRObject)GetManagedObject(ob);
            var items = self.inst as Array;
            return items.Length;
        }
    }
}
