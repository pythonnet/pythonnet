using System;

namespace Python.Runtime;

struct UnsafeReferenceWithRun
{
    public UnsafeReferenceWithRun(BorrowedReference pyObj)
    {
        RawObj = pyObj.DangerousGetAddressOrNull();
        Run = Runtime.GetRun();
    }

    public IntPtr RawObj;
    public BorrowedReference Ref => new(RawObj);
    public int Run;

    public BorrowedReference CheckRun()
    {
        if (Run != Runtime.GetRun())
            throw new RuntimeShutdownException(RawObj);

        return Ref;
    }
}
