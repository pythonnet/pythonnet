using System;

namespace Python.Runtime.Slots
{
    /// <summary>
    /// Implement this interface to override Python's __getattr__ for your class
    /// </summary>
    public interface IGetAttr {
        bool TryGetAttr(string name, out PyObject value);
    }

    /// <summary>
    /// Implement this interface to override Python's __setattr__ for your class
    /// </summary>
    public interface ISetAttr {
        bool TrySetAttr(string name, PyObject value);
    }

    static class SlotOverrides {
        public static IntPtr tp_getattro(IntPtr ob, IntPtr key) {
            IntPtr genericResult = Runtime.PyObject_GenericGetAttr(ob, key);
            if (genericResult != IntPtr.Zero || !Runtime.PyString_Check(key)) {
                return genericResult;
            }

            Exceptions.Clear();

            var self = (IGetAttr)((CLRObject)ManagedType.GetManagedObject(ob)).inst;
            string attr = Runtime.GetManagedString(key);
            return self.TryGetAttr(attr, out var value)
                ? Runtime.SelfIncRef(value.Handle)
                : Runtime.PyObject_GenericGetAttr(ob, key);
        }

        public static int tp_setattro(IntPtr ob, IntPtr key, IntPtr val) {
            if (!Runtime.PyString_Check(key)) {
                return Runtime.PyObject_GenericSetAttr(ob, key, val);
            }

            var self = (ISetAttr)((CLRObject)ManagedType.GetManagedObject(ob)).inst;
            string attr = Runtime.GetManagedString(key);
            return self.TrySetAttr(attr, new PyObject(Runtime.SelfIncRef(val)))
                ? 0
                : Runtime.PyObject_GenericSetAttr(ob, key, val);
        }
    }
}
