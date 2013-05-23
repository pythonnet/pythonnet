"""
Setup for packaging clr into an egg.
"""
from distutils.core import setup, Extension
from distutils.command.build_ext import build_ext
from platform import architecture
import subprocess
import shutil
import sys
import os

from distutils import msvc9compiler
msvc9compiler.VERSION = 11

class PythonNET_BuildExt(build_ext):

    def build_extension(self, ext):
        """
        Builds the .pyd file using msbuild.
        """
        if ext.name != "clr":
            return super(PythonNET_BuildExt, self).build_extension(ext)

        cc = msvc9compiler.MSVCCompiler()
        cc.initialize()
        msbuild = cc.find_exe("msbuild.exe")
        platform = "x64" if architecture()[0] == "64bit" else "x86"
        defines = [
            "PYTHON%d%s" % (sys.version_info[:2]),
            "UCS2"
        ]

        cmd = [
            msbuild,
            "pythonnet.sln",
            "/p:Configuration=ReleaseWin",
            "/p:Platform=%s" % platform,
            "/p:DefineConstants=\"%s\"" % ";".join(defines),
            "/t:clrmodule",
        ]
        self.announce("Building: %s" % " ".join(cmd))
        subprocess.check_call(" ".join(cmd))

        dest_file = self.get_ext_fullpath(ext.name)
        dest_dir = os.path.dirname(dest_file)
        if not os.path.exists(dest_dir):
            os.makedirs(dest_dir)
 
        src_file = os.path.join("src", "clrmodule", "bin", platform, "Release", "clr.pyd")
        self.announce("Copying %s to %s" % (src_file, dest_file))
        shutil.copyfile(src_file, dest_file)

        dest_file = os.path.join(dest_dir, "Python.Runtime.dll")
        src_file = os.path.join("src", "runtime", "bin", platform, "Release", "Python.Runtime.dll")
        self.announce("Copying %s to %s" % (src_file, dest_file))
        shutil.copyfile(src_file, dest_file)

setup(name="pythonnet",
        ext_modules=[
            Extension("clr", sources=[])
        ],
        cmdclass = {
            "build_ext" : PythonNET_BuildExt
        }
)
