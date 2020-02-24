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

using static Python.Runtime.Runtime;

namespace Python.Runtime
{
    public static class RuntimeData
    {
        private static Type _formatterType;
        public static Type FormatterType
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

        public static ICLRObjectStorer WrappersStorer { get; set; }

        internal static void Stash()
        {
            var metaStorage = new RuntimeDataStorage();
            MetaType.StashPush(metaStorage);

            var typeStorage = new RuntimeDataStorage();
            TypeManager.StashPush(typeStorage);

            var clsStorage = new RuntimeDataStorage();
            ClassManager.StashPush(clsStorage);

            var moduleStorage = new RuntimeDataStorage();
            StashPushModules(moduleStorage);

            var objStorage = new RuntimeDataStorage();
            StashPushObjects(objStorage);

            var runtimeStorage = new RuntimeDataStorage();
            runtimeStorage.AddValue("meta", metaStorage);
            runtimeStorage.AddValue("types", typeStorage);
            runtimeStorage.AddValue("classes", clsStorage);
            runtimeStorage.AddValue("modules", moduleStorage);
            runtimeStorage.AddValue("objs", objStorage);

            IFormatter formatter = CreateFormatter();
            var ms = new MemoryStream();
            formatter.Serialize(ms, runtimeStorage);

            Debug.Assert(ms.Length <= int.MaxValue);
            byte[] data = ms.GetBuffer();
            // TODO: use buffer api instead
            IntPtr mem = PyMem_Malloc(ms.Length + IntPtr.Size);
            Marshal.WriteIntPtr(mem, (IntPtr)ms.Length);
            Marshal.Copy(data, 0, mem + IntPtr.Size, (int)ms.Length);

            IntPtr capsule = PySys_GetObject("clr_data");
            if (capsule != IntPtr.Zero)
            {
                IntPtr oldData = PyCapsule_GetPointer(capsule, null);
                PyMem_Free(oldData);
                PyCapsule_SetPointer(capsule, IntPtr.Zero);
            }
            capsule = PyCapsule_New(mem, null, IntPtr.Zero);
            PySys_SetObject("clr_data", capsule);
            XDecref(capsule);
        }


        internal static void StashPop()
        {
            try
            {
                StashPopImpl();
            }
            finally
            {
                ClearStash();
            }
        }

        private static void StashPopImpl()
        {
            IntPtr capsule = PySys_GetObject("clr_data");
            if (capsule == IntPtr.Zero)
            {
                return;
            }
            IntPtr mem = PyCapsule_GetPointer(capsule, null);
            int length = (int)Marshal.ReadIntPtr(mem);
            byte[] data = new byte[length];
            Marshal.Copy(mem + IntPtr.Size, data, 0, length);
            var ms = new MemoryStream(data);
            var formatter = CreateFormatter();
            var storage = (RuntimeDataStorage)formatter.Deserialize(ms);

            StashPopModules(storage.GetStorage("modules"));
            StashPopObjects(storage.GetStorage("objs"));
            ClassManager.StashPop(storage.GetStorage("classes"));
            TypeManager.StashPop(storage.GetStorage("types"));
            PyCLRMetaType = MetaType.StashPop(storage.GetStorage("meta"));
        }

        public static bool HasStashData()
        {
            return PySys_GetObject("clr_data") != IntPtr.Zero;
        }

        public static void ClearStash()
        {
            PySys_SetObject("clr_data", IntPtr.Zero);
        }

        private static void StashPushObjects(RuntimeDataStorage storage)
        {
            var objs = ManagedType.GetManagedObjects();
            var extensionObjs = new List<ManagedType>();
            var wrappers = new Dictionary<object, List<CLRObject>>();
            var serializeObjs = new CLRWrapperCollection();
            foreach (var entry in objs)
            {
                var obj = entry.Key;
                switch (entry.Value)
                {
                    case ManagedType.TrackTypes.Extension:
                        Debug.Assert(obj.GetType().IsSerializable);
                        obj.Save();
                        extensionObjs.Add(obj);
                        break;
                    case ManagedType.TrackTypes.Wrapper:
                        // Wrapper must be the CLRObject
                        var clrObj = (CLRObject)obj;
                        object inst = clrObj.inst;
                        CLRMappedItem item;
                        List<CLRObject> mappedObjs;
                        if (!serializeObjs.TryGetValue(inst, out item))
                        {
                            item = new CLRMappedItem(inst)
                            {
                                Handles = new List<IntPtr>()
                            };
                            serializeObjs.Add(item);

                            Debug.Assert(!wrappers.ContainsKey(inst));
                            mappedObjs = new List<CLRObject>();
                            wrappers.Add(inst, mappedObjs);
                        }
                        else
                        {
                            mappedObjs = wrappers[inst];
                        }
                        item.Handles.Add(clrObj.pyHandle);
                        mappedObjs.Add(clrObj);
                        break;
                    default:
                        break;
                }
            }

            var wrapperStorage = new RuntimeDataStorage();
            WrappersStorer?.Store(serializeObjs, wrapperStorage);

            var internalStores = new List<CLRObject>();
            foreach (var item in serializeObjs)
            {
                if (!item.Stored)
                {
                    if (!item.Instance.GetType().IsSerializable)
                    {
                        continue;
                    }
                    internalStores.AddRange(wrappers[item.Instance]);
                }
                foreach (var clrObj in wrappers[item.Instance])
                {
                    clrObj.Save();
                }
            }
            storage.AddValue("internalStores", internalStores);
            storage.AddValue("extensions", extensionObjs);
            storage.AddValue("wrappers", wrapperStorage);
        }

        private static void StashPopObjects(RuntimeDataStorage storage)
        {
            var extensions = storage.GetValue<List<ManagedType>>("extensions");
            var internalStores = storage.GetValue<List<CLRObject>>("internalStores");
            foreach (var obj in Enumerable.Union(extensions, internalStores))
            {
                obj.Load();
            }
            if (WrappersStorer != null)
            {
                var wrapperStorage = storage.GetStorage("wrappers");
                var handle2Obj = WrappersStorer.Restore(wrapperStorage);
                foreach (var item in handle2Obj)
                {
                    object obj = item.Instance;
                    foreach (var handle in item.Handles)
                    {
                        CLRObject.Restore(obj, handle);
                    }
                }
            }
        }

        private static void StashPushModules(RuntimeDataStorage storage)
        {
            var pyModules = PyImport_GetModuleDict();
            var items = PyDict_Items(pyModules);
            long length = PyList_Size(items);
            var modules = new Dictionary<IntPtr, IntPtr>(); ;
            for (long i = 0; i < length; i++)
            {
                var item = PyList_GetItem(items, i);
                var name = PyTuple_GetItem(item, 0);
                var module = PyTuple_GetItem(item, 1);
                if (ManagedType.IsManagedType(module))
                {
                    XIncref(name);
                    XIncref(module);
                    modules.Add(name, module);
                }
            }
            XDecref(items);
            storage.AddValue("modules", modules);
        }

        private static void StashPopModules(RuntimeDataStorage storage)
        {
            var modules = storage.GetValue<Dictionary<IntPtr, IntPtr>>("modules");
            var pyMoudles = PyImport_GetModuleDict();
            foreach (var item in modules)
            {
                int res = PyDict_SetItem(pyMoudles, item.Key, item.Value);
                PythonException.ThrowIfIsNotZero(res);
                XDecref(item.Key);
                XDecref(item.Value);
            }
            modules.Clear();
        }

        private static IFormatter CreateFormatter()
        {
            return FormatterType != null ?
                (IFormatter)Activator.CreateInstance(FormatterType)
                : new BinaryFormatter();
        }
    }


    [Serializable]
    public class RuntimeDataStorage
    {
        private Stack _stack;
        private Dictionary<string, object> _namedValues;

        public T AddValue<T>(string name, T value)
        {
            if (_namedValues == null)
            {
                _namedValues = new Dictionary<string, object>();
            }
            _namedValues.Add(name, value);
            return value;
        }

        public object GetValue(string name)
        {
            return _namedValues[name];
        }

        public T GetValue<T>(string name)
        {
            return (T)GetValue(name);
        }

        public RuntimeDataStorage GetStorage(string name)
        {
            return GetValue<RuntimeDataStorage>(name);
        }

        public T PushValue<T>(T value)
        {
            if (_stack == null)
            {
                _stack = new Stack();
            }
            _stack.Push(value);
            return value;
        }

        public object PopValue()
        {
            return _stack.Pop();
        }

        public T PopValue<T>()
        {
            return (T)PopValue();
        }
    }


    public class CLRMappedItem
    {
        public object Instance { get; private set; }
        public IList<IntPtr> Handles { get; set; }
        public bool Stored { get; set; }

        public CLRMappedItem(object instance)
        {
            Instance = instance;
        }
    }


    public interface ICLRObjectStorer
    {
        ICollection<CLRMappedItem> Store(CLRWrapperCollection wrappers, RuntimeDataStorage storage);
        CLRWrapperCollection Restore(RuntimeDataStorage storage);
    }


    public class CLRWrapperCollection : KeyedCollection<object, CLRMappedItem>
    {
        public bool TryGetValue(object key, out CLRMappedItem value)
        {
            if (Dictionary == null)
            {
                value = null;
                return false;
            }
            return Dictionary.TryGetValue(key, out value);
        }

        protected override object GetKeyForItem(CLRMappedItem item)
        {
            return item.Instance;
        }
    }
}
