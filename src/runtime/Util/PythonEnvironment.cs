using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using static System.FormattableString;

namespace Python.Runtime;


internal class PythonEnvironment
{
    readonly static string PYDLL_ENV_VAR = "PYTHONNET_PYDLL";
    readonly static string PYEXE_ENV_VAR = "PYTHONNET_PYEXE";
    readonly static string PYNET_VENV_ENV_VAR = "PYTHONNET_VENV";
    readonly static string VENV_ENV_VAR = "VIRTUAL_ENV";

    public string? VenvPath { get; private set; }
    public string? Home { get; private set; }
    public Version? Version { get; private set; }
    public string? ProgramName { get; set; }
    public string? LibPython { get; set; }

    public bool IsValid =>
        !string.IsNullOrEmpty(ProgramName) && !string.IsNullOrEmpty(LibPython);


    // TODO: Move the lib-guessing step to separate function, use together with
    // PYTHONNET_PYEXE or a path lookup as last resort

    // Initialize PythonEnvironment instance from environment variables.
    //
    // If PYTHONNET_PYEXE and PYTHONNET_PYDLL are set, these always have precedence.
    // If PYTHONNET_VENV or VIRTUAL_ENV is set, we interpret the environment as a venv
    // and set the ProgramName/LibPython accordingly. PYTHONNET_VENV takes precedence.
    public static PythonEnvironment FromEnv()
    {
        var pydll = Environment.GetEnvironmentVariable(PYDLL_ENV_VAR);
        var pydllSet = !string.IsNullOrEmpty(pydll);
        var pyexe = Environment.GetEnvironmentVariable(PYEXE_ENV_VAR);
        var pyexeSet = !string.IsNullOrEmpty(pyexe);
        var pynetVenv = Environment.GetEnvironmentVariable(PYNET_VENV_ENV_VAR);
        var pynetVenvSet = !string.IsNullOrEmpty(pynetVenv);
        var venv = Environment.GetEnvironmentVariable(VENV_ENV_VAR);
        var venvSet = !string.IsNullOrEmpty(venv);

        PythonEnvironment? res = new();

        if (pynetVenvSet)
            res = FromVenv(pynetVenv) ?? res;
        else if (venvSet)
            res = FromVenv(venv) ?? res;

        if (pyexeSet)
            res.ProgramName = pyexe;

        if (pydllSet)
            res.LibPython = pydll;

        return res;
    }

    public static PythonEnvironment? FromVenv(string path)
    {
        var env = new PythonEnvironment
        {
            VenvPath = path
        };

        string venvCfg = Path.Combine(path, "pyvenv.cfg");

        if (!File.Exists(venvCfg))
            return null;

        var settings = TryParse(venvCfg);

        if (!settings.ContainsKey("home"))
            return null;

        env.Home = settings["home"];
        var pname = ProgramNameFromPath(path);
        if (File.Exists(pname))
            env.ProgramName = pname;

        if (settings.TryGetValue("version", out string versionStr))
        {
            _ = Version.TryParse(versionStr, out Version versionObj);
            env.Version = versionObj;
        }
        else if (settings.TryGetValue("version_info", out versionStr))
        {
            _ = Version.TryParse(versionStr, out Version versionObj);
            env.Version = versionObj;
        }

        env.LibPython = FindLibPython(env.Home, env.Version);

        return env;
    }

    private static Dictionary<string, string> TryParse(string venvCfg)
    {
        var settings = new Dictionary<string, string>();

        string[] lines = File.ReadAllLines(venvCfg);

        // The actually used format is really primitive: "<key> = <value>"
        foreach (string line in lines)
        {
            var split = line.Split(new[] { '=' }, 2);

            if (split.Length != 2)
                continue;

            settings[split[0].Trim()] = split[1].Trim();
        }

        return settings;
    }

    private static string? FindLibPython(string home, Version? maybeVersion)
    {
        // TODO: Check whether there is a .dll/.so/.dylib next to the executable

        if (maybeVersion is Version version)
        {
            return FindLibPythonInHome(home, version);
        }

        return null;
    }

    private static string? FindLibPythonInHome(string home, Version version)
    {
        var libPythonName = GetDefaultDllName(version);

        List<string> pathsToCheck = new();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var arch = RuntimeInformation.ProcessArchitecture;
            if (arch == Architecture.X64 || arch == Architecture.Arm64)
            {
                // multilib systems
                pathsToCheck.Add("../lib64");
            }
            pathsToCheck.Add("../lib");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            pathsToCheck.Add(".");
        }
        else
        {
            pathsToCheck.Add("../lib");
        }

        return pathsToCheck
            .Select(path => Path.Combine(home, path, libPythonName))
            .FirstOrDefault(File.Exists);
    }

    private static string ProgramNameFromPath(string path)
    {
        if (Runtime.IsWindows)
        {
            return Path.Combine(path, "Scripts", "python.exe");
        }
        else
        {
            return Path.Combine(path, "bin", "python");
        }
    }

    internal static string GetDefaultDllName(Version version)
    {
        string prefix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "" : "lib";

        string suffix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Invariant($"{version.Major}{version.Minor}")
            : Invariant($"{version.Major}.{version.Minor}");

        string ext = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".dll"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? ".dylib"
            : ".so";

        return prefix + "python" + suffix + ext;
    }
}
