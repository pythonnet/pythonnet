using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;

using static Python.Runtime.Runtime;

namespace Python.Runtime
{
    class RuntimeData
    {
        internal static void Stash()
        {
            var formatter = new BinaryFormatter();
            var ms = new MemoryStream();
            var stack = new Stack();
            MetaType.StashPush(stack);
            TypeManager.StashPush(stack);
            ClassManager.StashPush(stack);
            var objs = ManagedType.GetManagedObjects();
            var saveObjs = new Dictionary<ManagedType, ManagedType.TrackTypes>();
            foreach (var entry in objs)
            {
                var obj = entry.Key;
                if (entry.Value == ManagedType.TrackTypes.Wrapper
                    && !obj.GetType().IsSerializable)
                {
                    // XXX: Skip non-serializable objects,
                    // use them after next initialization will raise exceptions.
                    continue;
                }
                Debug.Assert(obj.GetType().IsSerializable);
                obj.Save();
                saveObjs.Add(entry.Key, entry.Value);
            }
            stack.Push(saveObjs);
            formatter.Serialize(ms, stack);

            byte[] data = ms.GetBuffer();
            // TODO: use buffer api instead
            Debug.Assert(data.Length <= int.MaxValue);
            IntPtr mem = PyMem_Malloc(data.LongLength + IntPtr.Size);
            Marshal.WriteIntPtr(mem, (IntPtr)data.LongLength);
            Marshal.Copy(data, 0, mem + IntPtr.Size, data.Length);

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
            var formatter = new BinaryFormatter();

            var stack = (Stack)formatter.Deserialize(ms);

            var loadObjs = (IDictionary<ManagedType, ManagedType.TrackTypes>)stack.Pop();
            foreach (var entry in loadObjs)
            {
                ManagedType obj = entry.Key;
                obj.Load();
            }
            Debug.Assert(ManagedType.GetManagedObjects().Count == loadObjs.Count);
            ClassManager.StashPop(stack);
            TypeManager.StashPop(stack);
            PyCLRMetaType = MetaType.StashPop(stack);
        }

        public static bool HasStashData()
        {
            return PySys_GetObject("clr_data") != IntPtr.Zero;
        }

        public static void ClearStash()
        {
            PySys_SetObject("clr_data", IntPtr.Zero);
        }
    }
}
