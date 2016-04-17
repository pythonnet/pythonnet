
using System;
using System.Reflection;
using System.Security.Permissions;
using System.Runtime.InteropServices;
using System.Resources;

[assembly: System.Reflection.AssemblyProduct("Python for .NET")]
[assembly: System.Reflection.AssemblyVersion("2.4.2.7")]
[assembly: AssemblyTitleAttribute("Python Console")]
[assembly: AssemblyDefaultAliasAttribute("python.exe")]
[assembly: CLSCompliant(true)]
[assembly: ComVisible(false)]


[assembly:PermissionSetAttribute(SecurityAction.RequestMinimum,
                                 Name = "FullTrust")]
[assembly: AssemblyDescriptionAttribute("")]
[assembly: AssemblyCopyrightAttribute("Zope Public License, Version 2.0 (ZPL)")]
[assembly: AssemblyFileVersionAttribute("2.0.0.4")]
[assembly: NeutralResourcesLanguageAttribute("en")]
