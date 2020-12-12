#!/usr/bin/env python

from setuptools import setup, Command, Extension
from setuptools.command.build_ext import build_ext
import distutils
from distutils.command import build
from subprocess import check_output, check_call

import sys, os

BUILD_MONO = True
BUILD_NETFX = True

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


# Write configuration to configured.props. This will go away once all of these
# can be decided at runtime.
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


class configure(Command):
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


class DotnetLib:
    def __init__(self, name, path, **kwargs):
        self.name = name
        self.path = path
        self.args = kwargs


class build_dotnet(Command):
    """Build command for dotnet-cli based builds"""

    description = "Build DLLs with dotnet-cli"
    user_options = [
        ("dotnet-config", None, "dotnet build configuration"),
        (
            "inplace",
            "i",
            "ignore build-lib and put compiled extensions into the source "
            + "directory alongside your pure Python modules",
        ),
    ]

    def initialize_options(self):
        self.dotnet_config = None
        self.build_lib = None
        self.inplace = False

    def finalize_options(self):
        if self.dotnet_config is None:
            self.dotnet_config = "release"

        build = self.distribution.get_command_obj("build")
        build.ensure_finalized()
        if self.inplace:
            self.build_lib = "."
        else:
            self.build_lib = build.build_lib

    def run(self):
        dotnet_modules = self.distribution.dotnet_libs
        self.run_command("configure")

        for lib in dotnet_modules:
            output = os.path.join(
                os.path.abspath(self.build_lib), lib.args.pop("output")
            )
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
                    self.warn(
                        "Can't find file to rename: {}, current dir: {}".format(
                            source, os.getcwd()
                        )
                    )


# Add build_dotnet to the build tasks:
from distutils.command.build import build as _build
from setuptools.command.develop import develop as _develop
from setuptools import Distribution
import setuptools


class build(_build):
    sub_commands = _build.sub_commands + [("build_dotnet", None)]


class develop(_develop):
    def install_for_development(self):
        # Build extensions in-place
        self.reinitialize_command("build_dotnet", inplace=1)
        self.run_command("build_dotnet")

        return super().install_for_development()


# Monkey-patch Distribution s.t. it supports the dotnet_libs attribute
Distribution.dotnet_libs = None

cmdclass = {
    "build": build,
    "build_dotnet": build_dotnet,
    "configure": configure,
    "develop": develop,
}


with open("README.rst", "r") as f:
    long_description = f.read()

dotnet_libs = [
    DotnetLib(
        "python-runtime",
        "src/runtime/Python.Runtime.csproj",
        output="pythonnet/runtime",
    )
]

setup(
    cmdclass=cmdclass,
    name="pythonnet",
    version="3.0.0.dev1",
    description=".Net and Mono integration for Python",
    url="https://pythonnet.github.io/",
    license="MIT",
    author="The Contributors of the Python.NET Project",
    author_email="pythonnet@python.org",
    packages=["pythonnet"],
    install_requires=["pycparser", "clr_loader"],
    long_description=long_description,
    # data_files=[("{install_platlib}", ["{build_lib}/pythonnet"])],
    py_modules=["clr"],
    dotnet_libs=dotnet_libs,
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
