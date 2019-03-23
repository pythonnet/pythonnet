#!/usr/bin/env python
# -*- coding: utf-8 -*-

"""
Setup script for building clr.pyd and dependencies using mono and into
an egg or wheel.
"""

import fnmatch
import glob
import os
import subprocess
import sys
import sysconfig
from distutils import spawn
from distutils.command import install, build_ext, install_data, install_lib
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
    if DEVTOOLS == "Mono":
        DEVTOOLS = "dotnet"


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

        if sys.platform != "win32":
            on_darwin = sys.platform == "darwin"
            if on_darwin:
                defines.append("MONO_OSX")
            else:
                defines.append("MONO_LINUX")

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
            self.announce(
                "Failed to locate interop file at {}, please run "
                "'python tools/geninterop/geninterop.py'"
            )
            raise NotImplementedError

        _solution_file = "pythonnet.sln"

        if DEVTOOLS == "MsDev":
            _xbuild = '"{0}"'.format(self._find_msbuild_tool())
            _config = "{0}Win".format(CONFIG)
        elif DEVTOOLS == "Mono":
            _xbuild = "msbuild"
            _config = "{0}Mono".format(CONFIG)
        elif DEVTOOLS == "dotnet":
            _xbuild = "dotnet msbuild"
            _config = "{0}Mono".format(CONFIG)
        else:
            raise NotImplementedError(
                "DevTool {0} not supported (use MsDev/Mono/dotnet)".format(
                    DEVTOOLS
                )
            )

        cmd = [
            _xbuild,
            _solution_file,
            "/p:Configuration={}".format(_config),
            "/p:Platform={}".format(ARCH),
            '/p:CustomDefineConstants="{}"'.format("%3B".join(defines)),
            '/p:PythonBuildDir="{}"'.format(os.path.abspath(dest_dir)),
            '/p:PythonInteropFile="{}"'.format(os.path.basename(interop_file)),
            "/verbosity:{}".format(VERBOSITY),
        ]

        self.debug_print("Building: {0}".format(" ".join(cmd)))
        use_shell = True if DEVTOOLS == "Mono" or DEVTOOLS == "dotnet" else False

        subprocess.check_call(" ".join(cmd + ["/t:Clean"]), shell=use_shell)
        subprocess.check_call(" ".join(cmd + ["/t:Build"]), shell=use_shell)
        if DEVTOOLS == "MsDev" or DEVTOOLS == "dotnet":
            subprocess.check_call(
                " ".join(
                    cmd
                    + [
                        '"/t:Console:publish;Python_EmbeddingTest:publish"',
                        "/p:TargetFramework=netcoreapp2.0",
                    ]
                ),
                shell=use_shell,
            )

        if DEVTOOLS == "Mono" or DEVTOOLS == "dotnet":
            self._build_monoclr()

    def _build_monoclr(self):
        try:
            mono_libs = _check_output("pkg-config --libs mono-2", shell=True)
        except Exception:
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

    def _find_msbuild_tool(self):
        """Return full path to one of the Microsoft build tools"""

        tool = "msbuild"

        # trying to search path with help of vswhere when MSBuild 15.0 and higher installed.
        if sys.platform == "win32":
            path = self._find_msbuild_tool_15()
            if path:
                return path

        # Search in PATH first
        path = spawn.find_executable(tool)
        if path:
            return path

        raise RuntimeError("{0} could not be found".format(tool))

    def _find_msbuild_tool_15(self):
        """Return full path to one of the Microsoft build tools"""

        import vswhere

        path = vswhere.find_first(
            latest=True,
            version="[15.0,16.0)",
            requires=["Microsoft.Component.MSBuild"],
            prop="InstallationPath",
        )

        if path:
            return os.path.join(path, "MSBuild", "15.0", "Bin", "MSBuild.exe")
        else:
            raise RuntimeError("MSBuild >=15.0 could not be found.")


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
    version="2.4.0.dev0",
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
        "Programming Language :: Python :: 3.4",
        "Programming Language :: Python :: 3.5",
        "Programming Language :: Python :: 3.6",
        "Programming Language :: Python :: 3.7",
        "Operating System :: Microsoft :: Windows",
        "Operating System :: POSIX :: Linux",
        "Operating System :: MacOS :: MacOS X",
    ],
    zip_safe=False,
)
