// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using System;
using System.Reflection;
using System.Security.Permissions;
using System.Runtime.InteropServices;
using System.Resources;

[assembly: System.Reflection.AssemblyProduct("Python for .NET")]
[assembly: System.Reflection.AssemblyVersion("2.0.0.2")]
[assembly: AssemblyTitleAttribute("Python Console")]
[assembly: AssemblyDefaultAliasAttribute("python.exe")]
[assembly: CLSCompliant(true)]
[assembly: ComVisible(false)]


[assembly:PermissionSetAttribute(SecurityAction.RequestMinimum, 
                                 Name = "FullTrust")]
[assembly: AssemblyDescriptionAttribute("")]
[assembly: AssemblyCopyrightAttribute("Zope Public License, Version 2.0 (ZPL)")]
[assembly: AssemblyFileVersionAttribute("2.0.0.2")]
[assembly: NeutralResourcesLanguageAttribute("en")]
