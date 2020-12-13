import os
import sys
import clr_loader

_RUNTIME = None
_LOADER_ASSEMBLY = None
_FFI = None
_LOADED = False


def set_runtime(runtime):
    global _RUNTIME
    _RUNTIME = runtime


def set_default_runtime():
    if sys.platform == 'win32':
        set_runtime(clr_loader.get_netfx())
    else:
        set_runtime(clr_loader.get_mono(gc=""))


def load():
    global _FFI, _LOADED, _LOADER_ASSEMBLY

    if _LOADED:
        return

    from .util import find_libpython
    from os.path import join, dirname, basename

    if _RUNTIME is None:
        # TODO: Warn, in the future the runtime must be set explicitly, either as a
        # config/env variable or via set_runtime
        set_default_runtime()

    dll_path = join(dirname(__file__), "runtime", "Python.Loader.dll")
    runtime_dll_path = join(dirname(dll_path), "Python.Runtime.dll")
    libpython = basename(find_libpython())
    # TODO: Add dirname of libpython to (DY)LD_LIBRARY_PATH or PATH

    if _FFI is None and libpython != "__Internal":
        # Load and leak libpython handle s.t. the .NET runtime doesn't dlcloses it
        import posix

        import cffi
        _FFI = cffi.FFI()
        _FFI.dlopen(libpython, posix.RTLD_NODELETE | posix.RTLD_LOCAL)

    _LOADER_ASSEMBLY = _RUNTIME.get_assembly(dll_path)

    func = _LOADER_ASSEMBLY["Python.Loader.Internal.Initialize"]
    if func(f"{runtime_dll_path};{libpython}".encode("utf8")) != 0:
        raise RuntimeError("Failed to initialize Python.Runtime.dll")

    import atexit
    atexit.register(unload)


def unload():
    if _LOADER_ASSEMBLY is not None:
        func = _LOADER_ASSEMBLY["Python.Loader.Internal.Shutdown"]
        if func(b"") != 0:
            raise RuntimeError("Failed to call Python.NET shutdown")
