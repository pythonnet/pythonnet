#!/usr/bin/env python
# -*- coding: utf-8 -*-

"""
Setup script for building clr.pyd and dependencies using mono and into
an egg or wheel.
"""

import collections
import fnmatch
import glob
import os
import subprocess
import sys
import sysconfig
from distutils import spawn
from distutils.command import install, build, build_ext, install_data, install_lib
from wheel import bdist_wheel

from setuptools import Extension, setup

# Allow config/verbosity to be set from cli
# http://stackoverflow.com/a/4792601/5208670
CONFIG = "Release"  # Release or Debug
VERBOSITY = "normal"  # quiet, minimal, normal, detailed, diagnostic

is_64bits = sys.maxsize > 2 ** 32
DEVTOOLS = "MsDev" if sys.platform == "win32" else "Mono"
ARCH = "x64" if is_64bits else "x86"
PY_MAJOR = sys.version_info[0]
PY_MINOR = sys.version_info[1]

###############################################################################
# Windows Keys Constants for MSBUILD tools
RegKey = collections.namedtuple("RegKey", "sdk_name key value_name suffix")
vs_python = "Programs\\Common\\Microsoft\\Visual C++ for Python\\9.0\\WinSDK"
vs_root = "SOFTWARE\\Microsoft\\MSBuild\\ToolsVersions\\{0}"
sdks_root = "SOFTWARE\\Microsoft\\Microsoft SDKs\\Windows\\v{0}Win32Tools"
kits_root = "SOFTWARE\\Microsoft\\Windows Kits\\Installed Roots"
kits_suffix = os.path.join("bin", ARCH)

WIN_SDK_KEYS = [
    RegKey(
        sdk_name="Windows Kit 10.0",
        key=kits_root,
        value_name="KitsRoot10",
        suffix=os.path.join("bin", "10.0.16299.0", ARCH),
    ),
    RegKey(
        sdk_name="Windows Kit 10.0",
        key=kits_root,
        value_name="KitsRoot10",
        suffix=os.path.join("bin", "10.0.15063.0", ARCH),
    ),
    RegKey(
        sdk_name="Windows Kit 10.0",
        key=kits_root,
        value_name="KitsRoot10",
        suffix=kits_suffix,
    ),
    RegKey(
        sdk_name="Windows Kit 8.1",
        key=kits_root,
        value_name="KitsRoot81",
        suffix=kits_suffix,
    ),
    RegKey(
        sdk_name="Windows Kit 8.0",
        key=kits_root,
        value_name="KitsRoot",
        suffix=kits_suffix,
    ),
    RegKey(
        sdk_name="Windows SDK 7.1A",
        key=sdks_root.format("7.1A\\WinSDK-"),
        value_name="InstallationFolder",
        suffix="",
    ),
    RegKey(
        sdk_name="Windows SDK 7.1",
        key=sdks_root.format("7.1\\WinSDK"),
        value_name="InstallationFolder",
        suffix="",
    ),
    RegKey(
        sdk_name="Windows SDK 7.0A",
        key=sdks_root.format("7.0A\\WinSDK-"),
        value_name="InstallationFolder",
        suffix="",
    ),
    RegKey(
        sdk_name="Windows SDK 7.0",
        key=sdks_root.format("7.0\\WinSDK"),
        value_name="InstallationFolder",
        suffix="",
    ),
    RegKey(
        sdk_name="Windows SDK 6.0A",
        key=sdks_root.format("6.0A\\WinSDK"),
        value_name="InstallationFolder",
        suffix="",
    ),
]

VS_KEYS = (
    RegKey(
        sdk_name="MSBuild 15",
        key=vs_root.format("15.0"),
        value_name="MSBuildToolsPath",
        suffix="",
    ),
    RegKey(
        sdk_name="MSBuild 14",
        key=vs_root.format("14.0"),
        value_name="MSBuildToolsPath",
        suffix="",
    ),
    RegKey(
        sdk_name="MSBuild 12",
        key=vs_root.format("12.0"),
        value_name="MSBuildToolsPath",
        suffix="",
    ),
    RegKey(
        sdk_name="MSBuild 4",
        key=vs_root.format("4.0"),
        value_name="MSBuildToolsPath",
        suffix="",
    ),
    RegKey(
        sdk_name="MSBuild 3.5",
        key=vs_root.format("3.5"),
        value_name="MSBuildToolsPath",
        suffix="",
    ),
    RegKey(
        sdk_name="MSBuild 2.0",
        key=vs_root.format("2.0"),
        value_name="MSBuildToolsPath",
        suffix="",
    ),
)


###############################################################################
def _check_output(*args, **kwargs):
    """Check output wrapper for py2/py3 compatibility"""
    output = subprocess.check_output(*args, **kwargs)
    if PY_MAJOR == 2:
        return output
    return output.decode("ascii")


def _get_interop_filename():
    """interopXX.cs is auto-generated as part of the build.
    For common windows platforms pre-generated files are included
    as most windows users won't have Clang installed, which is
    required to generate the file.
    """
    interop_filename = "interop{0}{1}{2}.cs".format(
        PY_MAJOR, PY_MINOR, getattr(sys, "abiflags", "")
    )
    return os.path.join("src", "runtime", interop_filename)


def _get_source_files():
    """Walk project and collect the files needed for ext_module"""
    for ext in (".sln",):
        for path in glob.glob("*" + ext):
            yield path

    for root, dirnames, filenames in os.walk("src"):
        for ext in (".cs", ".csproj", ".snk", ".config", ".py", ".c", ".h", ".ico"):
            for filename in fnmatch.filter(filenames, "*" + ext):
                yield os.path.join(root, filename)

    for root, dirnames, filenames in os.walk("tools"):
        for ext in (".exe", ".py", ".c", ".h"):
            for filename in fnmatch.filter(filenames, "*" + ext):
                yield os.path.join(root, filename)


def _get_long_description():
    """Helper to populate long_description for pypi releases"""
    return open("README.rst").read()


def _update_xlat_devtools():
    global DEVTOOLS
    if DEVTOOLS == "MsDev":
        DEVTOOLS = "MsDev15"
    elif DEVTOOLS == "Mono":
        DEVTOOLS = "dotnet"


def _collect_installed_windows_kits_v10(winreg):
    """Adds the installed Windows 10 kits to WIN_SDK_KEYS """
    global WIN_SDK_KEYS
    installed_kits = []

    with winreg.OpenKey(
        winreg.HKEY_LOCAL_MACHINE, kits_root, 0, winreg.KEY_READ
    ) as key:
        i = 0
        while True:
            try:
                installed_kits.append(winreg.EnumKey(key, i))
                i += 1
            except WindowsError:
                break

    def make_reg_key(version):
        return RegKey(
            sdk_name="Windows Kit 10.0",
            key=kits_root,
            value_name="KitsRoot10",
            suffix=os.path.join("bin", version, ARCH),
        )

    WIN_SDK_KEYS += [make_reg_key(e) for e in installed_kits if e.startswith("10.")]

    # Make sure this function won't be called again
    _collect_installed_windows_kits_v10 = lambda: None


class BuildExtPythonnet(build_ext.build_ext):
    user_options = build_ext.build_ext.user_options + [("xplat", None, None)]

    def initialize_options(self):
        build_ext.build_ext.initialize_options(self)
        self.xplat = None

    def finalize_options(self):
        build_ext.build_ext.finalize_options(self)

    def build_extension(self, ext):
        if self.xplat:
            _update_xlat_devtools()

        """Builds the .pyd file using msbuild or xbuild"""
        if ext.name != "clr":
            return build_ext.build_ext.build_extension(self, ext)

        # install packages using nuget
        self._install_packages()

        dest_file = self.get_ext_fullpath(ext.name)
        dest_dir = os.path.dirname(dest_file)
        if not os.path.exists(dest_dir):
            os.makedirs(dest_dir)

        # Up to Python 3.2 sys.maxunicode is used to determine the size of
        # Py_UNICODE, but from 3.3 onwards Py_UNICODE is a typedef of wchar_t.
        # TODO: Is this doing the right check for Py27?
        if sys.version_info[:2] <= (3, 2):
            unicode_width = 2 if sys.maxunicode < 0x10FFFF else 4
        else:
            import ctypes

            unicode_width = ctypes.sizeof(ctypes.c_wchar)

        defines = [
            "PYTHON{0}{1}".format(PY_MAJOR, PY_MINOR),
            "PYTHON{0}".format(PY_MAJOR),  # Python Major Version
            "UCS{0}".format(unicode_width),
        ]

        if CONFIG == "Debug":
            defines.extend(["DEBUG", "TRACE"])

        if sys.platform != "win32" and (DEVTOOLS == "Mono" or DEVTOOLS == "dotnet"):
            on_darwin = sys.platform == "darwin"
            defines.append("MONO_OSX" if on_darwin else "MONO_LINUX")

            # Check if --enable-shared was set when Python was built
            enable_shared = sysconfig.get_config_var("Py_ENABLE_SHARED")
            if enable_shared:
                # Double-check if libpython is linked dynamically with python
                ldd_cmd = ["otool", "-L"] if on_darwin else ["ldd"]
                lddout = _check_output(ldd_cmd + [sys.executable])
                if "libpython" not in lddout:
                    enable_shared = False

            if not enable_shared:
                defines.append("PYTHON_WITHOUT_ENABLE_SHARED")

        if hasattr(sys, "abiflags"):
            if "d" in sys.abiflags:
                defines.append("PYTHON_WITH_PYDEBUG")
            if "m" in sys.abiflags:
                defines.append("PYTHON_WITH_PYMALLOC")

        # check the interop file exists, and create it if it doesn't
        interop_file = _get_interop_filename()
        if not os.path.exists(interop_file):
            self.debug_print("Creating {0}".format(interop_file))
            geninterop = os.path.join("tools", "geninterop", "geninterop.py")
            subprocess.check_call([sys.executable, geninterop, interop_file])

        if DEVTOOLS == "MsDev":
            _xbuild = '"{0}"'.format(self._find_msbuild_tool("msbuild.exe"))
            _config = "{0}Win".format(CONFIG)
            _solution_file = "pythonnet.sln"
            _custom_define_constants = False
        elif DEVTOOLS == "MsDev15":
            _xbuild = '"{0}"'.format(self._find_msbuild_tool_15())
            _config = "{0}Win".format(CONFIG)
            _solution_file = "pythonnet.15.sln"
            _custom_define_constants = True
        elif DEVTOOLS == "Mono":
            _xbuild = "xbuild"
            _config = "{0}Mono".format(CONFIG)
            _solution_file = "pythonnet.sln"
            _custom_define_constants = False
        elif DEVTOOLS == "dotnet":
            _xbuild = "dotnet msbuild"
            _config = "{0}Mono".format(CONFIG)
            _solution_file = "pythonnet.15.sln"
            _custom_define_constants = True
        else:
            raise NotImplementedError(
                "DevTool {0} not supported (use MsDev/MsDev15/Mono/dotnet)".format(
                    DEVTOOLS
                )
            )

        cmd = [
            _xbuild,
            _solution_file,
            "/p:Configuration={}".format(_config),
            "/p:Platform={}".format(ARCH),
            '/p:{}DefineConstants="{}"'.format(
                "Custom" if _custom_define_constants else "", "%3B".join(defines)
            ),
            '/p:PythonBuildDir="{}"'.format(os.path.abspath(dest_dir)),
            '/p:PythonInteropFile="{}"'.format(os.path.basename(interop_file)),
            "/verbosity:{}".format(VERBOSITY),
        ]

        manifest = self._get_manifest(dest_dir)
        if manifest:
            cmd.append('/p:PythonManifest="{0}"'.format(manifest))

        self.debug_print("Building: {0}".format(" ".join(cmd)))
        use_shell = True if DEVTOOLS == "Mono" or DEVTOOLS == "dotnet" else False

        subprocess.check_call(" ".join(cmd + ["/t:Clean"]), shell=use_shell)
        subprocess.check_call(" ".join(cmd + ["/t:Build"]), shell=use_shell)
        if DEVTOOLS == "MsDev15" or DEVTOOLS == "dotnet":
            subprocess.check_call(
                " ".join(
                    cmd
                    + [
                        '"/t:Console_15:publish;Python_EmbeddingTest_15:publish"',
                        "/p:TargetFramework=netcoreapp2.0",
                    ]
                ),
                shell=use_shell,
            )
        if DEVTOOLS == "Mono" or DEVTOOLS == "dotnet":
            self._build_monoclr()

    def _get_manifest(self, build_dir):
        if DEVTOOLS != "MsDev" and DEVTOOLS != "MsDev15":
            return
        mt = self._find_msbuild_tool("mt.exe", use_windows_sdk=True)
        manifest = os.path.abspath(os.path.join(build_dir, "app.manifest"))
        cmd = [
            mt,
            '-inputresource:"{0}"'.format(sys.executable),
            '-out:"{0}"'.format(manifest),
        ]
        self.debug_print("Extracting manifest from {}".format(sys.executable))
        subprocess.check_call(" ".join(cmd), shell=False)
        return manifest

    def _build_monoclr(self):
        try:
            mono_libs = _check_output("pkg-config --libs mono-2", shell=True)
        except:
            if DEVTOOLS == "dotnet":
                print("Skipping building monoclr module...")
                return
            raise
        mono_cflags = _check_output("pkg-config --cflags mono-2", shell=True)
        glib_libs = _check_output("pkg-config --libs glib-2.0", shell=True)
        glib_cflags = _check_output("pkg-config --cflags glib-2.0", shell=True)
        cflags = mono_cflags.strip() + " " + glib_cflags.strip()
        libs = mono_libs.strip() + " " + glib_libs.strip()

        # build the clr python module
        clr_ext = Extension(
            "clr",
            sources=["src/monoclr/pynetinit.c", "src/monoclr/clrmod.c"],
            extra_compile_args=cflags.split(" "),
            extra_link_args=libs.split(" "),
        )

        build_ext.build_ext.build_extension(self, clr_ext)

    def _install_packages(self):
        """install packages using nuget"""
        use_shell = DEVTOOLS == "Mono" or DEVTOOLS == "dotnet"

        if DEVTOOLS == "MsDev15" or DEVTOOLS == "dotnet":
            if DEVTOOLS == "MsDev15":
                _config = "{0}Win".format(CONFIG)
            elif DEVTOOLS == "dotnet":
                _config = "{0}Mono".format(CONFIG)

            cmd = "dotnet msbuild /t:Restore pythonnet.15.sln /p:Configuration={0} /p:Platform={1}".format(
                _config, ARCH
            )
            self.debug_print("Updating packages with xplat: {0}".format(cmd))
            subprocess.check_call(cmd, shell=use_shell)
        else:
            nuget = os.path.join("tools", "nuget", "nuget.exe")

            if DEVTOOLS == "Mono":
                nuget = "mono {0}".format(nuget)

            cmd = "{0} update -self".format(nuget)
            self.debug_print("Updating NuGet: {0}".format(cmd))
            subprocess.check_call(cmd, shell=use_shell)

            try:
                # msbuild=14 is mainly for Mono issues
                cmd = "{0} restore pythonnet.sln  -MSBuildVersion 14 -o packages".format(
                    nuget
                )
                self.debug_print("Installing packages: {0}".format(cmd))
                subprocess.check_call(cmd, shell=use_shell)
            except:
                # when only VS 2017 is installed do not specify msbuild version
                cmd = "{0} restore pythonnet.sln  -o packages".format(nuget)
                self.debug_print("Installing packages: {0}".format(cmd))
                subprocess.check_call(cmd, shell=use_shell)

    def _find_msbuild_tool(self, tool="msbuild.exe", use_windows_sdk=False):
        """Return full path to one of the Microsoft build tools"""

        # trying to search path with help of vswhere when MSBuild 15.0 and higher installed.
        if tool == "msbuild.exe" and use_windows_sdk == False:
            try:
                basePathes = subprocess.check_output(
                    [
                        "tools\\vswhere\\vswhere.exe",
                        "-latest",
                        "-version",
                        "[15.0, 16.0)",
                        "-requires",
                        "Microsoft.Component.MSBuild",
                        "-property",
                        "InstallationPath",
                    ]
                ).splitlines()
                if len(basePathes):
                    return os.path.join(
                        basePathes[0].decode(sys.stdout.encoding or "utf-8"),
                        "MSBuild",
                        "15.0",
                        "Bin",
                        "MSBuild.exe",
                    )
            except:
                pass  # keep trying to search by old method.

        # Search in PATH first
        path = spawn.find_executable(tool)
        if path:
            return path

        # Search within registry to find build tools
        try:  # PY2
            import _winreg as winreg
        except ImportError:  # PY3
            import winreg

        _collect_installed_windows_kits_v10(winreg)

        keys_to_check = WIN_SDK_KEYS if use_windows_sdk else VS_KEYS
        hklm = winreg.HKEY_LOCAL_MACHINE
        for rkey in keys_to_check:
            try:
                with winreg.OpenKey(hklm, rkey.key) as hkey:
                    val, type_ = winreg.QueryValueEx(hkey, rkey.value_name)
                    if type_ != winreg.REG_SZ:
                        continue
                    path = os.path.join(val, rkey.suffix, tool)
                    if os.path.exists(path):
                        self.debug_print(
                            "Using {0} from {1}".format(tool, rkey.sdk_name)
                        )
                        return path
            except WindowsError:
                # Key doesn't exist
                pass

        # Add Visual C++ for Python as a fall-back in case one
        # of the other Windows SDKs isn't installed.
        # TODO: Extend checking by using setuptools/msvc.py?
        if use_windows_sdk:
            sdk_name = "Visual C++ for Python"
            localappdata = os.environ["LOCALAPPDATA"]
            suffix = "Bin\\x64" if ARCH == "x64" else "Bin"
            path = os.path.join(localappdata, vs_python, suffix, tool)
            if os.path.exists(path):
                self.debug_print("Using {0} from {1}".format(tool, sdk_name))
                return path

        raise RuntimeError("{0} could not be found".format(tool))

    def _find_msbuild_tool_15(self):
        """Return full path to one of the Microsoft build tools"""
        try:
            basePathes = subprocess.check_output(
                [
                    "tools\\vswhere\\vswhere.exe",
                    "-latest",
                    "-version",
                    "[15.0, 16.0)",
                    "-requires",
                    "Microsoft.Component.MSBuild",
                    "-property",
                    "InstallationPath",
                ]
            ).splitlines()
            if len(basePathes):
                return os.path.join(
                    basePathes[0].decode(sys.stdout.encoding or "utf-8"),
                    "MSBuild",
                    "15.0",
                    "Bin",
                    "MSBuild.exe",
                )
            else:
                raise RuntimeError("MSBuild >=15.0 could not be found.")
        except subprocess.CalledProcessError as e:
            raise RuntimeError(
                "MSBuild >=15.0 could not be found. {0}".format(e.output)
            )


class InstallLibPythonnet(install_lib.install_lib):
    def install(self):
        if not os.path.isdir(self.build_dir):
            self.warn(
                "'{0}' does not exist -- no Python modules"
                " to install".format(self.build_dir)
            )
            return

        if not os.path.exists(self.install_dir):
            self.mkpath(self.install_dir)

        # only copy clr.pyd/.so
        for srcfile in glob.glob(os.path.join(self.build_dir, "clr.*")):
            destfile = os.path.join(self.install_dir, os.path.basename(srcfile))
            self.copy_file(srcfile, destfile)


class InstallDataPythonnet(install_data.install_data):
    def run(self):
        build_cmd = self.get_finalized_command("build_ext")
        install_cmd = self.get_finalized_command("install")
        build_lib = os.path.abspath(build_cmd.build_lib)
        install_platlib = os.path.relpath(install_cmd.install_platlib, self.install_dir)

        for i, data_files in enumerate(self.data_files):
            if isinstance(data_files, str):
                self.data_files[i] = data_files[i].format(build_lib=build_lib)
            else:
                for j, filename in enumerate(data_files[1]):
                    data_files[1][j] = filename.format(build_lib=build_lib)
                dest = data_files[0].format(install_platlib=install_platlib)
                self.data_files[i] = dest, data_files[1]

        return install_data.install_data.run(self)


class InstallPythonnet(install.install):
    user_options = install.install.user_options + [("xplat", None, None)]

    def initialize_options(self):
        install.install.initialize_options(self)
        self.xplat = None

    def finalize_options(self):
        install.install.finalize_options(self)

    def run(self):
        if self.xplat:
            _update_xlat_devtools()
        return install.install.run(self)


class BDistWheelPythonnet(bdist_wheel.bdist_wheel):
    user_options = bdist_wheel.bdist_wheel.user_options + [("xplat", None, None)]

    def initialize_options(self):
        bdist_wheel.bdist_wheel.initialize_options(self)
        self.xplat = None

    def finalize_options(self):
        bdist_wheel.bdist_wheel.finalize_options(self)

    def run(self):
        if self.xplat:
            _update_xlat_devtools()
        return bdist_wheel.bdist_wheel.run(self)

        ###############################################################################


setupdir = os.path.dirname(__file__)
if setupdir:
    os.chdir(setupdir)

setup_requires = []
if not os.path.exists(_get_interop_filename()):
    setup_requires.append("pycparser")

setup(
    name="pythonnet",
    version="2.4.1-dev",
    description=".Net and Mono integration for Python",
    url="https://pythonnet.github.io/",
    license="MIT",
    author="The Python for .Net developers",
    author_email="pythondotnet@python.org",
    setup_requires=setup_requires,
    long_description=_get_long_description(),
    ext_modules=[Extension("clr", sources=list(_get_source_files()))],
    data_files=[("{install_platlib}", ["{build_lib}/Python.Runtime.dll"])],
    cmdclass={
        "install": InstallPythonnet,
        "build_ext": BuildExtPythonnet,
        "install_lib": InstallLibPythonnet,
        "install_data": InstallDataPythonnet,
        "bdist_wheel": BDistWheelPythonnet,
    },
    classifiers=[
        "Development Status :: 5 - Production/Stable",
        "Intended Audience :: Developers",
        "License :: OSI Approved :: MIT License",
        "Programming Language :: C#",
        "Programming Language :: Python :: 2",
        "Programming Language :: Python :: 2.7",
        "Programming Language :: Python :: 3",
        "Programming Language :: Python :: 3.5",
        "Programming Language :: Python :: 3.6",
        "Programming Language :: Python :: 3.7",
        "Operating System :: Microsoft :: Windows",
        "Operating System :: POSIX :: Linux",
        "Operating System :: MacOS :: MacOS X",
    ],
    zip_safe=False,
)
