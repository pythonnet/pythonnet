#!/usr/bin/env python
# -*- coding: utf-8 -*-

"""
Setup script for building clr.pyd and dependencies using mono and into
an egg or wheel.
"""

import fnmatch
import glob
import os
import platform
import subprocess
import sys
import sysconfig
from distutils import log, spawn
from distutils.command import build_ext, install_data, install_lib

from setuptools import Extension, setup

CONFIG = "Release"  # Release or Debug
VERBOSITY = "minimal"  # quiet, minimal, normal, detailed, diagnostic

DEVTOOLS = "MsDev" if sys.platform == "win32" else "Mono"
ARCH = "x64" if platform.architecture()[0] == "64bit" else "x86"


def _find_msbuild_tool(tool="msbuild.exe", use_windows_sdk=False):
    """Return full path to one of the Microsoft build tools"""
    path = spawn.find_executable(tool)
    if path:
        return path

    try:  # PY2
        import _winreg as winreg
    except ImportError:  # PY3
        import winreg

    keys_to_check = []
    if use_windows_sdk:
        sdks_root = r"SOFTWARE\Microsoft\Microsoft SDKs\Windows"
        kits_root = r"SOFTWARE\Microsoft\Windows Kits\Installed Roots"
        kits_suffix = os.path.join("bin", ARCH)
        keys_to_check.extend([
            ("Windows Kit 10.0", kits_root, "KitsRoot10", kits_suffix),
            ("Windows Kit 8.1", kits_root, "KitsRoot81", kits_suffix),
            ("Windows Kit 8.0", kits_root, "KitsRoot", kits_suffix),
            ("Windows SDK 7.1A", sdks_root + r"\v7.1A\WinSDK-Win32Tools", "InstallationFolder"),
            ("Windows SDK 7.1", sdks_root + r"\v7.1\WinSDKWin32Tools", "InstallationFolder"),
            ("Windows SDK 7.0A", sdks_root + r"\v7.0A\WinSDK-Win32Tools", "InstallationFolder"),
            ("Windows SDK 7.0", sdks_root + r"\v7.0\WinSDKWin32Tools", "InstallationFolder"),
            ("Windows SDK 6.0A", sdks_root + r"\v6.0A\WinSDKWin32Tools", "InstallationFolder")
        ])
    else:
        vs_root = r"SOFTWARE\Microsoft\MSBuild\ToolsVersions"
        keys_to_check.extend([
            ("MSBuild 14", vs_root + r"\14.0", "MSBuildToolsPath"),
            ("MSBuild 12", vs_root + r"\12.0", "MSBuildToolsPath"),
            ("MSBuild 4", vs_root + r"\4.0", "MSBuildToolsPath"),
            ("MSBuild 3.5", vs_root + r"\3.5", "MSBuildToolsPath"),
            ("MSBuild 2.0", vs_root + r"\2.0", "MSBuildToolsPath")
        ])

    # read the possible tools paths from the various registry locations
    paths_to_check = []
    hreg = winreg.ConnectRegistry(None, winreg.HKEY_LOCAL_MACHINE)
    try:
        for key_to_check in keys_to_check:
            sdk_name, key, value_name = key_to_check[:3]
            suffix = key_to_check[3] if len(key_to_check) > 3 else None
            hkey = None
            try:
                hkey = winreg.OpenKey(hreg, key)
                val, type_ = winreg.QueryValueEx(hkey, value_name)
                if type_ != winreg.REG_SZ:
                    continue
                if suffix:
                    val = os.path.join(val, suffix)
                paths_to_check.append((sdk_name, val))
            except WindowsError:
                pass
            finally:
                if hkey:
                    hkey.Close()
    finally:
        hreg.Close()

    # Add Visual C++ for Python as a fall-back in case one
    # of the other Windows SDKs isn't installed
    if use_windows_sdk:
        localappdata = os.environ["LOCALAPPDATA"]
        pywinsdk = localappdata + r"\Programs\Common\Microsoft\Visual C++ for Python\9.0\WinSDK\Bin"
        if ARCH == "x64":
            pywinsdk += r"\x64"
        paths_to_check.append(("Visual C++ for Python", pywinsdk))

    for sdk_name, path in paths_to_check:
        path = os.path.join(path, tool)
        if os.path.exists(path):
            log.info("Using %s from %s" % (tool, sdk_name))
            return path

    raise RuntimeError("%s could not be found" % tool)


class BuildExtPythonnet(build_ext.build_ext):
    def build_extension(self, ext):
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
        if sys.version_info[:2] <= (3, 2):
            unicode_width = 2 if sys.maxunicode < 0x10FFFF else 4
        else:
            import ctypes
            unicode_width = ctypes.sizeof(ctypes.c_wchar)

        defines = [
            "PYTHON%d%d" % (sys.version_info[:2]),
            "PYTHON%d" % (sys.version_info[:1]),  # Python Major Version
            "UCS%d" % unicode_width,
        ]

        if CONFIG == "Debug":
            defines.extend(["DEBUG", "TRACE"])

        if sys.platform != "win32" and DEVTOOLS == "Mono":
            if sys.platform == "darwin":
                defines.append("MONO_OSX")
            else:
                defines.append("MONO_LINUX")

            # Check if --enable-shared was set when Python was built
            enable_shared = sysconfig.get_config_var("Py_ENABLE_SHARED")
            if enable_shared:
                # Double-check if libpython is linked dynamically with python
                lddout = _check_output(["ldd", sys.executable])
                if 'libpython' not in lddout:
                    enable_shared = False

            if not enable_shared:
                defines.append("PYTHON_WITHOUT_ENABLE_SHARED")

        if hasattr(sys, "abiflags"):
            if "d" in sys.abiflags:
                defines.append("PYTHON_WITH_PYDEBUG")
            if "m" in sys.abiflags:
                defines.append("PYTHON_WITH_PYMALLOC")
            if "u" in sys.abiflags:
                defines.append("PYTHON_WITH_WIDE_UNICODE")

        # check the interop file exists, and create it if it doesn't
        interop_file = _get_interop_filename()
        if not os.path.exists(interop_file):
            geninterop = os.path.join("tools", "geninterop", "geninterop.py")
            subprocess.check_call([sys.executable, geninterop, interop_file])

        if DEVTOOLS == "MsDev":
            _xbuild = '"{0}"'.format(_find_msbuild_tool("msbuild.exe"))
            _defines_sep = ";"
            _config = "{0}Win".format(CONFIG)

        elif DEVTOOLS == "Mono":
            _xbuild = "xbuild"
            _defines_sep = ","
            _config = "{0}Mono".format(CONFIG)
        else:
            raise NotImplementedError(
                "DevTool {0} not supported (use MsDev/Mono)".format(DEVTOOLS))

        cmd = [
            _xbuild,
            "pythonnet.sln",
            "/p:Configuration=%s" % _config,
            "/p:Platform=%s" % ARCH,
            "/p:DefineConstants=\"%s\"" % _defines_sep.join(defines),
            "/p:PythonBuildDir=\"%s\"" % os.path.abspath(dest_dir),
            "/p:PythonInteropFile=\"%s\"" % os.path.basename(interop_file),
            "/verbosity:%s" % VERBOSITY,
        ]

        manifest = self._get_manifest(dest_dir)
        if manifest:
            cmd.append("/p:PythonManifest=\"%s\"" % manifest)

        self.announce("Building: %s" % " ".join(cmd))
        use_shell = True if DEVTOOLS == "Mono" else False
        subprocess.check_call(" ".join(cmd + ["/t:Clean"]), shell=use_shell)
        subprocess.check_call(" ".join(cmd + ["/t:Build"]), shell=use_shell)

        if DEVTOOLS == "Mono":
            self._build_monoclr(ext)

    def _get_manifest(self, build_dir):
        if DEVTOOLS == "MsDev" and sys.version_info[:2] > (2, 5):
            mt = _find_msbuild_tool("mt.exe", use_windows_sdk=True)
            manifest = os.path.abspath(os.path.join(build_dir, "app.manifest"))
            cmd = [mt, '-inputresource:"%s"' % sys.executable,
                   '-out:"%s"' % manifest]
            self.announce("Extracting manifest from %s" % sys.executable)
            subprocess.check_call(" ".join(cmd), shell=False)
            return manifest

    def _build_monoclr(self, ext):
        mono_libs = _check_output("pkg-config --libs mono-2", shell=True)
        mono_cflags = _check_output("pkg-config --cflags mono-2", shell=True)
        glib_libs = _check_output("pkg-config --libs glib-2.0", shell=True)
        glib_cflags = _check_output("pkg-config --cflags glib-2.0", shell=True)
        cflags = mono_cflags.strip() + " " + glib_cflags.strip()
        libs = mono_libs.strip() + " " + glib_libs.strip()

        # build the clr python module
        clr_ext = Extension("clr",
                            sources=[
                                "src/monoclr/pynetinit.c",
                                "src/monoclr/clrmod.c"
                            ],
                            extra_compile_args=cflags.split(" "),
                            extra_link_args=libs.split(" "))

        build_ext.build_ext.build_extension(self, clr_ext)

    def _install_packages(self):
        """install packages using nuget"""
        nuget = os.path.join("tools", "nuget", "nuget.exe")
        use_shell = False
        if DEVTOOLS == "Mono":
            nuget = "mono %s" % nuget
            use_shell = True

        cmd = "%s update -self" % nuget
        self.announce("Updating NuGet: %s" % cmd)
        subprocess.check_call(cmd, shell=use_shell)

        cmd = "%s restore pythonnet.sln -o packages" % nuget
        self.announce("Installing packages: %s" % cmd)
        subprocess.check_call(cmd, shell=use_shell)


class InstallLibPythonnet(install_lib.install_lib):
    def install(self):
        if not os.path.isdir(self.build_dir):
            self.warn("'%s' does not exist -- no Python modules to install" %
                      self.build_dir)
            return

        if not os.path.exists(self.install_dir):
            self.mkpath(self.install_dir)

        # only copy clr.pyd/.so
        for srcfile in glob.glob(os.path.join(self.build_dir, "clr.*")):
            destfile = os.path.join(
                self.install_dir, os.path.basename(srcfile))
            self.copy_file(srcfile, destfile)


class InstallDataPythonnet(install_data.install_data):
    def run(self):
        build_cmd = self.get_finalized_command("build_ext")
        install_cmd = self.get_finalized_command("install")
        build_lib = os.path.abspath(build_cmd.build_lib)
        install_platlib = os.path.relpath(
            install_cmd.install_platlib, self.install_dir)

        for i, data_files in enumerate(self.data_files):
            if isinstance(data_files, str):
                self.data_files[i] = data_files[i].format(build_lib=build_lib)
            else:
                for j, filename in enumerate(data_files[1]):
                    data_files[1][j] = filename.format(build_lib=build_lib)
                dest = data_files[0].format(install_platlib=install_platlib)
                self.data_files[i] = dest, data_files[1]

        return install_data.install_data.run(self)


def _check_output(*args, **kwargs):
    """Check output wrapper for py2/py3 compatibility"""
    output = subprocess.check_output(*args, **kwargs)
    if sys.version_info[0] > 2:
        return output.decode("ascii")
    return output


def _get_interop_filename():
    """interopXX.cs is auto-generated as part of the build.
    For common windows platforms pre-generated files are included
    as most windows users won't have Clang installed, which is
    required to generate the file.
    """
    interop_file = "interop%d%d%s.cs" % (sys.version_info[0], sys.version_info[1], getattr(sys, "abiflags", ""))
    return os.path.join("src", "runtime", interop_file)


if __name__ == "__main__":
    setupdir = os.path.dirname(__file__)
    if setupdir:
        os.chdir(setupdir)

    sources = []
    for ext in (".sln", ".snk", ".config"):
        sources.extend(glob.glob("*" + ext))

    for root, dirnames, filenames in os.walk("src"):
        for ext in (".cs", ".csproj", ".sln", ".snk", ".config", ".il",
                    ".py", ".c", ".h", ".ico"):
            for filename in fnmatch.filter(filenames, "*" + ext):
                sources.append(os.path.join(root, filename))

    for root, dirnames, filenames in os.walk("tools"):
        for ext in (".exe", ".py", ".c", ".h"):
            for filename in fnmatch.filter(filenames, "*" + ext):
                sources.append(os.path.join(root, filename))

    setup_requires = []
    interop_file = _get_interop_filename()
    if not os.path.exists(interop_file):
        setup_requires.append("pycparser")

    setup(
        name="pythonnet",
        version="2.2.2",
        description=".Net and Mono integration for Python",
        url='https://pythonnet.github.io/',
        license='MIT',
        author="The Python for .Net developers",
        classifiers=[
            'Development Status :: 5 - Production/Stable',
            'Intended Audience :: Developers',
            'License :: OSI Approved :: MIT License',
            'Programming Language :: C#',
            'Programming Language :: Python :: 2',
            'Programming Language :: Python :: 2.7',
            'Programming Language :: Python :: 3',
            'Programming Language :: Python :: 3.3',
            'Programming Language :: Python :: 3.4',
            'Programming Language :: Python :: 3.5',
            'Programming Language :: Python :: 3.6',
            'Operating System :: Microsoft :: Windows',
            'Operating System :: POSIX :: Linux',
            'Operating System :: MacOS :: MacOS X',
        ],
        ext_modules=[
            Extension("clr", sources=sources)
        ],
        data_files=[
            ("{install_platlib}", [
                "{build_lib}/Python.Runtime.dll",
                "Python.Runtime.dll.config"]),
        ],
        zip_safe=False,
        cmdclass={
            "build_ext": BuildExtPythonnet,
            "install_lib": InstallLibPythonnet,
            "install_data": InstallDataPythonnet,
        },
        setup_requires=setup_requires
    )
