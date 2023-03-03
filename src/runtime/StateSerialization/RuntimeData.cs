using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using Python.Runtime.StateSerialization;

using static Python.Runtime.Runtime;

namespace Python.Runtime
{
    public static class RuntimeData
    {
        private static Type? _formatterType;
        public static Type? FormatterType
        {
            get => _formatterType;
            set
            {
                if (!typeof(IFormatter).IsAssignableFrom(value))
                {
                    throw new ArgumentException("Not a type implemented IFormatter");
                }
                _formatterType = value;
            }
        }

        public static ICLRObjectStorer? WrappersStorer { get; set; }

        /// <summary>
        /// Clears the old "clr_data" entry if a previous one is present.
        /// </summary>
        static void ClearCLRData ()
        {
            BorrowedReference capsule = PySys_GetObject("clr_data");
            if (!capsule.IsNull)
            {
                IntPtr oldData = PyCapsule_GetPointer(capsule, IntPtr.Zero);
                PyMem_Free(oldData);
                PyCapsule_SetPointer(capsule, IntPtr.Zero);
            }
        }

        internal static void Stash()
        {
            return;
            var runtimeStorage = new PythonNetState
            {
                Metatype = MetaType.SaveRuntimeData(),
                ImportHookState = ImportHook.SaveRuntimeData(),
                Types = TypeManager.SaveRuntimeData(),
                Classes = ClassManager.SaveRuntimeData(),
                SharedObjects = SaveRuntimeDataObjects(),
            };

            IFormatter formatter = CreateFormatter();
            var ms = new MemoryStream();
            formatter.Serialize(ms, runtimeStorage);

            Debug.Assert(ms.Length <= int.MaxValue);
            byte[] data = ms.GetBuffer();
            // TODO: use buffer api instead
            IntPtr mem = PyMem_Malloc(ms.Length + IntPtr.Size);
            Marshal.WriteIntPtr(mem, (IntPtr)ms.Length);
            Marshal.Copy(data, 0, mem + IntPtr.Size, (int)ms.Length);

            ClearCLRData();

            using NewReference capsule = PyCapsule_New(mem, IntPtr.Zero, IntPtr.Zero);
            int res = PySys_SetObject("clr_data", capsule.BorrowOrThrow());
            PythonException.ThrowIfIsNotZero(res);
        }

        internal static void RestoreRuntimeData()
        {
            try
            {
                RestoreRuntimeDataImpl();
            }
            finally
            {
                ClearStash();
            }
        }

        private static void RestoreRuntimeDataImpl()
        {
            BorrowedReference capsule = PySys_GetObject("clr_data");
            if (capsule.IsNull)
            {
                return;
            }
            IntPtr mem = PyCapsule_GetPointer(capsule, IntPtr.Zero);
            int length = (int)Marshal.ReadIntPtr(mem);
            byte[] data = new byte[length];
            Marshal.Copy(mem + IntPtr.Size, data, 0, length);
            var ms = new MemoryStream(data);
            var formatter = CreateFormatter();
            var storage = (PythonNetState)formatter.Deserialize(ms);

            PyCLRMetaType = MetaType.RestoreRuntimeData(storage.Metatype);

            TypeManager.RestoreRuntimeData(storage.Types);
            ClassManager.RestoreRuntimeData(storage.Classes);

            RestoreRuntimeDataObjects(storage.SharedObjects);

            ImportHook.RestoreRuntimeData(storage.ImportHookState);
        }

        public static bool HasStashData()
        {
            return !PySys_GetObject("clr_data").IsNull;
        }

        public static void ClearStash()
        {
            PySys_SetObject("clr_data", default);
        }

        static bool CheckSerializable (object o)
        {
            Type type = o.GetType();
            do
            {
                if (!type.IsSerializable)
                {
                    return false;
                }
            } while ((type = type.BaseType) != null);
            return true;
        }

        private static SharedObjectsState SaveRuntimeDataObjects()
        {
            var contexts = new Dictionary<PyObject, Dictionary<string, object?>>(PythonReferenceComparer.Instance);

            var wrappers = new Dictionary<object, List<CLRObject>>();
            var userObjects = new CLRWrapperCollection();

            var wrapperStorage = new Dictionary<string, object?>();
            WrappersStorer?.Store(userObjects, wrapperStorage);

            var internalStores = new Dictionary<PyObject, CLRObject>(PythonReferenceComparer.Instance);
            foreach (var item in userObjects)
            {
                if (!item.Stored)
                {
                    if (!CheckSerializable(item.Instance))
                    {
                        continue;
                    }
                    var clrO = wrappers[item.Instance].First();
                    foreach (var @ref in item.PyRefs)
                    {
                        internalStores.Add(@ref, clrO);
                    }
                }
            }

            return new()
            {
                InternalStores = internalStores,
                Wrappers = wrapperStorage,
                Contexts = contexts,
            };
        }

        private static void RestoreRuntimeDataObjects(SharedObjectsState storage)
        {
            var extensions = storage.Extensions;
            var internalStores = storage.InternalStores;
            var contexts = storage.Contexts;
            foreach (var extension in extensions)
            {
                contexts.TryGetValue(extension.Key, out var context);
                extension.Value.Load(extension.Key, context);
            }
            foreach (var clrObj in internalStores)
            {
                clrObj.Value.Load(clrObj.Key, null);
            }
            if (WrappersStorer != null)
            {
                var wrapperStorage = storage.Wrappers;
                var handle2Obj = WrappersStorer.Restore(wrapperStorage);
                foreach (var item in handle2Obj)
                {
                    object obj = item.Instance;
                    foreach (var pyRef in item.PyRefs ?? new List<PyObject>())
                    {
                        var context = contexts[pyRef];
                        CLRObject.Restore(obj, pyRef, context);
                    }
                }
            }
        }

        internal static IFormatter CreateFormatter()
        {
            return FormatterType != null ?
                (IFormatter)Activator.CreateInstance(FormatterType)
                : new BinaryFormatter();
        }
    }
}
