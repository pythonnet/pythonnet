using System;

namespace Python.Runtime.Native
{
    [Flags]
    enum PyCompilerFlags
    {
        SOURCE_IS_UTF8 = 0x0100,
        DONT_IMPLY_DEDENT = 0x0200,
        ONLY_AST = 0x0400,
        IGNORE_COOKIE = 0x0800,
    }
}
