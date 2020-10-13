using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace Python.Runtime
{
    /// <summary>
    /// Base class for Python types that reflect managed types / classes.
    /// Concrete subclasses include ClassObject and DelegateObject. This
    /// class provides common attributes and common machinery for doing
    /// class initialization (initialization of the class __dict__). The
    /// concrete subclasses provide slot implementations appropriate for
    /// each variety of reflected type.
    /// </summary>
    [Serializable]
    internal class ClassBase : ManagedType
    {
        internal Indexer indexer;
        internal Type type;

        internal ClassBase(Type tp)
        {
            indexer = null;
            type = tp;
        }

        internal virtual bool CanSubclass()
        {
            return !type.IsEnum;
        }


        /// <summary>
        /// Default implementation of [] semantics for reflected types.
        /// </summary>
        public virtual IntPtr type_subscript(IntPtr idx)
        {
            Type[] types = Runtime.PythonArgsToTypeArray(idx);
            if (types == null)
            {
                return Exceptions.RaiseTypeError("type(s) expected");
            }

            Type target = GenericUtil.GenericForType(type, types.Length);

            if (target != null)
            {
                Type t = target.MakeGenericType(types);
                ManagedType c = ClassManager.GetClass(t);
                Runtime.XIncref(c.pyHandle);
                return c.pyHandle;
            }

            return Exceptions.RaiseTypeError("no type matches params");
        }

        /// <summary>
        /// Standard comparison implementation for instances of reflected types.
        /// </summary>
        public static IntPtr tp_richcompare(IntPtr ob, IntPtr other, int op)
        {
            CLRObject co1;
            CLRObject co2;
            switch (op)
            {
                case Runtime.Py_EQ:
                case Runtime.Py_NE:
                    IntPtr pytrue = Runtime.PyTrue;
                    IntPtr pyfalse = Runtime.PyFalse;

                    // swap true and false for NE
                    if (op != Runtime.Py_EQ)
                    {
                        pytrue = Runtime.PyFalse;
                        pyfalse = Runtime.PyTrue;
                    }

                    if (ob == other)
                    {
                        Runtime.XIncref(pytrue);
                        return pytrue;
                    }

                    co1 = GetManagedObject(ob) as CLRObject;
                    co2 = GetManagedObject(other) as CLRObject;
                    if (null == co2)
                    {
                        Runtime.XIncref(pyfalse);
                        return pyfalse;
                    }

                    object o1 = co1.inst;
                    object o2 = co2.inst;

                    if (Equals(o1, o2))
                    {
                        Runtime.XIncref(pytrue);
                        return pytrue;
                    }

                    Runtime.XIncref(pyfalse);
                    return pyfalse;
                case Runtime.Py_LT:
                case Runtime.Py_LE:
                case Runtime.Py_GT:
                case Runtime.Py_GE:
                    co1 = GetManagedObject(ob) as CLRObject;
                    co2 = GetManagedObject(other) as CLRObject;
                    if (co1 == null || co2 == null)
                    {
                        return Exceptions.RaiseTypeError("Cannot get managed object");
                    }
                    var co1Comp = co1.inst as IComparable;
                    if (co1Comp == null)
                    {
                        Type co1Type = co1.GetType();
                        return Exceptions.RaiseTypeError($"Cannot convert object of type {co1Type} to IComparable");
                    }
                    try
                    {
                        int cmp = co1Comp.CompareTo(co2.inst);

                        IntPtr pyCmp;
                        if (cmp < 0)
                        {
                            if (op == Runtime.Py_LT || op == Runtime.Py_LE)
                            {
                                pyCmp = Runtime.PyTrue;
                            }
                            else
                            {
                                pyCmp = Runtime.PyFalse;
                            }
                        }
                        else if (cmp == 0)
                        {
                            if (op == Runtime.Py_LE || op == Runtime.Py_GE)
                            {
                                pyCmp = Runtime.PyTrue;
                            }
                            else
                            {
                                pyCmp = Runtime.PyFalse;
                            }
                        }
                        else
                        {
                            if (op == Runtime.Py_GE || op == Runtime.Py_GT)
                            {
                                pyCmp = Runtime.PyTrue;
                            }
                            else
                            {
                                pyCmp = Runtime.PyFalse;
                            }
                        }
                        Runtime.XIncref(pyCmp);
                        return pyCmp;
                    }
                    catch (ArgumentException e)
                    {
                        return Exceptions.RaiseTypeError(e.Message);
                    }
                default:
                    Runtime.XIncref(Runtime.PyNotImplemented);
                    return Runtime.PyNotImplemented;
            }
        }

        /// <summary>
        /// Standard iteration support for instances of reflected types. This
        /// allows natural iteration over objects that either are IEnumerable
        /// or themselves support IEnumerator directly.
        /// </summary>
        public static IntPtr tp_iter(IntPtr ob)
        {
            var co = GetManagedObject(ob) as CLRObject;
            if (co == null)
            {
                return Exceptions.RaiseTypeError("invalid object");
            }

            var e = co.inst as IEnumerable;
            IEnumerator o;
            if (e != null)
            {
                o = e.GetEnumerator();
            }
            else
            {
                o = co.inst as IEnumerator;

                if (o == null)
                {
                    return Exceptions.RaiseTypeError("iteration over non-sequence");
                }
            }

            var elemType = typeof(object);
            var iterType = co.inst.GetType();
            foreach(var ifc in iterType.GetInterfaces())
            {
                if (ifc.IsGenericType)
                {
                    var genTypeDef = ifc.GetGenericTypeDefinition();
                    if (genTypeDef == typeof(IEnumerable<>) || genTypeDef == typeof(IEnumerator<>))
                    {
                        elemType = ifc.GetGenericArguments()[0];
                        break;
                    }
                }
            }

            return new Iterator(o, elemType).pyHandle;
        }


        /// <summary>
        /// Standard __hash__ implementation for instances of reflected types.
        /// </summary>
        public static IntPtr tp_hash(IntPtr ob)
        {
            var co = GetManagedObject(ob) as CLRObject;
            if (co == null)
            {
                return Exceptions.RaiseTypeError("unhashable type");
            }
            return new IntPtr(co.inst.GetHashCode());
        }


        /// <summary>
        /// Standard __str__ implementation for instances of reflected types.
        /// </summary>
        public static IntPtr tp_str(IntPtr ob)
        {
            var co = GetManagedObject(ob) as CLRObject;
            if (co == null)
            {
                return Exceptions.RaiseTypeError("invalid object");
            }
            try
            {
                return Runtime.PyString_FromString(co.inst.ToString());
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                {
                    e = e.InnerException;
                }
                Exceptions.SetError(e);
                return IntPtr.Zero;
            }
        }

        public static IntPtr tp_repr(IntPtr ob)
        {
            var co = GetManagedObject(ob) as CLRObject;
            if (co == null)
            {
                return Exceptions.RaiseTypeError("invalid object");
            }
            try
            {
                //if __repr__ is defined, use it
                var instType = co.inst.GetType();
                System.Reflection.MethodInfo methodInfo = instType.GetMethod("__repr__");
                if (methodInfo != null && methodInfo.IsPublic)
                {
                    var reprString = methodInfo.Invoke(co.inst, null) as string;
                    return Runtime.PyString_FromString(reprString);
                }

                //otherwise use the standard object.__repr__(inst)
                IntPtr args = Runtime.PyTuple_New(1);
                Runtime.XIncref(ob);
                Runtime.PyTuple_SetItem(args, 0, ob);
                IntPtr reprFunc = Runtime.PyObject_GetAttrString(Runtime.PyBaseObjectType, "__repr__");
                var output =  Runtime.PyObject_Call(reprFunc, args, IntPtr.Zero);
                Runtime.XDecref(args);
                Runtime.XDecref(reprFunc);
                return output;
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                {
                    e = e.InnerException;
                }
                Exceptions.SetError(e);
                return IntPtr.Zero;
            }
        }


        /// <summary>
        /// Standard dealloc implementation for instances of reflected types.
        /// </summary>
        public static void tp_dealloc(IntPtr ob)
        {
            ManagedType self = GetManagedObject(ob);
            tp_clear(ob);
            Runtime.PyObject_GC_UnTrack(self.pyHandle);
            Runtime.PyObject_GC_Del(self.pyHandle);
            self.FreeGCHandle();
        }

        public static int tp_clear(IntPtr ob)
        {
            ManagedType self = GetManagedObject(ob);
            if (!self.IsTypeObject())
            {
                ClearObjectDict(ob);
            }
            self.tpHandle = IntPtr.Zero;
            return 0;
        }

        protected override void OnSave(InterDomainContext context)
        {
            base.OnSave(context);
            if (pyHandle != tpHandle)
            {
                IntPtr dict = GetObjectDict(pyHandle);
                Runtime.XIncref(dict);
                context.Storage.AddValue("dict", dict);
            }
        }

        protected override void OnLoad(InterDomainContext context)
        {
            base.OnLoad(context);
            if (pyHandle != tpHandle)
            {
                IntPtr dict = context.Storage.GetValue<IntPtr>("dict");
                SetObjectDict(pyHandle, dict);
            }
            gcHandle = AllocGCHandle();
            Marshal.WriteIntPtr(pyHandle, TypeOffset.magic(), (IntPtr)gcHandle);
        }


        /// <summary>
        /// Implements __getitem__ for reflected classes and value types.
        /// </summary>
        public static IntPtr mp_subscript(IntPtr ob, IntPtr idx)
        {
            IntPtr tp = Runtime.PyObject_TYPE(ob);
            var cls = (ClassBase)GetManagedObject(tp);

            if (cls.indexer == null || !cls.indexer.CanGet)
            {
                Exceptions.SetError(Exceptions.TypeError, "unindexable object");
                return IntPtr.Zero;
            }

            // Arg may be a tuple in the case of an indexer with multiple
            // parameters. If so, use it directly, else make a new tuple
            // with the index arg (method binders expect arg tuples).
            IntPtr args = idx;
            var free = false;

            if (!Runtime.PyTuple_Check(idx))
            {
                args = Runtime.PyTuple_New(1);
                Runtime.XIncref(idx);
                Runtime.PyTuple_SetItem(args, 0, idx);
                free = true;
            }

            IntPtr value;

            try
            {
                value = cls.indexer.GetItem(ob, args);
            }
            finally
            {
                if (free)
                {
                    Runtime.XDecref(args);
                }
            }
            return value;
        }


        /// <summary>
        /// Implements __setitem__ for reflected classes and value types.
        /// </summary>
        public static int mp_ass_subscript(IntPtr ob, IntPtr idx, IntPtr v)
        {
            IntPtr tp = Runtime.PyObject_TYPE(ob);
            var cls = (ClassBase)GetManagedObject(tp);

            if (cls.indexer == null || !cls.indexer.CanSet)
            {
                Exceptions.SetError(Exceptions.TypeError, "object doesn't support item assignment");
                return -1;
            }

            // Arg may be a tuple in the case of an indexer with multiple
            // parameters. If so, use it directly, else make a new tuple
            // with the index arg (method binders expect arg tuples).
            IntPtr args = idx;
            var free = false;

            if (!Runtime.PyTuple_Check(idx))
            {
                args = Runtime.PyTuple_New(1);
                Runtime.XIncref(idx);
                Runtime.PyTuple_SetItem(args, 0, idx);
                free = true;
            }

            // Get the args passed in.
            var i = Runtime.PyTuple_Size(args);
            IntPtr defaultArgs = cls.indexer.GetDefaultArgs(args);
            var numOfDefaultArgs = Runtime.PyTuple_Size(defaultArgs);
            var temp = i + numOfDefaultArgs;
            IntPtr real = Runtime.PyTuple_New(temp + 1);
            for (var n = 0; n < i; n++)
            {
                IntPtr item = Runtime.PyTuple_GetItem(args, n);
                Runtime.XIncref(item);
                Runtime.PyTuple_SetItem(real, n, item);
            }

            // Add Default Args if needed
            for (var n = 0; n < numOfDefaultArgs; n++)
            {
                IntPtr item = Runtime.PyTuple_GetItem(defaultArgs, n);
                Runtime.XIncref(item);
                Runtime.PyTuple_SetItem(real, n + i, item);
            }
            // no longer need defaultArgs
            Runtime.XDecref(defaultArgs);
            i = temp;

            // Add value to argument list
            Runtime.XIncref(v);
            Runtime.PyTuple_SetItem(real, i, v);

            try
            {
                cls.indexer.SetItem(ob, real);
            }
            finally
            {
                Runtime.XDecref(real);

                if (free)
                {
                    Runtime.XDecref(args);
                }
            }

            if (Exceptions.ErrorOccurred())
            {
                return -1;
            }

            return 0;
        }
    }
}
