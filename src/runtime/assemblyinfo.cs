using System;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly: AssemblyProduct("Python for .NET")]
[assembly: AssemblyVersion("4.0.0.1")]
[assembly: AssemblyDefaultAlias("Python.Runtime.dll")]
[assembly: CLSCompliant(true)]
[assembly: ComVisible(false)]
[assembly: AssemblyCopyright("MIT License")]
[assembly: AssemblyFileVersion("2.0.0.2")]
[assembly: NeutralResourcesLanguage("en")]

#if PYTHON27
[assembly: AssemblyTitle("Python.Runtime for Python 2.7")]
[assembly: AssemblyDescription("Python Runtime for Python 2.7")]
#endif
#if PYTHON33
[assembly: AssemblyTitle("Python.Runtime for Python 3.3")]
[assembly: AssemblyDescription("Python Runtime for Python 3.3")]
#endif
#if PYTHON34
[assembly: AssemblyTitle("Python.Runtime for Python 3.4")]
[assembly: AssemblyDescription("Python Runtime for Python 3.4")]
#endif
#if PYTHON35
[assembly: AssemblyTitle("Python.Runtime for Python 3.5")]
[assembly: AssemblyDescription("Python Runtime for Python 3.5")]
#endif
#if PYTHON36
[assembly: AssemblyTitle("Python.Runtime for Python 3.6")]
[assembly: AssemblyDescription("Python Runtime for Python 3.6")]
#endif
