#!/usr/bin/env python

from setuptools import setup, Command, Extension
from wheel.bdist_wheel import bdist_wheel
from setuptools.command.build_ext import build_ext
import distutils
from subprocess import check_output, check_call

import sys, os

PY_MAJOR = sys.version_info[0]
PY_MINOR = sys.version_info[1]

CONFIGURED_PROPS = "configured.props"


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


def _write_configure_props():
    defines = [
        "PYTHON{0}{1}".format(PY_MAJOR, PY_MINOR),
    ]

    if sys.platform == "win32":
        defines.append("WINDOWS")

    if hasattr(sys, "abiflags"):
        if "d" in sys.abiflags:
            defines.append("PYTHON_WITH_PYDEBUG")
        if "m" in sys.abiflags:
            defines.append("PYTHON_WITH_PYMALLOC")

    # check the interop file exists, and create it if it doesn't
    interop_file = _get_interop_filename()
    if not os.path.exists(interop_file):
        print("Creating {0}".format(interop_file))
        geninterop = os.path.join("tools", "geninterop", "geninterop.py")
        check_call([sys.executable, geninterop, interop_file])

    import xml.etree.ElementTree as ET

    proj = ET.Element("Project")
    props = ET.SubElement(proj, "PropertyGroup")
    f = ET.SubElement(props, "PythonInteropFile")
    f.text = os.path.basename(interop_file)

    c = ET.SubElement(props, "ConfiguredConstants")
    c.text = " ".join(defines)

    ET.ElementTree(proj).write(CONFIGURED_PROPS)


class Configure(Command):
    """Configure command"""

    description = "Configure the pythonnet build"
    user_options = []

    def initialize_options(self):
        pass

    def finalize_options(self):
        pass

    def run(self):
        self.announce("Writing configured.props...", level=distutils.log.INFO)
        _write_configure_props()


class DotnetLib(Extension):
    def __init__(self, name, path, **kwargs):
        self.path = path
        self.args = kwargs
        super().__init__(name, sources=[])


class BuildDotnet(build_ext):
    """Build command for dotnet-cli based builds"""

    description = "Build DLLs with dotnet-cli"
    user_options = [("dotnet-config", None, "dotnet build configuration")]

    def initialize_options(self):
        self.dotnet_config = "release"
        super().initialize_options()

    def finalize_options(self):
        super().finalize_options()

    def get_source_files(self):
        return super().get_source_files()

    def run(self):
        orig_modules = self.distribution.ext_modules
        dotnet_modules = [lib for lib in orig_modules if isinstance(lib, DotnetLib)]
        other_modules = [lib for lib in orig_modules if not isinstance(lib, DotnetLib)]

        if dotnet_modules:
            if os.path.isfile(CONFIGURED_PROPS):
                self.announce("Already configured", level=distutils.log.INFO)
            else:
                self.announce("Writing configured.props...", level=distutils.log.INFO)
                _write_configure_props()

        for lib in dotnet_modules:
            output = os.path.join(os.path.abspath(self.build_lib), lib.args.pop("output"))
            rename = lib.args.pop("rename", {})

            opts = sum(
                [
                    ["--" + name.replace("_", "-"), value]
                    for name, value in lib.args.items()
                ],
                [],
            )

            opts.extend(["--configuration", self.dotnet_config])
            opts.extend(["--output", output])

            self.announce("Running dotnet build...", level=distutils.log.INFO)
            self.spawn(["dotnet", "build", lib.path] + opts)

            for k, v in rename.items():
                source = os.path.join(output, k)
                dest = os.path.join(output, v)

                if os.path.isfile(source):
                    try:
                        os.remove(dest)
                    except OSError:
                        pass

                    self.move_file(src=source, dst=dest, level=distutils.log.INFO)
                else:
                    self.warn("Can't find file to rename: {}, current dir: {}".format(source, os.getcwd()))

        if other_modules:
            self.distribution.ext_modules = other_modules
            super().run()
            self.distribution.ext_modules = orig_modules
        # If no modules need to be compiled, skip


class bdist_wheel_patched(bdist_wheel):
    def finalize_options(self):
        # Monkey patch bdist_wheel to think the package is pure even though we
        # include DLLs
        super().finalize_options()
        self.root_is_pure = True


with open("README.rst", "r") as f:
    long_description = f.read()


ext_modules = [
    DotnetLib(
        "python-runtime",
        "src/runtime/Python.Runtime.csproj",
        output="pythonnet/runtime"
    ),
    DotnetLib(
        "clrmodule-amd64",
        "src/clrmodule/",
        runtime="win-x64",
        output="pythonnet/netfx/amd64",
        rename={"clr.dll": "clr.pyd"},
    ),
    DotnetLib(
        "clrmodule-x86",
        "src/clrmodule/",
        runtime="win-x86",
        output="pythonnet/netfx/x86",
        rename={"clr.dll": "clr.pyd"},
    ),
]

try:
    mono_libs = check_output("pkg-config --libs mono-2", shell=True, encoding="utf8")
    mono_cflags = check_output(
        "pkg-config --cflags mono-2", shell=True, encoding="utf8"
    )
    cflags = mono_cflags.strip()
    libs = mono_libs.strip()

    # build the clr python module
    clr_ext = Extension(
        "clr",
        language="c++",
        sources=["src/monoclr/clrmod.c"],
        extra_compile_args=cflags.split(" "),
        extra_link_args=libs.split(" "),
    )
    ext_modules.append(clr_ext)
except Exception:
    print("Failed to find mono libraries via pkg-config, skipping the Mono CLR loader")


setup(
    name="pythonnet",
    version="3.0.0.dev1",
    description=".Net and Mono integration for Python",
    url="https://pythonnet.github.io/",
    license="MIT",
    author="The Contributors of the Python.NET Project",
    author_email="pythonnet@python.org",
    packages=["pythonnet"],
    setup_requires=["setuptools_scm"],
    install_requires=["pycparser"],
    long_description=long_description,
    # data_files=[("{install_platlib}", ["{build_lib}/pythonnet"])],
    cmdclass={
        "build_ext": BuildDotnet,
        "bdist_wheel": bdist_wheel_patched,
        "configure": Configure,
    },
    py_modules=["clr"],
    ext_modules=ext_modules,
    classifiers=[
        "Development Status :: 5 - Production/Stable",
        "Intended Audience :: Developers",
        "License :: OSI Approved :: MIT License",
        "Programming Language :: C#",
        "Programming Language :: Python :: 3",
        "Programming Language :: Python :: 3.6",
        "Programming Language :: Python :: 3.7",
        "Programming Language :: Python :: 3.8",
        "Operating System :: Microsoft :: Windows",
        "Operating System :: POSIX :: Linux",
        "Operating System :: MacOS :: MacOS X",
    ],
    zip_safe=False,
)
