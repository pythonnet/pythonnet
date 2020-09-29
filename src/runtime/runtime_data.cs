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

        /// <summary>
        /// Clears the old "clr_data" entry if a previous one is present.
        /// </summary>
        static void ClearCLRData ()
        {
            BorrowedReference capsule = PySys_GetObject("clr_data");
            if (!capsule.IsNull)
            {
                IntPtr oldData = PyCapsule_GetPointer(capsule, null);
                PyMem_Free(oldData);
                PyCapsule_SetPointer(capsule, IntPtr.Zero);
            }
        }

        internal static void Stash()
        {
            var metaStorage = new RuntimeDataStorage();
            MetaType.SaveRuntimeData(metaStorage);

            var importStorage = new RuntimeDataStorage();
            ImportHook.SaveRuntimeData(importStorage);

            var typeStorage = new RuntimeDataStorage();
            TypeManager.SaveRuntimeData(typeStorage);

            var clsStorage = new RuntimeDataStorage();
            ClassManager.SaveRuntimeData(clsStorage);

            var moduleStorage = new RuntimeDataStorage();
            SaveRuntimeDataModules(moduleStorage);

            var objStorage = new RuntimeDataStorage();
            SaveRuntimeDataObjects(objStorage);

            var runtimeStorage = new RuntimeDataStorage();
            runtimeStorage.AddValue("meta", metaStorage);
            runtimeStorage.AddValue("import", importStorage);
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

            ClearCLRData();
            NewReference capsule = PyCapsule_New(mem, null, IntPtr.Zero);
            PySys_SetObject("clr_data", capsule.DangerousGetAddress());
            // Let the dictionary own the reference
            capsule.Dispose();
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
            IntPtr mem = PyCapsule_GetPointer(capsule, null);
            int length = (int)Marshal.ReadIntPtr(mem);
            byte[] data = new byte[length];
            Marshal.Copy(mem + IntPtr.Size, data, 0, length);
            var ms = new MemoryStream(data);
            var formatter = CreateFormatter();
            var storage = (RuntimeDataStorage)formatter.Deserialize(ms);

            var objs = RestoreRuntimeDataObjects(storage.GetStorage("objs"));
            RestoreRuntimeDataModules(storage.GetStorage("modules"));
            var clsObjs = ClassManager.RestoreRuntimeData(storage.GetStorage("classes"));
            TypeManager.RestoreRuntimeData(storage.GetStorage("types"));
            ImportHook.RestoreRuntimeData(storage.GetStorage("import"));
            PyCLRMetaType = MetaType.RestoreRuntimeData(storage.GetStorage("meta"));

            foreach (var item in objs)
            {
                item.Value.ExecutePostActions();
                XDecref(item.Key.pyHandle);
            }
            foreach (var item in clsObjs)
            {
                item.Value.ExecutePostActions();
            }
        }

        public static bool HasStashData()
        {
            return !PySys_GetObject("clr_data").IsNull;
        }

        public static void ClearStash()
        {
            PySys_SetObject("clr_data", IntPtr.Zero);
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

        private static void SaveRuntimeDataObjects(RuntimeDataStorage storage)
        {
            var objs = ManagedType.GetManagedObjects();
            var extensionObjs = new List<ManagedType>();
            var wrappers = new Dictionary<object, List<CLRObject>>();
            var serializeObjs = new CLRWrapperCollection();
            var contexts = new Dictionary<IntPtr, InterDomainContext>();
            foreach (var entry in objs)
            {
                var obj = entry.Key;
                XIncref(obj.pyHandle);
                switch (entry.Value)
                {
                    case ManagedType.TrackTypes.Extension:
                        Debug.Assert(CheckSerializable(obj));
                        var context = new InterDomainContext();
                        contexts[obj.pyHandle] = context;
                        obj.Save(context);
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
                    if (!CheckSerializable(item.Instance))
                    {
                        continue;
                    }
                    internalStores.AddRange(wrappers[item.Instance]);
                }
                foreach (var clrObj in wrappers[item.Instance])
                {
                    XIncref(clrObj.pyHandle);
                    var context = new InterDomainContext();
                    contexts[clrObj.pyHandle] = context;
                    clrObj.Save(context);
                }
            }
            storage.AddValue("internalStores", internalStores);
            storage.AddValue("extensions", extensionObjs);
            storage.AddValue("wrappers", wrapperStorage);
            storage.AddValue("contexts", contexts);
        }

        private static Dictionary<ManagedType, InterDomainContext> RestoreRuntimeDataObjects(RuntimeDataStorage storage)
        {
            var extensions = storage.GetValue<List<ManagedType>>("extensions");
            var internalStores = storage.GetValue<List<CLRObject>>("internalStores");
            var contexts = storage.GetValue <Dictionary<IntPtr, InterDomainContext>>("contexts");
            var storedObjs = new Dictionary<ManagedType, InterDomainContext>();
            foreach (var obj in Enumerable.Union(extensions, internalStores))
            {
                var context = contexts[obj.pyHandle];
                obj.Load(context);
                storedObjs.Add(obj, context);
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
                        var context = contexts[handle];
                        var co = CLRObject.Restore(obj, handle, context);
                        storedObjs.Add(co, context);
                    }
                }
            }
            return storedObjs;
        }

        private static void SaveRuntimeDataModules(RuntimeDataStorage storage)
        {
            var pyModules = PyImport_GetModuleDict();
            var items = PyDict_Items(pyModules);
            long length = PyList_Size(items);
            var modules = new Dictionary<IntPtr, IntPtr>(); ;
            for (long i = 0; i < length; i++)
            {
                var item = PyList_GetItem(items, i);
                var name = PyTuple_GetItem(item.DangerousGetAddress(), 0);
                var module = PyTuple_GetItem(item.DangerousGetAddress(), 1);
                if (ManagedType.IsManagedType(module))
                {
                    XIncref(name);
                    XIncref(module);
                    modules.Add(name, module);
                }
            }
            items.Dispose();
            storage.AddValue("modules", modules);
        }

        private static void RestoreRuntimeDataModules(RuntimeDataStorage storage)
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

        public T GetValue<T>(string name, out T value)
        {
            value = GetValue<T>(name);
            return value;
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

        public T PopValue<T>(out T value)
        {
            return value = (T)PopValue();
        }
    }


    [Serializable]
    class InterDomainContext
    {
        private RuntimeDataStorage _storage;
        public RuntimeDataStorage Storage => _storage ?? (_storage = new RuntimeDataStorage());

        /// <summary>
        /// Actions after loaded.
        /// </summary>
        [NonSerialized]
        private List<Action> _postActions;
        public List<Action> PostActions => _postActions ?? (_postActions = new List<Action>());

        public void AddPostAction(Action action)
        {
            PostActions.Add(action);
        }

        public void ExecutePostActions()
        {
            if (_postActions == null)
            {
                return;
            }
            foreach (var action in _postActions)
            {
                action();
            }
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
