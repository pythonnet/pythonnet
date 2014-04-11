; --------------------------------------------------------------------------------
; Setup script for Python for .NET (based on InnoSetup 5.0.8)
; --------------------------------------------------------------------------------

[Setup]

SourceDir=..
OutputDir=release

AppName=Python for .NET
AppVerName=Python for .NET 1.0 RC2
AppPublisher=Brian Lloyd
AppCopyright=Copyright © 2005 Zope Corporation
DefaultDirName={pf}\PythonNet
DefaultGroupName=Python for .NET
LicenseFile=installer\license.txt
DisableProgramGroupPage=yes
WizardImageFile=installer\left.bmp
WizardSmallImageFile=installer\top.bmp
WizardImageStretch=no


[Tasks]

Name: "existing"; Description: "Install .NET support in &existing python installation"; Flags: unchecked
Name: "icon"; Description: "Create a &desktop icon"; Flags: unchecked



[Files]

Source: "makefile"; DestDir: "{app}"; Excludes: ".svn*,~*,CVS*"; Flags: ignoreversion
Source: "python.exe"; DestDir: "{app}"; Excludes: ".svn*,~*,CVS*"; Flags: ignoreversion

Source: "*.dll"; DestDir: "{app}"; Excludes: ".svn*,~*,CVS*"; Flags: ignoreversion
Source: "demo\*.*"; DestDir: "{app}\demo"; Excludes: ".svn*,~*,CVS*"; Flags: ignoreversion recursesubdirs
Source: "doc\*.*"; DestDir: "{app}\doc"; Excludes: ".svn*,~*,CVS*"; Flags: ignoreversion recursesubdirs
Source: "src\*.*"; DestDir: "{app}\src"; Excludes: ".svn*,~*,CVS*"; Flags: ignoreversion recursesubdirs
Source: "redist\2.3\*.*"; DestDir: "{app}"; Excludes: ".svn*,~*,CVS*"; Flags: ignoreversion recursesubdirs
Source: "doc/readme.html"; DestDir: "{app}/doc"; Excludes: ".svn*,~*,CVS*"; Flags: ignoreversion isreadme

Source: "*Python.Runtime.dll"; DestDir: "{code:GetPythonDir}"; Excludes: ".svn*,~*,CVS*"; Flags: ignoreversion recursesubdirs; Check: UpdateExisting
Source: "CLR.dll"; DestDir: "{code:GetPythonDir}"; Excludes: ".svn*,~*,CVS*"; Flags: ignoreversion recursesubdirs; Check: UpdateExisting

[Icons]

Name: "{group}\Python for .NET"; Filename: "{app}\python.exe"
Name: "{userdesktop}\Python for .NET"; Filename: "{app}\python.exe"; Tasks: icon


[Code]

function GetPythonDir(Default: String): string;
var
    path : string;
begin
    path := '';
    RegQueryStringValue(HKLM, 'Software\Python\PythonCore\2.3\InstallPath', '', path);
    Result := path;
end;

function UpdateExisting(): boolean;
var
    temp: string;
    res: boolean;
begin
    temp := WizardSelectedTasks(False);
    res := (Pos('existing', temp) <> 0);
    temp := GetPythonDir('');
    Result := res and (Pos('Python', temp) <> 0);
end;


