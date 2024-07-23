using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.Serialization;
using System.Threading;

namespace Python.Runtime
{
    /// <summary>
    /// Represents a generic Python object. The methods of this class are
    /// generally equivalent to the Python "abstract object API". See
    /// PY2: https://docs.python.org/2/c-api/object.html
    /// PY3: https://docs.python.org/3/c-api/object.html
    /// for details.
    /// </summary>
    [Serializable]
    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
    public partial class PyObject : DynamicObject, IDisposable, ISerializable
    {
#if TRACE_ALLOC
        /// <summary>
        /// Trace stack for PyObject's construction
        /// </summary>
        public StackTrace Traceback { get; } = new StackTrace(1);
#endif  

        protected IntPtr rawPtr = IntPtr.Zero;
        internal readonly int run = Runtime.GetRun();

        internal BorrowedReference obj => new (rawPtr);

        public static PyObject None => new (Runtime.PyNone);
        internal BorrowedReference Reference => new (rawPtr);

        /// <summary>
        /// PyObject Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new PyObject from an IntPtr object reference. Note that
        /// the PyObject instance assumes ownership of the object reference
        /// and the reference will be DECREFed when the PyObject is garbage
        /// collected or explicitly disposed.
        /// </remarks>
        [Obsolete]
        internal PyObject(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) throw new ArgumentNullException(nameof(ptr));

            rawPtr = ptr;
            Finalizer.Instance.ThrottledCollect();
        }

        [Obsolete("for testing purposes only")]
        internal PyObject(IntPtr ptr, bool skipCollect)
        {
            if (ptr == IntPtr.Zero) throw new ArgumentNullException(nameof(ptr));

            rawPtr = ptr;
            if (!skipCollect)
                Finalizer.Instance.ThrottledCollect();
        }

        /// <summary>
        /// Creates new <see cref="PyObject"/> pointing to the same object as
        /// the <paramref name="reference"/>. Increments refcount, allowing <see cref="PyObject"/>
        /// to have ownership over its own reference.
        /// </summary>
        internal PyObject(BorrowedReference reference)
        {
            if (reference.IsNull) throw new ArgumentNullException(nameof(reference));

            rawPtr = new NewReference(reference).DangerousMoveToPointer();
            Finalizer.Instance.ThrottledCollect();
        }

        internal PyObject(BorrowedReference reference, bool skipCollect)
        {
            if (reference.IsNull) throw new ArgumentNullException(nameof(reference));

            rawPtr = new NewReference(reference).DangerousMoveToPointer();
            if (!skipCollect)
                Finalizer.Instance.ThrottledCollect();
        }

        internal PyObject(in StolenReference reference)
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));

            rawPtr = reference.DangerousGetAddressOrNull();
            Finalizer.Instance.ThrottledCollect();
        }

        // Ensure that encapsulated Python object is decref'ed appropriately
        // when the managed wrapper is garbage-collected.
        ~PyObject()
        {
            if (!IsDisposed)
            {

#if TRACE_ALLOC
                CheckRun();
#endif

                Interlocked.Increment(ref Runtime._collected);

                Finalizer.Instance.AddFinalizedObject(ref rawPtr, run
#if TRACE_ALLOC
                    , Traceback
#endif
                );
            }

            Dispose(false);
        }


        /// <summary>
        /// Gets the native handle of the underlying Python object. This
        /// value is generally for internal use by the PythonNet runtime.
        /// </summary>
        [Obsolete]
        public IntPtr Handle
        {
            get { return rawPtr; }
        }


        /// <summary>
        /// Gets raw Python proxy for this object (bypasses all conversions,
        /// except <c>null</c> &lt;==&gt; <c>None</c>)
        /// </summary>
        /// <remarks>
        /// Given an arbitrary managed object, return a Python instance that
        /// reflects the managed object.
        /// </remarks>
        public static PyObject FromManagedObject(object ob)
        {
            // Special case: if ob is null, we return None.
            if (ob == null)
            {
                return new PyObject(Runtime.PyNone);
            }
            return CLRObject.GetReference(ob).MoveToPyObject();
        }

        /// <summary>
        /// Creates new <see cref="PyObject"/> from a nullable reference.
        /// When <paramref name="reference"/> is <c>null</c>, <c>null</c> is returned.
        /// </summary>
        internal static PyObject? FromNullableReference(BorrowedReference reference)
            => reference.IsNull ? null : new PyObject(reference);


        /// <summary>
        /// AsManagedObject Method
        /// </summary>
        /// <remarks>
        /// Return a managed object of the given type, based on the
        /// value of the Python object.
        /// </remarks>
        public object? AsManagedObject(Type t)
        {
            if (!Converter.ToManaged(obj, t, out var result, true))
            {
                throw new InvalidCastException("cannot convert object to target type",
                    PythonException.FetchCurrentOrNull(out _));
            }
            return result;
        }

        /// <summary>
        /// Return a managed object of the given type, based on the
        /// value of the Python object.
        /// </summary>
        public T As<T>() => (T)this.AsManagedObject(typeof(T))!;

        internal bool IsDisposed => rawPtr == IntPtr.Zero;

        void CheckDisposed()
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(PyObject));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (IsDisposed)
            {
                return;
            }

            if (Runtime.Py_IsInitialized() == 0 && Runtime._Py_IsFinalizing() != true)
            {
                throw new InvalidOperationException("Python runtime must be initialized");
            }

            nint refcount = Runtime.Refcount(this.obj);
            Debug.Assert(refcount > 0, "Object refcount is 0 or less");

            if (refcount == 1)
            {
                Runtime.PyErr_Fetch(out var errType, out var errVal, out var traceback);

                try
                {
                    Runtime.XDecref(StolenReference.Take(ref rawPtr));
                    Runtime.CheckExceptionOccurred();
                }
                finally
                {
                    // Python requires finalizers to preserve exception:
                    // https://docs.python.org/3/extending/newtypes.html#finalization-and-de-allocation
                    Runtime.PyErr_Restore(errType.StealNullable(), errVal.StealNullable(), traceback.StealNullable());
                }
            }
            else
            {
                Runtime.XDecref(StolenReference.Take(ref rawPtr));
            }

            this.rawPtr = IntPtr.Zero;
        }

        /// <summary>
        /// The Dispose method provides a way to explicitly release the
        /// Python object represented by a PyObject instance. It is a good
        /// idea to call Dispose on PyObjects that wrap resources that are
        /// limited or need strict lifetime control. Otherwise, references
        /// to Python objects will not be released until a managed garbage
        /// collection occurs.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
            
        }

        internal StolenReference Steal()
        {
            GC.SuppressFinalize(this);
            return StolenReference.Take(ref this.rawPtr);
        }

        [Obsolete("Test use only")]
        internal void Leak()
        {
            Debug.Assert(!IsDisposed);
            GC.SuppressFinalize(this);
            rawPtr = IntPtr.Zero;
        }

        internal IntPtr DangerousGetAddressOrNull() => rawPtr;

        internal void CheckRun()
        {
            if (run != Runtime.GetRun())
                throw new RuntimeShutdownException(rawPtr);
        }

        internal BorrowedReference GetPythonTypeReference()
            => Runtime.PyObject_TYPE(obj);

        /// <summary>
        /// GetPythonType Method
        /// </summary>
        /// <remarks>
        /// Returns the Python type of the object. This method is equivalent
        /// to the Python expression: type(object).
        /// </remarks>
        public PyType GetPythonType()
        {
            var tp = Runtime.PyObject_Type(Reference);
            return new PyType(tp.StealOrThrow(), prevalidated: true);
        }


        /// <summary>
        /// TypeCheck Method
        /// </summary>
        /// <remarks>
        /// Returns true if the object o is of type typeOrClass or a subtype
        /// of typeOrClass.
        /// </remarks>
        public bool TypeCheck(PyType typeOrClass)
        {
            if (typeOrClass == null) throw new ArgumentNullException(nameof(typeOrClass));

            return Runtime.PyObject_TypeCheck(obj, typeOrClass.obj);
        }

        internal PyType PyType => this.GetPythonType();


        /// <summary>
        /// HasAttr Method
        /// </summary>
        /// <remarks>
        /// Returns true if the object has an attribute with the given name.
        /// </remarks>
        public bool HasAttr(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            return Runtime.PyObject_HasAttrString(Reference, name) != 0;
        }


        /// <summary>
        /// HasAttr Method
        /// </summary>
        /// <remarks>
        /// Returns true if the object has an attribute with the given name,
        /// where name is a PyObject wrapping a string or unicode object.
        /// </remarks>
        public bool HasAttr(PyObject name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            return Runtime.PyObject_HasAttr(Reference, name.Reference) != 0;
        }


        /// <summary>
        /// GetAttr Method
        /// </summary>
        /// <remarks>
        /// Returns the named attribute of the Python object, or raises a
        /// PythonException if the attribute access fails.
        /// </remarks>
        public PyObject GetAttr(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            using var op = Runtime.PyObject_GetAttrString(obj, name);
            return new PyObject(op.StealOrThrow());
        }


        /// <summary>
        /// Returns the named attribute of the Python object, or the given
        /// default object if the attribute access throws AttributeError.
        /// </summary>
        /// <remarks>
        /// This method ignores any AttrubiteError(s), even ones
        /// not raised due to missing requested attribute.
        ///
        /// For example, if attribute getter calls other Python code, and
        /// that code happens to cause AttributeError elsewhere, it will be ignored
        /// and <paramref name="_default"/> value will be returned instead.
        /// </remarks>
        /// <param name="name">Name of the attribute.</param>
        /// <param name="_default">The object to return on AttributeError.</param>
        [Obsolete("See remarks")]
        public PyObject GetAttr(string name, PyObject _default)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            using var op = Runtime.PyObject_GetAttrString(obj, name);
            if (op.IsNull())
            {
                if (Exceptions.ExceptionMatches(Exceptions.AttributeError))
                {
                    Runtime.PyErr_Clear();
                    return _default;
                }
                else
                {
                    throw PythonException.ThrowLastAsClrException();
                }
            }
            return new PyObject(op.Steal());
        }


        /// <summary>
        /// GetAttr Method
        /// </summary>
        /// <remarks>
        /// Returns the named attribute of the Python object or raises a
        /// PythonException if the attribute access fails. The name argument
        /// is a PyObject wrapping a Python string or unicode object.
        /// </remarks>
        public PyObject GetAttr(PyObject name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            using var op = Runtime.PyObject_GetAttr(obj, name.obj);
            return new PyObject(op.StealOrThrow());
        }


        /// <summary>
        /// Returns the named attribute of the Python object, or the given
        /// default object if the attribute access throws AttributeError.
        /// </summary>
        /// <remarks>
        /// This method ignores any AttrubiteError(s), even ones
        /// not raised due to missing requested attribute.
        ///
        /// For example, if attribute getter calls other Python code, and
        /// that code happens to cause AttributeError elsewhere, it will be ignored
        /// and <paramref name="_default"/> value will be returned instead.
        /// </remarks>
        /// <param name="name">Name of the attribute. Must be of Python type 'str'.</param>
        /// <param name="_default">The object to return on AttributeError.</param>
        [Obsolete("See remarks")]
        public PyObject GetAttr(PyObject name, PyObject _default)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            using var op = Runtime.PyObject_GetAttr(obj, name.obj);
            if (op.IsNull())
            {
                if (Exceptions.ExceptionMatches(Exceptions.AttributeError))
                {
                    Runtime.PyErr_Clear();
                    return _default;
                }
                else
                {
                    throw PythonException.ThrowLastAsClrException();
                }
            }
            return new PyObject(op.Steal());
        }


        /// <summary>
        /// SetAttr Method
        /// </summary>
        /// <remarks>
        /// Set an attribute of the object with the given name and value. This
        /// method throws a PythonException if the attribute set fails.
        /// </remarks>
        public void SetAttr(string name, PyObject value)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (value == null) throw new ArgumentNullException(nameof(value));

            int r = Runtime.PyObject_SetAttrString(obj, name, value.obj);
            if (r < 0)
            {
                throw PythonException.ThrowLastAsClrException();
            }
        }


        /// <summary>
        /// SetAttr Method
        /// </summary>
        /// <remarks>
        /// Set an attribute of the object with the given name and value,
        /// where the name is a Python string or unicode object. This method
        /// throws a PythonException if the attribute set fails.
        /// </remarks>
        public void SetAttr(PyObject name, PyObject value)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (value == null) throw new ArgumentNullException(nameof(value));

            int r = Runtime.PyObject_SetAttr(obj, name.obj, value.obj);
            if (r < 0)
            {
                throw PythonException.ThrowLastAsClrException();
            }
        }


        /// <summary>
        /// DelAttr Method
        /// </summary>
        /// <remarks>
        /// Delete the named attribute of the Python object. This method
        /// throws a PythonException if the attribute set fails.
        /// </remarks>
        public void DelAttr(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            int r = Runtime.PyObject_DelAttrString(obj, name);
            if (r < 0)
            {
                throw PythonException.ThrowLastAsClrException();
            }
        }


        /// <summary>
        /// DelAttr Method
        /// </summary>
        /// <remarks>
        /// Delete the named attribute of the Python object, where name is a
        /// PyObject wrapping a Python string or unicode object. This method
        /// throws a PythonException if the attribute set fails.
        /// </remarks>
        public void DelAttr(PyObject name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            int r = Runtime.PyObject_DelAttr(obj, name.obj);
            if (r < 0)
            {
                throw PythonException.ThrowLastAsClrException();
            }
        }


        /// <summary>
        /// GetItem Method
        /// </summary>
        /// <remarks>
        /// For objects that support the Python sequence or mapping protocols,
        /// return the item at the given object index. This method raises a
        /// PythonException if the indexing operation fails.
        /// </remarks>
        public virtual PyObject GetItem(PyObject key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            using var op = Runtime.PyObject_GetItem(obj, key.obj);
            if (op.IsNull())
            {
                throw PythonException.ThrowLastAsClrException();
            }
            return new PyObject(op.Steal());
        }


        /// <summary>
        /// GetItem Method
        /// </summary>
        /// <remarks>
        /// For objects that support the Python sequence or mapping protocols,
        /// return the item at the given string index. This method raises a
        /// PythonException if the indexing operation fails.
        /// </remarks>
        public virtual PyObject GetItem(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            using var pyKey = new PyString(key);
            return GetItem(pyKey);
        }


        /// <summary>
        /// GetItem Method
        /// </summary>
        /// <remarks>
        /// For objects that support the Python sequence or mapping protocols,
        /// return the item at the given numeric index. This method raises a
        /// PythonException if the indexing operation fails.
        /// </remarks>
        public virtual PyObject GetItem(int index)
        {
            using var key = new PyInt(index);
            return GetItem(key);
        }


        /// <summary>
        /// SetItem Method
        /// </summary>
        /// <remarks>
        /// For objects that support the Python sequence or mapping protocols,
        /// set the item at the given object index to the given value. This
        /// method raises a PythonException if the set operation fails.
        /// </remarks>
        public virtual void SetItem(PyObject key, PyObject value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (value == null) throw new ArgumentNullException(nameof(value));

            int r = Runtime.PyObject_SetItem(obj, key.obj, value.obj);
            if (r < 0)
            {
                throw PythonException.ThrowLastAsClrException();
            }
        }


        /// <summary>
        /// SetItem Method
        /// </summary>
        /// <remarks>
        /// For objects that support the Python sequence or mapping protocols,
        /// set the item at the given string index to the given value. This
        /// method raises a PythonException if the set operation fails.
        /// </remarks>
        public virtual void SetItem(string key, PyObject value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (value == null) throw new ArgumentNullException(nameof(value));

            using var pyKey = new PyString(key);
            SetItem(pyKey, value);
        }


        /// <summary>
        /// SetItem Method
        /// </summary>
        /// <remarks>
        /// For objects that support the Python sequence or mapping protocols,
        /// set the item at the given numeric index to the given value. This
        /// method raises a PythonException if the set operation fails.
        /// </remarks>
        public virtual void SetItem(int index, PyObject value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            using var pyindex = new PyInt(index);
            SetItem(pyindex, value);
        }


        /// <summary>
        /// DelItem Method
        /// </summary>
        /// <remarks>
        /// For objects that support the Python sequence or mapping protocols,
        /// delete the item at the given object index. This method raises a
        /// PythonException if the delete operation fails.
        /// </remarks>
        public virtual void DelItem(PyObject key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            int r = Runtime.PyObject_DelItem(obj, key.obj);
            if (r < 0)
            {
                throw PythonException.ThrowLastAsClrException();
            }
        }


        /// <summary>
        /// DelItem Method
        /// </summary>
        /// <remarks>
        /// For objects that support the Python sequence or mapping protocols,
        /// delete the item at the given string index. This method raises a
        /// PythonException if the delete operation fails.
        /// </remarks>
        public virtual void DelItem(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            using var pyKey = new PyString(key);
            DelItem(pyKey);
        }


        /// <summary>
        /// DelItem Method
        /// </summary>
        /// <remarks>
        /// For objects that support the Python sequence or mapping protocols,
        /// delete the item at the given numeric index. This method raises a
        /// PythonException if the delete operation fails.
        /// </remarks>
        public virtual void DelItem(int index)
        {
            using var pyindex = new PyInt(index);
            DelItem(pyindex);
        }


        /// <summary>
        /// Returns the length for objects that support the Python sequence
        /// protocol.
        /// </summary>
        public virtual long Length()
        {
            var s = Runtime.PyObject_Size(Reference);
            if (s < 0)
            {
                throw PythonException.ThrowLastAsClrException();
            }
            return s;
        }


        /// <summary>
        /// String Indexer
        /// </summary>
        /// <remarks>
        /// Provides a shorthand for the string versions of the GetItem and
        /// SetItem methods.
        /// </remarks>
        public virtual PyObject this[string key]
        {
            get { return GetItem(key); }
            set { SetItem(key, value); }
        }


        /// <summary>
        /// PyObject Indexer
        /// </summary>
        /// <remarks>
        /// Provides a shorthand for the object versions of the GetItem and
        /// SetItem methods.
        /// </remarks>
        public virtual PyObject this[PyObject key]
        {
            get { return GetItem(key); }
            set { SetItem(key, value); }
        }


        /// <summary>
        /// Numeric Indexer
        /// </summary>
        /// <remarks>
        /// Provides a shorthand for the numeric versions of the GetItem and
        /// SetItem methods.
        /// </remarks>
        public virtual PyObject this[int index]
        {
            get { return GetItem(index); }
            set { SetItem(index, value); }
        }


        /// <summary>
        /// Return a new (Python) iterator for the object. This is equivalent
        /// to the Python expression "iter(object)".
        /// </summary>
        /// <exception cref="PythonException">Thrown if the object can not be iterated.</exception>
        public PyIter GetIterator() => PyIter.GetIter(this);

        /// <summary>
        /// Invoke Method
        /// </summary>
        /// <remarks>
        /// Invoke the callable object with the given arguments, passed as a
        /// PyObject[]. A PythonException is raised if the invocation fails.
        /// </remarks>
        public PyObject Invoke(params PyObject[] args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            if (args.Contains(null)) throw new ArgumentNullException();

            var t = new PyTuple(args);
            using var r = Runtime.PyObject_Call(obj, t.obj, null);
            t.Dispose();
            return new PyObject(r.StealOrThrow());
        }


        /// <summary>
        /// Invoke Method
        /// </summary>
        /// <remarks>
        /// Invoke the callable object with the given arguments, passed as a
        /// Python tuple. A PythonException is raised if the invocation fails.
        /// </remarks>
        public PyObject Invoke(PyTuple args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));

            using var r = Runtime.PyObject_Call(obj, args.obj, null);
            return new PyObject(r.StealOrThrow());
        }


        /// <summary>
        /// Invoke Method
        /// </summary>
        /// <remarks>
        /// Invoke the callable object with the given positional and keyword
        /// arguments. A PythonException is raised if the invocation fails.
        /// </remarks>
        public PyObject Invoke(PyObject[] args, PyDict? kw)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            if (args.Contains(null)) throw new ArgumentNullException();

            using var t = new PyTuple(args);
            using var r = Runtime.PyObject_Call(obj, t.obj, kw is null ? null : kw.obj);
            return new PyObject(r.StealOrThrow());
        }


        /// <summary>
        /// Invoke Method
        /// </summary>
        /// <remarks>
        /// Invoke the callable object with the given positional and keyword
        /// arguments. A PythonException is raised if the invocation fails.
        /// </remarks>
        public PyObject Invoke(PyTuple args, PyDict? kw)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));

            using var r = Runtime.PyObject_Call(obj, args.obj, kw is null ? null : kw.obj);
            return new PyObject(r.StealOrThrow());
        }


        /// <summary>
        /// InvokeMethod Method
        /// </summary>
        /// <remarks>
        /// Invoke the named method of the object with the given arguments.
        /// A PythonException is raised if the invocation is unsuccessful.
        /// </remarks>
        public PyObject InvokeMethod(string name, params PyObject[] args)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (args == null) throw new ArgumentNullException(nameof(args));
            if (args.Contains(null)) throw new ArgumentNullException();

            PyObject method = GetAttr(name);
            PyObject result = method.Invoke(args);
            method.Dispose();
            return result;
        }


        /// <summary>
        /// InvokeMethod Method
        /// </summary>
        /// <remarks>
        /// Invoke the named method of the object with the given arguments.
        /// A PythonException is raised if the invocation is unsuccessful.
        /// </remarks>
        public PyObject InvokeMethod(string name, PyTuple args)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (args == null) throw new ArgumentNullException(nameof(args));

            PyObject method = GetAttr(name);
            PyObject result = method.Invoke(args);
            method.Dispose();
            return result;
        }

        /// <summary>
        /// InvokeMethod Method
        /// </summary>
        /// <remarks>
        /// Invoke the named method of the object with the given arguments.
        /// A PythonException is raised if the invocation is unsuccessful.
        /// </remarks>
        public PyObject InvokeMethod(PyObject name, params PyObject[] args)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (args == null) throw new ArgumentNullException(nameof(args));
            if (args.Contains(null)) throw new ArgumentNullException();

            PyObject method = GetAttr(name);
            PyObject result = method.Invoke(args);
            method.Dispose();
            return result;
        }


        /// <summary>
        /// InvokeMethod Method
        /// </summary>
        /// <remarks>
        /// Invoke the named method of the object with the given arguments.
        /// A PythonException is raised if the invocation is unsuccessful.
        /// </remarks>
        public PyObject InvokeMethod(PyObject name, PyTuple args)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (args == null) throw new ArgumentNullException(nameof(args));

            PyObject method = GetAttr(name);
            PyObject result = method.Invoke(args);
            method.Dispose();
            return result;
        }


        /// <summary>
        /// InvokeMethod Method
        /// </summary>
        /// <remarks>
        /// Invoke the named method of the object with the given arguments
        /// and keyword arguments. Keyword args are passed as a PyDict object.
        /// A PythonException is raised if the invocation is unsuccessful.
        /// </remarks>
        public PyObject InvokeMethod(string name, PyObject[] args, PyDict? kw)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (args == null) throw new ArgumentNullException(nameof(args));
            if (args.Contains(null)) throw new ArgumentNullException();

            PyObject method = GetAttr(name);
            PyObject result = method.Invoke(args, kw);
            method.Dispose();
            return result;
        }


        /// <summary>
        /// InvokeMethod Method
        /// </summary>
        /// <remarks>
        /// Invoke the named method of the object with the given arguments
        /// and keyword arguments. Keyword args are passed as a PyDict object.
        /// A PythonException is raised if the invocation is unsuccessful.
        /// </remarks>
        public PyObject InvokeMethod(string name, PyTuple args, PyDict? kw)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (args == null) throw new ArgumentNullException(nameof(args));

            PyObject method = GetAttr(name);
            PyObject result = method.Invoke(args, kw);
            method.Dispose();
            return result;
        }


        /// <summary>
        /// IsInstance Method
        /// </summary>
        /// <remarks>
        /// Return true if the object is an instance of the given Python type
        /// or class. This method always succeeds.
        /// </remarks>
        public bool IsInstance(PyObject typeOrClass)
        {
            if (typeOrClass == null) throw new ArgumentNullException(nameof(typeOrClass));

            int r = Runtime.PyObject_IsInstance(obj, typeOrClass.obj);
            if (r < 0)
            {
                Runtime.PyErr_Clear();
                return false;
            }
            return r != 0;
        }


        /// <summary>
        /// Return <c>true</c> if the object is identical to or derived from the
        /// given Python type or class. This method always succeeds.
        /// </summary>
        public bool IsSubclass(PyObject typeOrClass)
        {
            if (typeOrClass == null) throw new ArgumentNullException(nameof(typeOrClass));

            return IsSubclass(typeOrClass.Reference);
        }

        internal bool IsSubclass(BorrowedReference typeOrClass)
        {
            if (typeOrClass.IsNull) throw new ArgumentNullException(nameof(typeOrClass));

            int r = Runtime.PyObject_IsSubclass(Reference, typeOrClass);
            if (r < 0)
            {
                Runtime.PyErr_Clear();
                return false;
            }
            return r != 0;
        }


        /// <summary>
        /// IsCallable Method
        /// </summary>
        /// <remarks>
        /// Returns true if the object is a callable object. This method
        /// always succeeds.
        /// </remarks>
        public bool IsCallable()
        {
            return Runtime.PyCallable_Check(obj) != 0;
        }


        /// <summary>
        /// IsIterable Method
        /// </summary>
        /// <remarks>
        /// Returns true if the object is iterable object. This method
        /// always succeeds.
        /// </remarks>
        public bool IsIterable()
        {
            return Runtime.PyObject_IsIterable(obj);
        }


        /// <summary>
        /// IsTrue Method
        /// </summary>
        /// <remarks>
        /// Return true if the object is true according to Python semantics.
        /// This method always succeeds.
        /// </remarks>
        public bool IsTrue()
        {
            return Runtime.PyObject_IsTrue(obj) != 0;
        }

        /// <summary>
        /// Return true if the object is None
        /// </summary>
        public bool IsNone() => CheckNone(this) == null;

        /// <summary>
        /// Dir Method
        /// </summary>
        /// <remarks>
        /// Return a list of the names of the attributes of the object. This
        /// is equivalent to the Python expression "dir(object)".
        /// </remarks>
        public PyList Dir()
        {
            using var r = Runtime.PyObject_Dir(obj);
            return new PyList(r.StealOrThrow());
        }


        /// <summary>
        /// Repr Method
        /// </summary>
        /// <remarks>
        /// Return a string representation of the object. This method is
        /// the managed equivalent of the Python expression "repr(object)".
        /// </remarks>
        public string? Repr()
        {
            using var strval = Runtime.PyObject_Repr(obj);
            return Runtime.GetManagedString(strval.BorrowOrThrow());
        }


        /// <summary>
        /// ToString Method
        /// </summary>
        /// <remarks>
        /// Return the string representation of the object. This method is
        /// the managed equivalent of the Python expression "str(object)".
        /// </remarks>
        public override string? ToString()
        {
            using var _ = Py.GIL();
            using var strval = Runtime.PyObject_Str(obj);
            return Runtime.GetManagedString(strval.BorrowOrThrow());
        }

        ManagedType? InternalManagedObject => ManagedType.GetManagedObject(this.Reference);

        string? DebuggerDisplay
        {
            get
            {
                if (DebugUtil.HaveInterpreterLock())
                    return this.ToString();
                var obj = this.InternalManagedObject;
                return obj is { }
                    ? obj.ToString()
                    : $"pyobj at 0x{this.rawPtr:X} (get Py.GIL to see more info)";
            }
        }


        /// <summary>
        /// Equals Method
        /// </summary>
        /// <remarks>
        /// Return true if this object is equal to the given object. This
        /// method is based on Python equality semantics.
        /// </remarks>
        public override bool Equals(object o)
        {
            using var _ = Py.GIL();
            return Equals(o as PyObject);
        }

        public virtual bool Equals(PyObject? other)
        {
            if (other is null) return false;

            if (obj == other.obj)
            {
                return true;
            }
            int result = Runtime.PyObject_RichCompareBool(obj, other.obj, Runtime.Py_EQ);
            if (result < 0) throw PythonException.ThrowLastAsClrException();
            return result != 0;
        }


        /// <summary>
        /// GetHashCode Method
        /// </summary>
        /// <remarks>
        /// Return a hashcode based on the Python object. This returns the
        /// hash as computed by Python, equivalent to the Python expression
        /// "hash(obj)".
        /// </remarks>
        public override int GetHashCode()
        {
            using var _ = Py.GIL();
            nint pyHash = Runtime.PyObject_Hash(obj);
            if (pyHash == -1 && Exceptions.ErrorOccurred())
            {
                throw PythonException.ThrowLastAsClrException();
            }
            return pyHash.GetHashCode();
        }

        /// <summary>
        /// GetBuffer Method. This Method only works for objects that have a buffer (like "bytes", "bytearray" or "array.array")
        /// </summary>
        /// <remarks>
        /// Send a request to the PyObject to fill in view as specified by flags. If the PyObject cannot provide a buffer of the exact type, it MUST raise PyExc_BufferError, set view->obj to NULL and return -1.
        /// On success, fill in view, set view->obj to a new reference to exporter and return 0. In the case of chained buffer providers that redirect requests to a single object, view->obj MAY refer to this object instead of exporter(See Buffer Object Structures).
        /// Successful calls to <see cref="PyObject.GetBuffer"/> must be paired with calls to <see cref="PyBuffer.Dispose()"/>, similar to malloc() and free(). Thus, after the consumer is done with the buffer, <see cref="PyBuffer.Dispose()"/> must be called exactly once.
        /// </remarks>
        public PyBuffer GetBuffer(PyBUF flags = PyBUF.SIMPLE)
        {
            CheckDisposed();
            return new PyBuffer(this, flags);
        }


        public long Refcount
        {
            get
            {
                return Runtime.Refcount(obj);
            }
        }

        internal int CompareTo(BorrowedReference other)
        {
            int greater = Runtime.PyObject_RichCompareBool(this.Reference, other, Runtime.Py_GT);
            Debug.Assert(greater != -1);
            if (greater > 0)
                return 1;
            int less = Runtime.PyObject_RichCompareBool(this.Reference, other, Runtime.Py_LT);
            Debug.Assert(less != -1);
            return less > 0 ? -1 : 0;
        }

        internal bool Equals(BorrowedReference other)
        {
            int equal = Runtime.PyObject_RichCompareBool(this.Reference, other, Runtime.Py_EQ);
            Debug.Assert(equal != -1);
            return equal > 0;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            using var _ = Py.GIL();
            result = CheckNone(this.GetAttr(binder.Name));
            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, object? value)
        {
            using var _ = Py.GIL();
            using var newVal = Converter.ToPythonDetectType(value);
            int r = Runtime.PyObject_SetAttrString(obj, binder.Name, newVal.Borrow());
            if (r < 0)
            {
                throw PythonException.ThrowLastAsClrException();
            }
            return true;
        }

        private void GetArgs(object?[] inargs, CallInfo callInfo, out PyTuple args, out PyDict? kwargs)
        {
            if (callInfo == null || callInfo.ArgumentNames.Count == 0)
            {
                GetArgs(inargs, out args, out kwargs);
                return;
            }

            // Support for .net named arguments
            var namedArgumentCount = callInfo.ArgumentNames.Count;
            var regularArgumentCount = callInfo.ArgumentCount - namedArgumentCount;

            using var argTuple = Runtime.PyTuple_New(regularArgumentCount);
            for (int i = 0; i < regularArgumentCount; ++i)
            {
                AddArgument(argTuple.Borrow(), i, inargs[i]);
            }
            args = new PyTuple(argTuple.Steal());

            var namedArgs = new object?[namedArgumentCount * 2];
            for (int i = 0; i < namedArgumentCount; ++i)
            {
                namedArgs[i * 2] = callInfo.ArgumentNames[i];
                namedArgs[i * 2 + 1] = inargs[regularArgumentCount + i];
            }
            kwargs = Py.kw(namedArgs);
        }

        private void GetArgs(object?[] inargs, out PyTuple args, out PyDict? kwargs)
        {
            int arg_count;
            for (arg_count = 0; arg_count < inargs.Length && !(inargs[arg_count] is Py.KeywordArguments); ++arg_count)
            {
                ;
            }
            using var argtuple = Runtime.PyTuple_New(arg_count);
            for (var i = 0; i < arg_count; i++)
            {
                AddArgument(argtuple.Borrow(), i, inargs[i]);
            }
            args = new PyTuple(argtuple.Steal());

            kwargs = null;
            for (int i = arg_count; i < inargs.Length; i++)
            {
                if (inargs[i] is not Py.KeywordArguments kw)
                {
                    throw new ArgumentException("Keyword arguments must come after normal arguments.");
                }
                if (kwargs == null)
                {
                    kwargs = kw;
                }
                else
                {
                    kwargs.Update(kw);
                }
            }
        }

        private static void AddArgument(BorrowedReference argtuple, nint i, object? target)
        {
            using var ptr = GetPythonObject(target);

            if (Runtime.PyTuple_SetItem(argtuple, i, ptr.StealNullable()) < 0)
            {
                throw PythonException.ThrowLastAsClrException();
            }
        }

        private static NewReference GetPythonObject(object? target)
        {
            if (target is PyObject pyObject)
            {
                return new NewReference(pyObject);
            }
            else
            {
                return Converter.ToPythonDetectType(target);
            }
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object?[] args, out object? result)
        {
            using var _ = Py.GIL();
            if (this.HasAttr(binder.Name) && this.GetAttr(binder.Name).IsCallable())
            {
                PyTuple? pyargs = null;
                PyDict? kwargs = null;
                try
                {
                    GetArgs(args, binder.CallInfo, out pyargs, out kwargs);
                    result = CheckNone(InvokeMethod(binder.Name, pyargs, kwargs));
                }
                finally
                {
                    pyargs?.Dispose();
                    kwargs?.Dispose();
                }
                return true;
            }
            else
            {
                return base.TryInvokeMember(binder, args, out result);
            }
        }

        public override bool TryInvoke(InvokeBinder binder, object?[] args, out object? result)
        {
            using var _ = Py.GIL();
            if (this.IsCallable())
            {
                PyTuple? pyargs = null;
                PyDict? kwargs = null;
                try
                {
                    GetArgs(args, binder.CallInfo, out pyargs, out kwargs);
                    result = CheckNone(Invoke(pyargs, kwargs));
                }
                finally
                {
                    pyargs?.Dispose();
                    kwargs?.Dispose();
                }
                return true;
            }
            else
            {
                return base.TryInvoke(binder, args, out result);
            }
        }

        public override bool TryConvert(ConvertBinder binder, out object? result)
        {
            using var _ = Py.GIL();
            // always try implicit conversion first
            if (Converter.ToManaged(this.obj, binder.Type, out result, false))
            {
                return true;
            }

            if (binder.Explicit)
            {
                Runtime.PyErr_Fetch(out var errType, out var errValue, out var tb);
                bool converted = Converter.ToManagedExplicit(Reference, binder.Type, out result);
                Runtime.PyErr_Restore(errType.StealNullable(), errValue.StealNullable(), tb.StealNullable());
                return converted;
            }

            if (binder.Type == typeof(System.Collections.IEnumerable) && this.IsIterable())
            {
                result = new PyIterable(this.Reference);
                return true;
            }

            return false;
        }

        private bool TryCompare(PyObject arg, int op, out object @out)
        {
            int result = Runtime.PyObject_RichCompareBool(this.obj, arg.obj, op);
            @out = result != 0;
            if (result < 0)
            {
                Exceptions.Clear();
                return false;
            }
            return true;
        }

        public override bool TryBinaryOperation(BinaryOperationBinder binder, object arg, out object? result)
        {
            using var _ = Py.GIL();
            NewReference res;
            if (arg is not PyObject)
            {
                arg = arg.ToPython();
            }

            switch (binder.Operation)
            {
                case ExpressionType.Add:
                    res = Runtime.PyNumber_Add(this.obj, ((PyObject)arg).obj);
                    break;
                case ExpressionType.AddAssign:
                    res = Runtime.PyNumber_InPlaceAdd(this.obj, ((PyObject)arg).obj);
                    break;
                case ExpressionType.Subtract:
                    res = Runtime.PyNumber_Subtract(this.obj, ((PyObject)arg).obj);
                    break;
                case ExpressionType.SubtractAssign:
                    res = Runtime.PyNumber_InPlaceSubtract(this.obj, ((PyObject)arg).obj);
                    break;
                case ExpressionType.Multiply:
                    res = Runtime.PyNumber_Multiply(this.obj, ((PyObject)arg).obj);
                    break;
                case ExpressionType.MultiplyAssign:
                    res = Runtime.PyNumber_InPlaceMultiply(this.obj, ((PyObject)arg).obj);
                    break;
                case ExpressionType.Divide:
                    res = Runtime.PyNumber_TrueDivide(this.obj, ((PyObject)arg).obj);
                    break;
                case ExpressionType.DivideAssign:
                    res = Runtime.PyNumber_InPlaceTrueDivide(this.obj, ((PyObject)arg).obj);
                    break;
                case ExpressionType.And:
                    res = Runtime.PyNumber_And(this.obj, ((PyObject)arg).obj);
                    break;
                case ExpressionType.AndAssign:
                    res = Runtime.PyNumber_InPlaceAnd(this.obj, ((PyObject)arg).obj);
                    break;
                case ExpressionType.ExclusiveOr:
                    res = Runtime.PyNumber_Xor(this.obj, ((PyObject)arg).obj);
                    break;
                case ExpressionType.ExclusiveOrAssign:
                    res = Runtime.PyNumber_InPlaceXor(this.obj, ((PyObject)arg).obj);
                    break;
                case ExpressionType.GreaterThan:
                    return this.TryCompare((PyObject)arg, Runtime.Py_GT, out result);
                case ExpressionType.GreaterThanOrEqual:
                    return this.TryCompare((PyObject)arg, Runtime.Py_GE, out result);
                case ExpressionType.LeftShift:
                    res = Runtime.PyNumber_Lshift(this.obj, ((PyObject)arg).obj);
                    break;
                case ExpressionType.LeftShiftAssign:
                    res = Runtime.PyNumber_InPlaceLshift(this.obj, ((PyObject)arg).obj);
                    break;
                case ExpressionType.LessThan:
                    return this.TryCompare((PyObject)arg, Runtime.Py_LT, out result);
                case ExpressionType.LessThanOrEqual:
                    return this.TryCompare((PyObject)arg, Runtime.Py_LE, out result);
                case ExpressionType.Modulo:
                    res = Runtime.PyNumber_Remainder(this.obj, ((PyObject)arg).obj);
                    break;
                case ExpressionType.ModuloAssign:
                    res = Runtime.PyNumber_InPlaceRemainder(this.obj, ((PyObject)arg).obj);
                    break;
                case ExpressionType.NotEqual:
                    return this.TryCompare((PyObject)arg, Runtime.Py_NE, out result);
                case ExpressionType.Equal:
                    return this.TryCompare((PyObject)arg, Runtime.Py_EQ, out result);
                case ExpressionType.Or:
                    res = Runtime.PyNumber_Or(this.obj, ((PyObject)arg).obj);
                    break;
                case ExpressionType.OrAssign:
                    res = Runtime.PyNumber_InPlaceOr(this.obj, ((PyObject)arg).obj);
                    break;
                case ExpressionType.Power:
                    res = Runtime.PyNumber_Power(this.obj, ((PyObject)arg).obj);
                    break;
                case ExpressionType.RightShift:
                    res = Runtime.PyNumber_Rshift(this.obj, ((PyObject)arg).obj);
                    break;
                case ExpressionType.RightShiftAssign:
                    res = Runtime.PyNumber_InPlaceRshift(this.obj, ((PyObject)arg).obj);
                    break;
                default:
                    result = null;
                    return false;
            }
            Exceptions.ErrorCheck(res.BorrowNullable());
            result = CheckNone(new PyObject(res.Borrow()));
            return true;
        }

        public static bool operator ==(PyObject? a, PyObject? b)
        {
            if (a is null && b is null)
            {
                return true;
            }
            if (a is null || b is null)
            {
                return false;
            }

            using var _ = Py.GIL();
            int result = Runtime.PyObject_RichCompareBool(a.obj, b.obj, Runtime.Py_EQ);
            if (result < 0) throw PythonException.ThrowLastAsClrException();
            return result != 0;
        }

        public static bool operator !=(PyObject? a, PyObject? b)
        {
            if (a is null && b is null)
            {
                return false;
            }
            if (a is null || b is null)
            {
                return true;
            }

            using var _ = Py.GIL();
            int result = Runtime.PyObject_RichCompareBool(a.obj, b.obj, Runtime.Py_NE);
            if (result < 0) throw PythonException.ThrowLastAsClrException();
            return result != 0;
        }

        // Workaround for https://bugzilla.xamarin.com/show_bug.cgi?id=41509
        // See https://github.com/pythonnet/pythonnet/pull/219
        internal static object? CheckNone(PyObject pyObj)
        {
            if (pyObj != null)
            {
                if (pyObj.obj == Runtime.PyNone)
                {
                    return null;
                }
            }

            return pyObj;
        }

        public override bool TryUnaryOperation(UnaryOperationBinder binder, out object? result)
        {
            using var _ = Py.GIL();
            int r;
            NewReference res;
            switch (binder.Operation)
            {
                case ExpressionType.Negate:
                    res = Runtime.PyNumber_Negative(this.obj);
                    break;
                case ExpressionType.UnaryPlus:
                    res = Runtime.PyNumber_Positive(this.obj);
                    break;
                case ExpressionType.OnesComplement:
                    res = Runtime.PyNumber_Invert(this.obj);
                    break;
                case ExpressionType.Not:
                    r = Runtime.PyObject_Not(this.obj);
                    result = r == 1;
                    if (r == -1) Exceptions.Clear();
                    return r != -1;
                case ExpressionType.IsFalse:
                    r = Runtime.PyObject_IsTrue(this.obj);
                    result = r == 0;
                    if (r == -1) Exceptions.Clear();
                    return r != -1;
                case ExpressionType.IsTrue:
                    r = Runtime.PyObject_IsTrue(this.obj);
                    result = r == 1;
                    if (r == -1) Exceptions.Clear();
                    return r != -1;
                case ExpressionType.Decrement:
                case ExpressionType.Increment:
                default:
                    result = null;
                    return false;
            }
            result = CheckNone(new PyObject(res.StealOrThrow()));
            return true;
        }

        /// <summary>
        /// Returns the enumeration of all dynamic member names.
        /// </summary>
        /// <remarks>
        /// This method exists for debugging purposes only.
        /// </remarks>
        /// <returns>A sequence that contains dynamic member names.</returns>
        public override IEnumerable<string> GetDynamicMemberNames()
        {
            using var _ = Py.GIL();
            return Dir().Select(pyObj => pyObj.ToString()!).ToArray();
        }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
            => GetObjectData(info, context);
        protected virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            Runtime.XIncref(this);
#pragma warning restore CS0618 // Type or member is obsolete
            info.AddValue("h", rawPtr.ToInt64());
            info.AddValue("r", run);
        }

        protected PyObject(SerializationInfo info, StreamingContext context)
        {
            rawPtr = (IntPtr)info.GetInt64("h");
            run = info.GetInt32("r");
            if (IsDisposed) GC.SuppressFinalize(this);
        }
    }

    internal static class PyObjectExtensions
    {
        internal static NewReference NewReferenceOrNull(this PyObject? self)
            => self is null || self.IsDisposed ? default : new NewReference(self);

        internal static BorrowedReference BorrowNullable(this PyObject? self)
            => self is null ? default : self.Reference;
    }
}
