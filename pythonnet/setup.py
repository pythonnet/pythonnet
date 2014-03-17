"""
Setup script for building clr.pyd and dependencies using mono and into
an egg or wheel.
"""
from setuptools import setup, Extension
from distutils.command.build_ext import build_ext
from distutils.command.build_scripts import build_scripts
from distutils.command.install_lib import install_lib
from distutils.sysconfig import get_config_vars
from platform import architecture
from subprocess import Popen, CalledProcessError, PIPE, check_call
from glob import glob
import shutil
import sys
import os

CONFIG = "Release" # Release or Debug
DEVTOOLS = "MsDev" if sys.platform == "win32" else "Mono"
VERBOSITY = "minimal" # quiet, minimal, normal, detailed, diagnostic

def FindMsBuildPath():
    import _winreg

    aReg = _winreg.ConnectRegistry(None,_winreg.HKEY_LOCAL_MACHINE)
    try:
        keysToCheck = [r"SOFTWARE\Microsoft\MSBuild\ToolsVersions\12.0", r"SOFTWARE\Microsoft\MSBuild\ToolsVersions\4.0", r"SOFTWARE\Microsoft\MSBuild\ToolsVersions\3.5", r"SOFTWARE\Microsoft\MSBuild\ToolsVersions\2.0"]
        aKey = None
        for key in keysToCheck:
            try:
                aKey = _winreg.OpenKey(aReg, key)
                break
            except WindowsError:
                pass

        if aKey==None:
            raise RuntimeError("MSBUILD.exe could not be found")

        try:
            val, type = _winreg.QueryValueEx(aKey, "MSBuildToolsPath")

            if type!=_winreg.REG_SZ:
                raise RuntimeError("MSBUILD.exe could not be found")
        finally:
            aKey.Close()
    finally:
        aReg.Close()

    msbuildpath = os.path.join(val, "msbuild.exe")
    return msbuildpath


if DEVTOOLS == "MsDev":
    _xbuild = "\"%s\"" % FindMsBuildPath()
    _defines_sep = ";"
    _config = "%sWin" % CONFIG
    _npython_exe = "nPython.exe"

elif DEVTOOLS == "Mono":
    _xbuild = "xbuild"
    _defines_sep = ","
    _config = "%sMono" % CONFIG
    _npython_exe = "npython"

else:
    raise NotImplementedError("DevTools %s not supported (use MsDev or Mono)" % DEVTOOLS)

_platform = "x64" if architecture()[0] == "64bit" else "x86"

class PythonNET_BuildExt(build_ext):

    def build_extension(self, ext):
        """
        Builds the .pyd file using msbuild or xbuild.
        """
        if ext.name != "clr":
            return build_ext.build_extension(self, ext)

        dest_file = self.get_ext_fullpath(ext.name)
        dest_dir = os.path.dirname(dest_file)
        if not os.path.exists(dest_dir):
            os.makedirs(dest_dir)

        defines = [
            "PYTHON%d%s" % (sys.version_info[:2]),
            "UCS2" if sys.maxunicode < 0x10FFFF else "UCS4",
        ]

        if CONFIG == "Debug":
            defines.extend(["DEBUG", "TRACE"])

        cmd = [
            _xbuild,
            "pythonnet.sln",
            "/p:Configuration=%s" % _config,
            "/p:Platform=%s" % _platform,
            "/p:DefineConstants=\"%s\"" % _defines_sep.join(defines),
            "/p:PythonBuildDir=%s" % os.path.abspath(dest_dir),
            "/verbosity:%s" % VERBOSITY,
        ]

        self.announce("Building: %s" % " ".join(cmd))
        check_call(" ".join(cmd) + " /t:Clean", shell=True)
        check_call(" ".join(cmd) + " /t:Build", shell=True)

        if DEVTOOLS == "Mono":
            self._build_monoclr(ext)


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

        build_ext.build_extension(self, clr_ext)

        # build the clr python executable
        sources = [
            "src/monoclr/pynetinit.c",
            "src/monoclr/python.c",
        ]

        macros = ext.define_macros[:]
        for undef in ext.undef_macros:
            macros.append((undef,))

        objects = self.compiler.compile(sources,
                                        output_dir=self.build_temp,
                                        macros=macros,
                                        include_dirs=ext.include_dirs,
                                        debug=self.debug,
                                        extra_postargs=cflags.split(" "),
                                        depends=ext.depends)

        output_dir = os.path.dirname(self.get_ext_fullpath(ext.name))
        py_libs = get_config_vars("BLDLIBRARY")[0]
        libs += " " + py_libs

        self.compiler.link_executable(objects,
                                      _npython_exe,
                                      output_dir=output_dir,
                                      libraries=self.get_libraries(ext),
                                      library_dirs=ext.library_dirs,
                                      runtime_library_dirs=ext.runtime_library_dirs,
                                      extra_postargs=libs.split(" "),
                                      debug=self.debug)


class PythonNET_InstallLib(install_lib):

    def install(self):
        if not os.path.isdir(self.build_dir):
            self.warn("'%s' does not exist -- no Python modules to install" %
                        self.build_dir)
            return

        if not os.path.exists(self.install_dir):
            self.mkpath(self.install_dir)

        # only copy clr.pyd and its dependencies
        for pattern in ("clr.*", "Python.Runtime.*"):
            for srcfile in glob(os.path.join(self.build_dir, pattern)):
                destfile = os.path.join(self.install_dir, os.path.basename(srcfile))
                self.copy_file(srcfile, destfile)
    

class PythonNET_BuildScripts(build_scripts):

    def finalize_options(self):
        build_scripts.finalize_options(self)

        # fixup scripts to look in the build_ext output folder
        if self.scripts:
            build_ext = self.get_finalized_command("build_ext")
            output_dir = os.path.dirname(build_ext.get_ext_fullpath(_npython_exe))
            scripts = []
            for script in self.scripts:
                if os.path.exists(os.path.join(output_dir, script)):
                    script = os.path.join(output_dir, script)
                scripts.append(script)
            self.scripts = scripts


def _check_output(*popenargs, **kwargs):
    """subprocess.check_output from python 2.7.
    Added here to support building for earlier versions
    of Python.
    """
    process = Popen(stdout=PIPE, *popenargs, **kwargs)
    output, unused_err = process.communicate()
    retcode = process.poll()
    if retcode:
        cmd = kwargs.get("args")
        if cmd is None:
            cmd = popenargs[0]
        raise CalledProcessError(retcode, cmd, output=output)
    return output


if __name__ == "__main__":
    setup(
        name="pythonnet",
        version="2.0.0.dev1",
        description=".Net and Mono integration for Python",
        url='http://pythonnet.github.io/',
        author="Python for .Net developers",
        classifiers=[
            'Development Status :: 3 - Alpha',
            'Intended Audience :: Developers'],
        ext_modules=[
            Extension("clr", sources=[])
        ],
        scripts=[_npython_exe],
        zip_safe=False,
        cmdclass={
            "build_ext" : PythonNET_BuildExt,
            "build_scripts" : PythonNET_BuildScripts,
            "install_lib" : PythonNET_InstallLib
        }
    )

