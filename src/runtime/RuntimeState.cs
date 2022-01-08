using System;
using System.Collections.Generic;
using System.Diagnostics;

using static Python.Runtime.Runtime;

namespace Python.Runtime
{
    class RuntimeState
    {
        public static void Save()
        {
            if (!PySys_GetObject("initial_modules").IsNull)
            {
                throw new Exception("Runtime State set already");
            }

            using var modules = PySet_New(default);
            int res = PySys_SetObject("initial_modules", modules.Borrow());
            PythonException.ThrowIfIsNotZero(res);

            foreach (var name in GetModuleNames())
            {
                res = PySet_Add(modules.Borrow(), new BorrowedReference(name));
                PythonException.ThrowIfIsNotZero(res);
            }
        }

        public static void Restore()
        {
            RestoreModules();
        }

        private static void RestoreModules()
        {
            var intialModules = PySys_GetObject("initial_modules");
            Debug.Assert(!intialModules.IsNull);
            var modules = PyImport_GetModuleDict();
            foreach (var nameRaw in GetModuleNames())
            {
                var name = new BorrowedReference(nameRaw);
                if (PySet_Contains(intialModules, name) == 1)
                {
                    continue;
                }
                if (PyDict_DelItem(modules, name) != 0)
                {
                    PyErr_Print();
                }
            }
        }

        public static IEnumerable<IntPtr> GetModuleNames()
        {
            var modules = PyImport_GetModuleDict();
            using var names = PyDict_Keys(modules);
            nint length = PyList_Size(names.BorrowOrThrow());
            if (length < 0) throw PythonException.ThrowLastAsClrException();
            var result = new IntPtr[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = PyList_GetItem(names.Borrow(), i).DangerousGetAddress();
            }
            return result;
        }
    }
}
