using System;

namespace Python.Runtime;

/// <summary>
/// Base class for Python types that are dervided from types based on System.Exception
/// </summary>
[Serializable]
internal class ExceptionClassDerivedObject : ClassDerivedObject
{
    internal ExceptionClassDerivedObject(Type tp) : base(tp) { }

    internal static Exception? ToException(BorrowedReference ob) => ExceptionClassObject.ToException(ob);

    /// <summary>
    /// Exception __repr__ implementation
    /// </summary>
    public new static NewReference tp_repr(BorrowedReference ob) => ExceptionClassObject.tp_repr(ob);

    /// <summary>
    /// Exception __str__ implementation
    /// </summary>
    public new static NewReference tp_str(BorrowedReference ob) => ExceptionClassObject.tp_str(ob);

    public override bool Init(BorrowedReference obj, BorrowedReference args, BorrowedReference kw)
    {
        if (!base.Init(obj, args, kw)) return false;

        var e = (CLRObject)GetManagedObject(obj)!;
        return Exceptions.SetArgsAndCause(obj, (Exception)e.inst);
    }
}
