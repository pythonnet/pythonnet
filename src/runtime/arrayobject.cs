using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Python.Runtime
{
    /// <summary>
    /// Implements a Python type for managed arrays. This type is essentially
    /// the same as a ClassObject, except that it provides sequence semantics
    /// to support natural array usage (indexing) from Python.
    /// </summary>
    [Serializable]
    internal sealed class ArrayObject : ClassBase
    {
        internal ArrayObject(Type tp) : base(tp)
        {
        }

        internal override bool CanSubclass()
        {
            return false;
        }

        public static NewReference tp_new(BorrowedReference tp, BorrowedReference args, BorrowedReference kw)
        {
            if (kw != null)
            {
                return Exceptions.RaiseTypeError("array constructor takes no keyword arguments");
            }

            var self = (ArrayObject)GetManagedObject(tp)!;
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
                         shapeTuple: args,
                         pyType: tp);
            }

            BorrowedReference op = Runtime.PyTuple_GetItem(args, 0);

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
                    return NewInstance(arrType.GetElementType(), tp, dimensions);
                }
            }
            object? result;

            // this implements casting to Array[T]
            if (!Converter.ToManaged(op, arrType, out result, true))
            {
                return default;
            }
            return CLRObject.GetReference(result!, tp);
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
            for (int dim = 0; dim < dimensions.Length; dim++)
            {
                if (dimensions[dim] < 0)
                {
                    Exceptions.SetError(Exceptions.ValueError, $"Non-negative number required (dims[{dim}])");
                    return default;
                }
            }

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
            return CLRObject.GetReference(result, arrayPyType);
        }


        /// <summary>
        /// Implements __getitem__ for array types.
        /// </summary>
        public static NewReference mp_subscript(BorrowedReference ob, BorrowedReference idx)
        {
            var obj = (CLRObject)GetManagedObject(ob)!;
            var arrObj = (ArrayObject)GetManagedObject(Runtime.PyObject_TYPE(ob))!;
            if (!arrObj.type.Valid)
            {
                return Exceptions.RaiseTypeError(arrObj.type.DeletedMessage);
            }
            var items = (Array)obj.inst;
            Type itemType = arrObj.type.Value.GetElementType();
            int rank = items.Rank;
            long index;
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
                index = Runtime.PyLong_AsSignedSize_t(idx);

                if (index == -1 && Exceptions.ErrorOccurred())
                {
                    return Exceptions.RaiseTypeError("invalid index value");
                }

                if (index < 0)
                {
                    index = items.LongLength + index;
                }

                if (index < 0 || index >= items.LongLength)
                {
                    Exceptions.SetError(Exceptions.IndexError, "array index out of range");
                    return default;
                }

                value = items.GetValue(index);

                return Converter.ToPython(value, itemType);
            }

            // Multi-dimensional arrays can be indexed a la: list[1, 2, 3].

            if (!Runtime.PyTuple_Check(idx))
            {
                Exceptions.SetError(Exceptions.TypeError, "invalid index value");
                return default;
            }

            var count = Runtime.PyTuple_Size(idx);

            long[] indices = new long[count];

            for (int dimension = 0; dimension < count; dimension++)
            {
                BorrowedReference op = Runtime.PyTuple_GetItem(idx, dimension);
                if (!Runtime.PyInt_Check(op))
                {
                    return RaiseIndexMustBeIntegerError(op);
                }
                index = Runtime.PyLong_AsSignedSize_t(op);

                if (index == -1 && Exceptions.ErrorOccurred())
                {
                    return Exceptions.RaiseTypeError("invalid index value");
                }

                long len = items.GetLongLength(dimension);

                if (index < 0)
                {
                    index = len + index;
                }

                if (index < 0 || index >= len)
                {
                    Exceptions.SetError(Exceptions.IndexError, "array index out of range");
                    return default;
                }

                indices[dimension] = index;
            }

            value = items.GetValue(indices);

            return Converter.ToPython(value, itemType);
        }


        /// <summary>
        /// Implements __setitem__ for array types.
        /// </summary>
        public static int mp_ass_subscript(BorrowedReference ob, BorrowedReference idx, BorrowedReference v)
        {
            var obj = (CLRObject)GetManagedObject(ob)!;
            var items = (Array)obj.inst;
            Type itemType = obj.inst.GetType().GetElementType();
            int rank = items.Rank;
            long index;
            object? value;

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
                index = Runtime.PyLong_AsSignedSize_t(idx);

                if (index == -1 && Exceptions.ErrorOccurred())
                {
                    Exceptions.RaiseTypeError("invalid index value");
                    return -1;
                }

                if (index < 0)
                {
                    index = items.LongLength + index;
                }

                if (index < 0 || index >= items.LongLength)
                {
                    Exceptions.SetError(Exceptions.IndexError, "array index out of range");
                    return -1;
                }

                items.SetValue(value, index);
                return 0;
            }

            if (!Runtime.PyTuple_Check(idx))
            {
                Exceptions.RaiseTypeError("invalid index value");
                return -1;
            }

            var count = Runtime.PyTuple_Size(idx);
            long[] indices = new long[count];

            for (int dimension = 0; dimension < count; dimension++)
            {
                BorrowedReference op = Runtime.PyTuple_GetItem(idx, dimension);
                if (!Runtime.PyInt_Check(op))
                {
                    RaiseIndexMustBeIntegerError(op);
                    return -1;
                }
                index = Runtime.PyLong_AsSignedSize_t(op);

                if (index == -1 && Exceptions.ErrorOccurred())
                {
                    Exceptions.RaiseTypeError("invalid index value");
                    return -1;
                }

                long len = items.GetLongLength(dimension);

                if (index < 0)
                {
                    index = len + index;
                }

                if (index < 0 || index >= len)
                {
                    Exceptions.SetError(Exceptions.IndexError, "array index out of range");
                    return -1;
                }

                indices[dimension] = index;
            }

            items.SetValue(value, indices);

            return 0;
        }

        private static NewReference RaiseIndexMustBeIntegerError(BorrowedReference idx)
        {
            string tpName = Runtime.PyObject_GetTypeName(idx);
            return Exceptions.RaiseTypeError($"array index has type {tpName}, expected an integer");
        }

        /// <summary>
        /// Implements __contains__ for array types.
        /// </summary>
        public static int sq_contains(BorrowedReference ob, BorrowedReference v)
        {
            var obj = (CLRObject)GetManagedObject(ob)!;
            Type itemType = obj.inst.GetType().GetElementType();
            var items = (IList)obj.inst;
            object? value;

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

        #region Buffer protocol
        static int GetBuffer(BorrowedReference obj, out Py_buffer buffer, PyBUF flags)
        {
            buffer = default;

            if (flags == PyBUF.SIMPLE)
            {
                Exceptions.SetError(Exceptions.BufferError, "SIMPLE not implemented");
                return -1;
            }
            if ((flags & PyBUF.F_CONTIGUOUS) == PyBUF.F_CONTIGUOUS)
            {
                Exceptions.SetError(Exceptions.BufferError, "only C-contiguous supported");
                return -1;
            }
            var self = (Array)((CLRObject)GetManagedObject(obj)!).inst;
            Type itemType = self.GetType().GetElementType();

            bool formatRequested = (flags & PyBUF.FORMATS) != 0;
            string? format = GetFormat(itemType);
            if (formatRequested && format is null)
            {
                Exceptions.SetError(Exceptions.BufferError, "unsupported element type: " + itemType.Name);
                return -1;
            }
            GCHandle gcHandle;
            try
            {
                gcHandle = GCHandle.Alloc(self, GCHandleType.Pinned);
            } catch (ArgumentException ex)
            {
                Exceptions.SetError(Exceptions.BufferError, ex.Message);
                return -1;
            }

            int itemSize = Marshal.SizeOf(itemType);
            IntPtr[] shape = GetShape(self);
            IntPtr[] strides = GetStrides(shape, itemSize);
            buffer = new Py_buffer
            {
                buf = gcHandle.AddrOfPinnedObject(),
                obj = new NewReference(obj).DangerousMoveToPointer(),
                len = (IntPtr)(self.LongLength*itemSize),
                itemsize = (IntPtr)itemSize,
                _readonly = false,
                ndim = self.Rank,
                format = format,
                shape = ToUnmanaged(shape),
                strides = (flags & PyBUF.STRIDES) == PyBUF.STRIDES ? ToUnmanaged(strides) : IntPtr.Zero,
                suboffsets = IntPtr.Zero,
                _internal = (IntPtr)gcHandle,
            };

            return 0;
        }
        static void ReleaseBuffer(BorrowedReference obj, ref Py_buffer buffer)
        {
            if (buffer._internal == IntPtr.Zero) return;

            UnmanagedFree(ref buffer.shape);
            UnmanagedFree(ref buffer.strides);
            UnmanagedFree(ref buffer.suboffsets);

            // TODO: decref buffer.obj?

            var gcHandle = (GCHandle)buffer._internal;
            gcHandle.Free();
            buffer._internal = IntPtr.Zero;
        }

        static IntPtr[] GetStrides(IntPtr[] shape, long itemSize)
        {
            var result = new IntPtr[shape.Length];
            result[shape.Length - 1] = new IntPtr(itemSize);
            for (int dim = shape.Length - 2; dim >= 0; dim--)
            {
                itemSize *= shape[dim + 1].ToInt64();
                result[dim] = new IntPtr(itemSize);
            }
            return result;
        }
        static IntPtr[] GetShape(Array array)
        {
            var result = new IntPtr[array.Rank];
            for (int i = 0; i < result.Length; i++)
                result[i] = (IntPtr)array.GetLongLength(i);
            return result;
        }

        static void UnmanagedFree(ref IntPtr address)
        {
            if (address == IntPtr.Zero) return;

            Marshal.FreeHGlobal(address);
            address = IntPtr.Zero;
        }
        static unsafe IntPtr ToUnmanaged<T>(T[] array) where T : unmanaged
        {
            IntPtr result = Marshal.AllocHGlobal(checked(Marshal.SizeOf(typeof(T)) * array.Length));
            fixed (T* ptr = array)
            {
                var @out = (T*)result;
                for (int i = 0; i < array.Length; i++)
                    @out[i] = ptr[i];
            }
            return result;
        }

        static readonly Dictionary<Type, string> ItemFormats = new Dictionary<Type, string>
        {
            [typeof(byte)] = "B",
            [typeof(sbyte)] = "b",

            [typeof(bool)] = "?",

            [typeof(short)] = "h",
            [typeof(ushort)] = "H",
            // see https://github.com/pybind/pybind11/issues/1908#issuecomment-658358767
            [typeof(int)] = "i",
            [typeof(uint)] = "I",
            [typeof(long)] = "q",
            [typeof(ulong)] = "Q",

            [typeof(IntPtr)] = "n",
            [typeof(UIntPtr)] = "N",

            // TODO: half = "e"
            [typeof(float)] = "f",
            [typeof(double)] = "d",
        };

        static string? GetFormat(Type elementType)
            => ItemFormats.TryGetValue(elementType, out string result) ? result : null;

        static readonly GetBufferProc getBufferProc = GetBuffer;
        static readonly ReleaseBufferProc releaseBufferProc = ReleaseBuffer;
        static readonly IntPtr BufferProcsAddress = AllocateBufferProcs();
        static IntPtr AllocateBufferProcs()
        {
            var procs = new PyBufferProcs
            {
                Get = Marshal.GetFunctionPointerForDelegate(getBufferProc),
                Release = Marshal.GetFunctionPointerForDelegate(releaseBufferProc),
            };
            IntPtr result = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(PyBufferProcs)));
            Marshal.StructureToPtr(procs, result, fDeleteOld: false);
            return result;
        }
        #endregion

        /// <summary>
        /// <see cref="TypeManager.InitializeSlots(IntPtr, Type, SlotsHolder)"/>
        /// </summary>
        public static void InitializeSlots(PyType type, ISet<string> initialized, SlotsHolder slotsHolder)
        {
            if (initialized.Add(nameof(TypeOffset.tp_as_buffer)))
            {
                // TODO: only for unmanaged arrays
                int offset = TypeOffset.GetSlotOffset(nameof(TypeOffset.tp_as_buffer));
                Util.WriteIntPtr(type, offset, BufferProcsAddress);
            }
        }
    }
}
