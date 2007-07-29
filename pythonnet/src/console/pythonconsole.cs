// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using System;
using Python.Runtime;

namespace Python.Runtime {

public sealed class PythonConsole {

    private PythonConsole() {}

    [STAThread]
    public static int Main(string[] args) {
        string [] cmd = Environment.GetCommandLineArgs();
        PythonEngine.Initialize();

        int i = Runtime.Py_Main(cmd.Length, cmd);
        PythonEngine.Shutdown();

        return i;
    }

}

}
