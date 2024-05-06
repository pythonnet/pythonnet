using System;
using System.Collections.Generic;
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

        public readonly static Func<IFormatter> DefaultFormatterFactory = () =>
        {
            try
            {
                return new BinaryFormatter();
            }
            catch
            {
                return new NoopFormatter();
            }
        };

        private static Func<IFormatter> _formatterFactory { get; set; } = DefaultFormatterFactory;

        public static Func<IFormatter> FormatterFactory
        {
            get => _formatterFactory;
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                _formatterFactory = value;
            }
        }

        private static Type? _formatterType = null;
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

        /// <summary>
        /// Callback called as a last step in the serialization process
        /// </summary>
        public static Action? PostStashHook { get; set; } = null;
        /// <summary>
        /// Callback called as the first step in the deserialization process
        /// </summary>
        public static Action? PreRestoreHook { get; set; } = null;
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
            PostStashHook?.Invoke();
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
            PreRestoreHook?.Invoke();
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
            var extensionObjs = new Dictionary<PyObject, ExtensionType>(PythonReferenceComparer.Instance);
            // make a copy with strongly typed references to avoid concurrent modification
            var extensions = ExtensionType.loadedExtensions
                                .Select(addr => new PyObject(
                                    new BorrowedReference(addr),
                                    // if we don't skip collect, finalizer might modify loadedExtensions
                                    skipCollect: true))
                                .ToArray();
            foreach (var pyObj in extensions)
            {
                var extension = (ExtensionType)ManagedType.GetManagedObject(pyObj)!;
                Debug.Assert(CheckSerializable(extension));
                var context = extension.Save(pyObj);
                if (context is not null)
                {
                    contexts[pyObj] = context;
                }
                extensionObjs.Add(pyObj, extension);
            }

            var wrappers = new Dictionary<object, List<CLRObject>>();
            var userObjects = new CLRWrapperCollection();
            // make a copy with strongly typed references to avoid concurrent modification
            var reflectedObjects = CLRObject.reflectedObjects
                                    .Select(addr => new PyObject(
                                        new BorrowedReference(addr),
                                        // if we don't skip collect, finalizer might modify reflectedObjects
                                        skipCollect: true))
                                    .ToList();
            foreach (var pyObj in reflectedObjects)
            {
                // Wrapper must be the CLRObject
                var clrObj = (CLRObject)ManagedType.GetManagedObject(pyObj)!;
                object inst = clrObj.inst;
                List<CLRObject> mappedObjs;
                if (!userObjects.TryGetValue(inst, out var item))
                {
                    item = new CLRMappedItem(inst);
                    userObjects.Add(item);

                    Debug.Assert(!wrappers.ContainsKey(inst));
                    mappedObjs = new List<CLRObject>();
                    wrappers.Add(inst, mappedObjs);
                }
                else
                {
                    mappedObjs = wrappers[inst];
                }
                item.AddRef(pyObj);
                mappedObjs.Add(clrObj);
            }

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
                Extensions = extensionObjs,
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

        static readonly string serialization_key_namepsace = "pythonnet_serialization_";
        /// <summary>
        /// Removes the serialization capsule from the `sys` module object.
        /// </summary>
        /// <remarks>
        /// The serialization data must have been set with <code>StashSerializationData</code>
        /// </remarks>
        /// <param name="key">The name given to the capsule on the `sys` module object</param>
        public static void FreeSerializationData(string key)
        {
            key = serialization_key_namepsace + key;
            BorrowedReference oldCapsule = PySys_GetObject(key);
            if (!oldCapsule.IsNull)
            {
                IntPtr oldData = PyCapsule_GetPointer(oldCapsule, IntPtr.Zero);
                Marshal.FreeHGlobal(oldData);
                PyCapsule_SetPointer(oldCapsule, IntPtr.Zero);
                PySys_SetObject(key, null);
            }
        }

        /// <summary>
        /// Stores the data in the <paramref name="stream"/> argument in a Python capsule and stores
        /// the capsule on the `sys` module object with the name <paramref name="key"/>.
        /// </summary>
        /// <remarks>
        /// No checks on pre-existing names on the `sys` module object are made.
        /// </remarks>
        /// <param name="key">The name given to the capsule on the `sys` module object</param>
        /// <param name="stream">A MemoryStream that contains the data to be placed in the capsule</param>
        public static void StashSerializationData(string key, MemoryStream stream)
        {
            if (stream.TryGetBuffer(out var data))
            {
                IntPtr mem = Marshal.AllocHGlobal(IntPtr.Size + data.Count);

                // store the length of the buffer first
                Marshal.WriteIntPtr(mem, (IntPtr)data.Count);
                Marshal.Copy(data.Array, data.Offset, mem + IntPtr.Size, data.Count);

                try
                {
                    using NewReference capsule = PyCapsule_New(mem, IntPtr.Zero, IntPtr.Zero);
                    int res = PySys_SetObject(key, capsule.BorrowOrThrow());
                    PythonException.ThrowIfIsNotZero(res);
                }
                catch
                {
                    Marshal.FreeHGlobal(mem);
                }
            }
            else
            {
                throw new NotImplementedException($"{nameof(stream)} must be exposable");
            }

        }

        static byte[] emptyBuffer = new byte[0];
        /// <summary>
        /// Retreives the previously stored data on a Python capsule.
        /// Throws if the object corresponding to the <paramref name="key"/> parameter
        /// on the `sys` module object is not a capsule.
        /// </summary>
        /// <param name="key">The name given to the capsule on the `sys` module object</param>
        /// <returns>A MemoryStream containing the previously saved serialization data.
        /// The stream is empty if no name matches the key.  </returns>
        public static MemoryStream GetSerializationData(string key)
        {
            BorrowedReference capsule = PySys_GetObject(key);
            if (capsule.IsNull)
            {
                // nothing to do.
                return new MemoryStream(emptyBuffer, writable:false);
            }
            var ptr = PyCapsule_GetPointer(capsule, IntPtr.Zero);
            if (ptr == IntPtr.Zero)
            {
                // The PyCapsule API returns NULL on error; NULL cannot be stored
                // as a capsule's value
                PythonException.ThrowIfIsNull(null);
            }
            var len = (int)Marshal.ReadIntPtr(ptr);
            byte[] buffer = new byte[len];
            Marshal.Copy(ptr+IntPtr.Size, buffer, 0, len);
            return new MemoryStream(buffer, writable:false);
        }

        internal static IFormatter CreateFormatter()
        {

            if (FormatterType != null)
            {
                return (IFormatter)Activator.CreateInstance(FormatterType);
            }
            return FormatterFactory();
        }
    }
}
