using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Python.Runtime
{
    [Serializable]
    [DebuggerDisplay("clrO: {inst}")]
    internal sealed class CLRObject : ManagedType
    {
        internal readonly object inst;

        internal static readonly ReflectedObjectsCollection reflectedObjects = new();

        static NewReference Create(object ob, BorrowedReference tp)
        {
            Debug.Assert(tp != null);
            var py = Runtime.PyType_GenericAlloc(tp, 0);

            var self = new CLRObject(ob);

            GCHandle gc = GCHandle.Alloc(self);
            InitGCHandle(py.Borrow(), type: tp, gc);

            bool isNew = reflectedObjects.Add(py.DangerousGetAddress());
            Debug.Assert(isNew);

            // Fix the BaseException args (and __cause__ in case of Python 3)
            // slot if wrapping a CLR exception
            if (ob is Exception e) Exceptions.SetArgsAndCause(py.Borrow(), e);

            return py;
        }

        CLRObject(object inst)
        {
            this.inst = inst;
        }

        internal static NewReference GetReference(object ob, BorrowedReference pyType)
            => Create(ob, pyType);

        internal static NewReference GetReference(object ob, Type type)
        {
            BorrowedReference cc = ClassManager.GetClass(type);
            return Create(ob, cc);
        }

        internal static NewReference GetReference(object ob)
        {
            BorrowedReference cc = ClassManager.GetClass(ob.GetType());
            return Create(ob, cc);
        }

        internal static void Restore(object ob, BorrowedReference pyHandle, Dictionary<string, object?> context)
        {
            var co = new CLRObject(ob);
            co.OnLoad(pyHandle, context);
        }

        protected override void OnLoad(BorrowedReference ob, Dictionary<string, object?>? context)
        {
            base.OnLoad(ob, context);
            GCHandle gc = GCHandle.Alloc(this);
            SetGCHandle(ob, gc);

            bool isNew = reflectedObjects.Add(ob.DangerousGetAddress());
            Debug.Assert(isNew);
        }

        internal sealed class ReflectedObjectsCollection : ISet<IntPtr>, IReadOnlyCollection<IntPtr>
        {
            readonly HashSet<IntPtr> objects = new();

            public bool Add(IntPtr item)
            {
                bool isNew = objects.Add(item);
                if (isNew) IncRef(item);
                return isNew;
            }

            public bool Remove(IntPtr item)
            {
                bool removed = objects.Remove(item);
                if (removed) DecRef(item);
                return removed;
            }

            public void Clear()
            {
                foreach (var item in objects)
                {
                    DecRef(item);
                }
                objects.Clear();
            }

            void IncRef(IntPtr item)
            {
                Runtime.Py_IncRef(new BorrowedReference(item));
            }

            void DecRef(IntPtr item)
            {
                Runtime.XDecref(StolenReference.DangerousFromPointer(item));
            }

            #region Implement interaces

            public int Count => objects.Count;

            public bool IsReadOnly => false;

            bool ISet<IntPtr>.Add(IntPtr item)
            {
                return Add(item);
            }

            public void ExceptWith(IEnumerable<IntPtr> other)
            {
                var copy = new HashSet<IntPtr>(objects);
                copy.IntersectWith(other);
                foreach (var item in copy) DecRef(item);
                objects.ExceptWith(other);
            }

            public void IntersectWith(IEnumerable<IntPtr> other)
            {
                var copy = new HashSet<IntPtr>(objects);
                copy.SymmetricExceptWith(other);
                copy.ExceptWith(other);
                foreach (var item in copy) DecRef(item);
                objects.IntersectWith(other);
            }

            public bool IsProperSubsetOf(IEnumerable<IntPtr> other)
            {
                return objects.IsProperSubsetOf(other);
            }

            public bool IsProperSupersetOf(IEnumerable<IntPtr> other)
            {
                return objects.IsProperSupersetOf(other);
            }

            public bool IsSubsetOf(IEnumerable<IntPtr> other)
            {
                return objects.IsSubsetOf(other);
            }

            public bool IsSupersetOf(IEnumerable<IntPtr> other)
            {
                return objects.IsSupersetOf(other);
            }

            public bool Overlaps(IEnumerable<IntPtr> other)
            {
                return objects.Overlaps(other);
            }

            public bool SetEquals(IEnumerable<IntPtr> other)
            {
                return objects.SetEquals(other);
            }

            public void SymmetricExceptWith(IEnumerable<IntPtr> other)
            {
                var copy = new HashSet<IntPtr>(objects);
                copy.IntersectWith(other);
                foreach (var item in copy) DecRef(item);
                var otherCopy = new HashSet<IntPtr>(other);
                otherCopy.ExceptWith(objects);
                foreach (var item in otherCopy) IncRef(item);
                objects.SymmetricExceptWith(other);
            }

            public void UnionWith(IEnumerable<IntPtr> other)
            {
                var otherCopy = new HashSet<IntPtr>(other);
                otherCopy.ExceptWith(objects);
                foreach (var item in otherCopy) IncRef(item);
                objects.UnionWith(other);
            }

            void ICollection<IntPtr>.Add(IntPtr item)
            {
                Add(item);
            }

            void ICollection<IntPtr>.Clear()
            {
                Clear();
            }

            public bool Contains(IntPtr item)
            {
                return objects.Contains(item);
            }

            public void CopyTo(IntPtr[] array, int arrayIndex)
            {
                // Responsibility for ref count at whoever handles the array.
                // No increment here.
                objects.CopyTo(array, arrayIndex);
            }

            bool ICollection<IntPtr>.Remove(IntPtr item)
            {
                return Remove(item);
            }

            public IEnumerator<IntPtr> GetEnumerator()
            {
                return objects.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return objects.GetEnumerator();
            }

            #endregion
        }
    }
}
