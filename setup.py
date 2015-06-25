"""
Setup script for building clr.pyd and dependencies using mono and into
an egg or wheel.
"""
from setuptools import setup, Extension
from distutils.command.build_ext import build_ext
from distutils.command.build_scripts import build_scripts
from distutils.command.install_lib import install_lib
from distutils.command.install_data import install_data
from distutils.sysconfig import get_config_var
from platform import architecture
from subprocess import Popen, CalledProcessError, PIPE, check_call
from glob import glob
import fnmatch
import shutil
import sys
import os

CONFIG = "Release" # Release or Debug
DEVTOOLS = "MsDev" if sys.platform == "win32" else "Mono"
VERBOSITY = "minimal" # quiet, minimal, normal, detailed, diagnostic
PLATFORM = "x64" if architecture()[0] == "64bit" else "x86"


def _find_msbuild_tool(tool="msbuild.exe", use_windows_sdk=False):
    """Return full path to one of the microsoft build tools"""
    import _winreg

    if use_windows_sdk:
        value_name = "InstallationFolder"
        sdk_name = "Windows SDK"
        keys_to_check = [
            r"SOFTWARE\Microsoft\Microsoft SDKs\Windows\v7.1A\WinSDK-Win32Tools",
            r"SOFTWARE\Microsoft\Microsoft SDKs\Windows\v7.1\WinSDKWin32Tools",
            r"SOFTWARE\Microsoft\Microsoft SDKs\Windows\v7.0A\WinSDK-Win32Tools",
            r"SOFTWARE\Microsoft\Microsoft SDKs\Windows\v7.0\WinSDKWin32Tools",
            r"SOFTWARE\Microsoft\Microsoft SDKs\Windows\v6.0A\WinSDKWin32Tools",
        ]
    else:
        value_name = "MSBuildToolsPath"
        sdk_name = "MSBuild"
        keys_to_check = [
            r"SOFTWARE\Microsoft\MSBuild\ToolsVersions\12.0",
            r"SOFTWARE\Microsoft\MSBuild\ToolsVersions\4.0",
            r"SOFTWARE\Microsoft\MSBuild\ToolsVersions\3.5",
            r"SOFTWARE\Microsoft\MSBuild\ToolsVersions\2.0"
        ]

    hreg = _winreg.ConnectRegistry(None, _winreg.HKEY_LOCAL_MACHINE)
    try:
        hkey = None
        for key in keys_to_check:
            try:
                hkey = _winreg.OpenKey(hreg, key)
                break
            except WindowsError:
                pass

        if hkey is None:
            raise RuntimeError("%s could not be found" % sdk_name)

        try:
            val, type_ = _winreg.QueryValueEx(hkey, value_name)
            if type_ != _winreg.REG_SZ:
                raise RuntimeError("%s could not be found" % sdk_name)
 
            path = os.path.join(val, tool)
            if os.path.exists(path):
                return path
        finally:
            hkey.Close()
    finally:
        hreg.Close()

    raise RuntimeError("%s could not be found" % tool)
    

if DEVTOOLS == "MsDev":
    _xbuild = "\"%s\"" % _find_msbuild_tool("msbuild.exe")
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


class PythonNET_BuildExt(build_ext):

    def build_extension(self, ext):
        """
        Builds the .pyd file using msbuild or xbuild.
        """
        if ext.name != "clr":
            return build_ext.build_extension(self, ext)

        # install packages using nuget
        self._install_packages()

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
            "/p:Platform=%s" % PLATFORM,
            "/p:DefineConstants=\"%s\"" % _defines_sep.join(defines),
            "/p:PythonBuildDir=%s" % os.path.abspath(dest_dir),
            "/verbosity:%s" % VERBOSITY,
        ]

        manifest = self._get_manifest(dest_dir)
        if manifest:
            cmd.append("/p:PythonManifest=\"%s\"" % manifest)

        self.announce("Building: %s" % " ".join(cmd))
        use_shell = True if DEVTOOLS == "Mono" else False
        check_call(" ".join(cmd + ["/t:Clean"]), shell=use_shell)
        check_call(" ".join(cmd + ["/t:Build"]), shell=use_shell)

        if DEVTOOLS == "Mono":
            self._build_monoclr(ext)


    def _get_manifest(self, build_dir):
        if DEVTOOLS == "MsDev" and sys.version_info[:2] > (2,5):
            mt = _find_msbuild_tool("mt.exe", use_windows_sdk=True)
            manifest = os.path.abspath(os.path.join(build_dir, "app.manifest"))
            cmd = [mt, '-inputresource:"%s"' % sys.executable, '-out:"%s"' % manifest]
            self.announce("Extracting manifest from %s" % sys.executable)
            check_call(" ".join(cmd), shell=False)
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
        py_libs = get_config_var("BLDLIBRARY")
        libs += " " + py_libs

        # Include the directories python's shared libs were installed to. This
        # is case python was built with --enable-shared as then npython will need
        # to be able to find libpythonX.X.so.
        runtime_library_dirs = (get_config_var("DESTDIRS") or "").split(" ")
        if ext.runtime_library_dirs:
            runtime_library_dirs.extend(ext.runtime_library_dirs)

        self.compiler.link_executable(objects,
                                      _npython_exe,
                                      output_dir=output_dir,
                                      libraries=self.get_libraries(ext),
                                      library_dirs=ext.library_dirs,
                                      runtime_library_dirs=runtime_library_dirs,
                                      extra_postargs=libs.split(" "),
                                      debug=self.debug)


    def _install_packages(self):
        """install packages using nuget"""
        nuget = os.path.join("tools", "nuget", "nuget.exe")
        use_shell = False
        if DEVTOOLS == "Mono":
            nuget = "mono %s" % nuget
            use_shell = True

        cmd = "%s restore pythonnet.sln -o packages" % nuget
        self.announce("Installing packages: %s" % cmd)
        check_call(cmd, shell=use_shell)


class PythonNET_InstallLib(install_lib):

    def install(self):
        if not os.path.isdir(self.build_dir):
            self.warn("'%s' does not exist -- no Python modules to install" %
                        self.build_dir)
            return

        if not os.path.exists(self.install_dir):
            self.mkpath(self.install_dir)

        # only copy clr.pyd/.so
        for srcfile in glob(os.path.join(self.build_dir, "clr.*")):
            destfile = os.path.join(self.install_dir, os.path.basename(srcfile))
            self.copy_file(srcfile, destfile)


class PythonNET_InstallData(install_data):

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

        return install_data.run(self)
 

class PythonNET_BuildScripts(build_scripts):

    def run(self):
        build_scripts.finalize_options(self)

        # fixup scripts to look in the build_ext output folder
        if self.scripts:
            build_ext = self.get_finalized_command("build_ext")
            output_dir = os.path.dirname(build_ext.get_ext_fullpath("clr"))
            scripts = []
            for script in self.scripts:
                if os.path.exists(os.path.join(output_dir, script)):
                    script = os.path.join(output_dir, script)
                scripts.append(script)
            self.scripts = scripts

        return build_scripts.run(self)


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
    setupdir = os.path.dirname(__file__)
    if setupdir:
        os.chdir(setupdir)

    sources = []
    for ext in (".sln", ".snk", ".config"):
        sources.extend(glob("*" + ext))

    for root, dirnames, filenames in os.walk("src"):
        for ext in (".cs", ".csproj", ".sln", ".snk", ".config", ".il", ".py", ".c", ".h", ".ico"):
            for filename in fnmatch.filter(filenames, "*" + ext):
                sources.append(os.path.join(root, filename))

    for root, dirnames, filenames in os.walk("tools"):
        for ext in (".exe"):
            for filename in fnmatch.filter(filenames, "*" + ext):
                sources.append(os.path.join(root, filename))

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
            Extension("clr", sources=sources)
        ],
        data_files=[
            ("{install_platlib}", [
                "{build_lib}/Python.Runtime.dll",
                "Python.Runtime.dll.config"]),
        ],
        scripts=[_npython_exe],
        zip_safe=False,
        cmdclass={
            "build_ext" : PythonNET_BuildExt,
            "build_scripts" : PythonNET_BuildScripts,
            "install_lib" : PythonNET_InstallLib,
            "install_data": PythonNET_InstallData,
        }
    )

