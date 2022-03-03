#!/usr/bin/env python

from setuptools import setup, Command, Extension
from setuptools.command.build_ext import build_ext
import distutils
from distutils.command import build
from subprocess import check_output, check_call

import sys, os

# Disable SourceLink during the build until it can read repo-format v1, #1613
os.environ["EnableSourceControlManagerQueries"] = "false"

class DotnetLib:
    def __init__(self, name, path, **kwargs):
        self.name = name
        self.path = path
        self.args = kwargs


class build_dotnet(Command):
    """Build command for dotnet-cli based builds"""

    description = "Build DLLs with dotnet-cli"
    user_options = [
        ("dotnet-config=", None, "dotnet build configuration"),
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
from wheel.bdist_wheel import bdist_wheel as _bdist_wheel
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


class bdist_wheel(_bdist_wheel):
    def finalize_options(self):
        # Monkey patch bdist_wheel to think the package is pure even though we
        # include DLLs
        super().finalize_options()
        self.root_is_pure = True


# Monkey-patch Distribution s.t. it supports the dotnet_libs attribute
Distribution.dotnet_libs = None

cmdclass = {
    "build": build,
    "build_dotnet": build_dotnet,
    "develop": develop,
    "bdist_wheel": bdist_wheel,
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
    project_urls={
        "Source": "https://github.com/pythonnet/pythonnet",
    },
    license="MIT",
    author="The Contributors of the Python.NET Project",
    author_email="pythonnet@python.org",
    packages=["pythonnet", "pythonnet.find_libpython"],
    install_requires=["clr_loader >= 0.1.7"],
    long_description=long_description,
    long_description_content_type="text/x-rst",
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
        "Programming Language :: Python :: 3.9",
        "Programming Language :: Python :: 3.10",
        "Operating System :: Microsoft :: Windows",
        "Operating System :: POSIX :: Linux",
        "Operating System :: MacOS :: MacOS X",
    ],
    zip_safe=False,
)
