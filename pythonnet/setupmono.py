"""
Setup script for building clr.pyd and dependencies using mono and into
an egg or wheel.
"""
from setuptools import setup, Extension
from distutils.command.build_ext import build_ext
from distutils.sysconfig import get_config_vars
from platform import architecture
from subprocess import Popen, CalledProcessError, PIPE, check_call
import shutil
import sys
import os

CONFIG = "Release" # Release or Debug
DEVTOOLS = "Mono" # Mono or MsDev
VERBOSITY = "minimal" # quiet, minimal, normal, detailed, diagnostic

if DEVTOOLS == "MsDev":
    from distutils import msvc9compiler
    msvc9compiler.VERSION = 11

    cc = msvc9compiler.MSVCCompiler()
    cc.initialize()
    _xbuild = cc.find_exe("msbuild.exe")
    _defines_sep = ";"
    _config = "%sWin" % CONFIG

elif DEVTOOLS == "Mono":
    _xbuild = "xbuild"
    _defines_sep = ","
    _config = "%sMono" % CONFIG

else:
    raise NotImplementedError("DevTools %s not supported (use MsDev or Mono)" % DEVTOOLS)

_platform = "x64" if architecture()[0] == "64bit" else "x86"

class PythonNET_BuildExt(build_ext):

    def build_extension(self, ext):
        """
        Builds the .pyd file using msbuild or xbuild.
        """
        if ext.name != "clr":
            return super(PythonNET_BuildExt, self).build_extension(ext)

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
            "/p:NoNuGet=true",
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
        setup(name="monoclr",
              ext_modules=[
                Extension("clr",
                    sources=[
                        "src/monoclr/pynetinit.c",
                        "src/monoclr/clrmod.c"
                    ],
                    extra_compile_args=cflags.split(" "),
                    extra_link_args=libs.split(" "),
                )]
        )

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
                                      "npython",
                                      output_dir=output_dir,
                                      libraries=self.get_libraries(ext),
                                      library_dirs=ext.library_dirs,
                                      runtime_library_dirs=ext.runtime_library_dirs,
                                      extra_postargs=libs.split(" "),
                                      debug=self.debug)


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
    setup(name="pythonnet",
          ext_modules=[
            Extension("clr", sources=[])
          ],
          cmdclass = {
            "build_ext" : PythonNET_BuildExt
          }
    )

