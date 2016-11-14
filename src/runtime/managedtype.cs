using System;
using System.Runtime.InteropServices;
using System.Collections;
using System.Reflection;

namespace Python.Runtime
{
    //========================================================================
    // Common base class for all objects that are implemented in managed
    // code. It defines the common fields that associate CLR and Python
    // objects and common utilities to convert between those identities.
    //========================================================================

    internal abstract class ManagedType
    {
        internal GCHandle gcHandle; // Native handle
        internal IntPtr pyHandle; // PyObject *
        internal IntPtr tpHandle; // PyType *


        //====================================================================
        // Given a Python object, return the associated managed object or null.
        //====================================================================

        internal static ManagedType GetManagedObject(IntPtr ob)
        {
            if (ob != IntPtr.Zero)
            {
                IntPtr tp = Runtime.PyObject_TYPE(ob);
                if (tp == Runtime.PyTypeType || tp == Runtime.PyCLRMetaType)
                {
                    tp = ob;
                }

                int flags = (int)Marshal.ReadIntPtr(tp, TypeOffset.tp_flags);
                if ((flags & TypeFlags.Managed) != 0)
                {
                    IntPtr op = (tp == ob)
                        ? Marshal.ReadIntPtr(tp, TypeOffset.magic())
                        : Marshal.ReadIntPtr(ob, ObjectOffset.magic(ob));
                    GCHandle gc = (GCHandle)op;
                    return (ManagedType)gc.Target;
                }
            }
            return null;
        }


        internal static ManagedType GetManagedObjectErr(IntPtr ob)
        {
            ManagedType result = GetManagedObject(ob);
            if (result == null)
            {
                Exceptions.SetError(Exceptions.TypeError,
                    "invalid argument, expected CLR type"
                    );
            }
            return result;
        }


        internal static bool IsManagedType(IntPtr ob)
        {
            if (ob != IntPtr.Zero)
            {
                IntPtr tp = Runtime.PyObject_TYPE(ob);
                if (tp == Runtime.PyTypeType || tp == Runtime.PyCLRMetaType)
                {
                    tp = ob;
                }

                int flags = (int)Marshal.ReadIntPtr(tp, TypeOffset.tp_flags);
                if ((flags & TypeFlags.Managed) != 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}