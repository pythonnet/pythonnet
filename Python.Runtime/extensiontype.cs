using System;
using System.Runtime.InteropServices;

namespace Python.Runtime
{
    /// <summary>
    /// Base class for extensions whose instances *share* a single Python
    /// type object, such as the types that represent CLR methods, fields,
    /// etc. Instances implemented by this class do not support sub-typing.
    /// </summary>
    internal abstract class ExtensionType : ManagedType
    {
        public ExtensionType()
        {
            // Create a new PyObject whose type is a generated type that is
            // implemented by the particular concrete ExtensionType subclass.
            // The Python instance object is related to an instance of a
            // particular concrete subclass with a hidden CLR gchandle.

            IntPtr tp = TypeManager.GetTypeHandle(GetType());

            //int rc = (int)Marshal.ReadIntPtr(tp, TypeOffset.ob_refcnt);
            //if (rc > 1050)
            //{
            //    DebugUtil.Print("tp is: ", tp);
            //    DebugUtil.DumpType(tp);
            //}

            IntPtr py = Runtime.PyType_GenericAlloc(tp, 0);

            GCHandle gc = GCHandle.Alloc(this);
            Marshal.WriteIntPtr(py, ObjectOffset.magic(tp), (IntPtr)gc);

            // We have to support gc because the type machinery makes it very
            // hard not to - but we really don't have a need for it in most
            // concrete extension types, so untrack the object to save calls
            // from Python into the managed runtime that are pure overhead.

            Runtime.PyObject_GC_UnTrack(py);

            tpHandle = tp;
            pyHandle = py;
            gcHandle = gc;
        }


        /// <summary>
        /// Common finalization code to support custom tp_deallocs.
        /// </summary>
        public static void FinalizeObject(ManagedType self)
        {
            Runtime.PyObject_GC_Del(self.pyHandle);
            Runtime.XDecref(self.tpHandle);
            self.gcHandle.Free();
        }


        /// <summary>
        /// Type __setattr__ implementation.
        /// </summary>
        public static int tp_setattro(IntPtr ob, IntPtr key, IntPtr val)
        {
            var message = "type does not support setting attributes";
            if (val == IntPtr.Zero)
            {
                message = "readonly attribute";
            }
            Exceptions.SetError(Exceptions.TypeError, message);
            return -1;
        }


        /// <summary>
        /// Default __set__ implementation - this prevents descriptor instances
        /// being silently replaced in a type __dict__ by default __setattr__.
        /// </summary>
        public static int tp_descr_set(IntPtr ds, IntPtr ob, IntPtr val)
        {
            Exceptions.SetError(Exceptions.AttributeError, "attribute is read-only");
            return -1;
        }


        /// <summary>
        /// Default dealloc implementation.
        /// </summary>
        public static void tp_dealloc(IntPtr ob)
        {
            // Clean up a Python instance of this extension type. This
            // frees the allocated Python object and decrefs the type.
            ManagedType self = GetManagedObject(ob);
            FinalizeObject(self);
        }
    }
}
