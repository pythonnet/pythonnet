#!/usr/bin/env python

from setuptools import setup, find_packages, Command, Extension
from wheel.bdist_wheel import bdist_wheel


class DotnetLib(Extension):
    def __init__(self, name, path, **kwargs):
        self.path = path
        self.args = kwargs
        super().__init__(name, sources=[])


class BuildDotnet(Command):
    """Build command for dotnet-cli based builds"""

    description = "Build DLLs with dotnet-cli"
    user_options = [("dotnet-config", None, "dotnet build configuration")]

    def initialize_options(self):
        self.dotnet_config = "release"

    def finalize_options(self):
        pass

    def get_source_files(self):
        return []

    def run(self):
        for lib in self.distribution.ext_modules:
            opts = sum(
                [
                    ["--" + name.replace("_", "-"), value]
                    for name, value in lib.args.items()
                ],
                [],
            )

            opts.append("--configuration")
            opts.append(self.dotnet_config)

            self.spawn(["dotnet", "build", lib.path] + opts)


class bdist_wheel_patched(bdist_wheel):
    def finalize_options(self):
        # Monkey patch bdist_wheel to think the package is pure even though we
        # include DLLs
        super().finalize_options()
        self.root_is_pure = True


with open("README.rst", "r") as f:
    long_description = f.read()

setup(
    name="pythonnet",
    version="2.5.0",
    description=".NET and Mono integration for Python",
    author="The Python for .NET developers",
    author_email="pythondotnet@python.org",
    long_description=long_description,
    license="MIT",
    install_requires=["clr-loader"],
    zip_safe=False,
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
        "Programming Language :: Python :: 3.8",
        "Operating System :: Microsoft :: Windows",
        "Operating System :: POSIX :: Linux",
        "Operating System :: MacOS :: MacOS X",
    ],
    package_data={"pythonnet": ["dlls/*.dll"]},
    packages=find_packages(),
    cmdclass={"build_ext": BuildDotnet, "bdist_wheel": bdist_wheel_patched},
    ext_modules={
        DotnetLib(
            "python-runtime",
            "Python.Runtime/",
            output="pythonnet/dlls",
        ),
    },
)
