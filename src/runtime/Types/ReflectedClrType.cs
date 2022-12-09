using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;

using static Python.Runtime.PythonException;

namespace Python.Runtime;

[Serializable]
internal sealed class ReflectedClrType : PyType
{
    private ReflectedClrType(StolenReference reference) : base(reference, prevalidated: true) { }
    internal ReflectedClrType(ReflectedClrType original) : base(original, prevalidated: true) { }
    internal ReflectedClrType(BorrowedReference original) : base(original) { }
    ReflectedClrType(SerializationInfo info, StreamingContext context) : base(info, context) { }

    internal ClassBase Impl => (ClassBase)ManagedType.GetManagedObject(this)!;

    /// <summary>
    /// Get the Python type that reflects the given CLR type.
    /// </summary>
    /// <remarks>
    /// Returned <see cref="ReflectedClrType"/> might be partially initialized.
    /// </remarks>
    public static ReflectedClrType GetOrCreate(Type type)
    {
        if (ClassManager.cache.TryGetValue(type, out ReflectedClrType pyType))
        {
            return pyType;
        }

        pyType = AllocateClass(type);
        ClassManager.cache.Add(type, pyType);

        ClassBase impl = ClassManager.CreateClass(type);

        TypeManager.InitializeClassCore(type, pyType, impl);
        ClassManager.InitClassBase(type, impl, pyType);
        // Now we force initialize the Python type object to reflect the given
        // managed type, filling the Python type slots with thunks that
        // point to the managed methods providing the implementation.
        TypeManager.InitializeClass(pyType, impl, type);

        return pyType;
    }

    internal void Restore(Dictionary<string, object?> context)
    {
        var cb = (ClassBase)context["impl"]!;

        Debug.Assert(cb is not null);

        cb!.Load(this, context);

        Restore(cb);
    }

    internal void Restore(ClassBase cb)
    {
        ClassManager.InitClassBase(cb.type.Value, cb, this);

        TypeManager.InitializeClass(this, cb, cb.type.Value);
    }

    internal static NewReference CreateSubclass(ClassBase baseType, IEnumerable<Type> interfaces,
                                                string name, string? assembly, string? ns,
                                                BorrowedReference dict)
    {
        try
        {
            Type subType;

            subType = ClassDerivedObject.CreateDerivedType(name,
                baseType.type.Value,
                interfaces,
                dict,
                ns,
                assembly);

            ClassManager.cache.Remove(subType);
            ReflectedClrType pyTypeObj = GetOrCreate(subType);

            // by default the class dict will have all the C# methods in it, but as this is a
            // derived class we want the python overrides in there instead if they exist.
            var cls_dict = Util.ReadRef(pyTypeObj, TypeOffset.tp_dict);
            ThrowIfIsNotZero(Runtime.PyDict_Update(cls_dict, dict));
            // Update the __classcell__ if it exists
            BorrowedReference cell = Runtime.PyDict_GetItemString(cls_dict, "__classcell__");
            if (!cell.IsNull)
            {
                ThrowIfIsNotZero(Runtime.PyCell_Set(cell, pyTypeObj));
                ThrowIfIsNotZero(Runtime.PyDict_DelItemString(cls_dict, "__classcell__"));
            }

            return new NewReference(pyTypeObj);
        }
        catch (Exception e)
        {
            return Exceptions.RaiseTypeError(e.Message);
        }
    }

    static ReflectedClrType AllocateClass(Type clrType)
    {
        string name = TypeManager.GetPythonTypeName(clrType);

        var type = TypeManager.AllocateTypeObject(name, Runtime.PyCLRMetaType);
        type.Flags = TypeFlags.Default
                        | TypeFlags.HasClrInstance
                        | TypeFlags.HeapType
                        | TypeFlags.BaseType
                        | TypeFlags.HaveGC;

        return new ReflectedClrType(type.Steal());
    }

    public override bool Equals(PyObject? other) => rawPtr == other?.DangerousGetAddressOrNull();
    public override int GetHashCode() => rawPtr.GetHashCode();
}
