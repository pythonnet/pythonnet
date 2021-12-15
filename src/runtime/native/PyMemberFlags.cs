using System;

namespace Python.Runtime.Native;

[Flags]
enum PyMemberFlags: int
{
    None = 0,
    ReadOnly = 1,
    ReadRestricted = 2,
    WriteRestricted = 4,
    Restricted = (ReadRestricted | WriteRestricted),
    AuditRead = ReadRestricted,
}
