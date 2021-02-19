import os
import sys
import clr_loader

_RUNTIME = None
_LOADER_ASSEMBLY = None
_FFI = None
_LOADED = False


def set_runtime(runtime):
    global _RUNTIME
    if _LOADED:
        raise RuntimeError("The runtime {runtime} has already been loaded".format(_RUNTIME))

    _RUNTIME = runtime


def set_default_runtime() -> None:
    if sys.platform == 'win32':
        set_runtime(clr_loader.get_netfx())
    else:
        set_runtime(clr_loader.get_mono())


def load():
    global _FFI, _LOADED, _LOADER_ASSEMBLY

    if _LOADED:
        return

    from .find_libpython import linked_libpython
    from os.path import join, dirname

    if _RUNTIME is None:
        # TODO: Warn, in the future the runtime must be set explicitly, either
        # as a config/env variable or via set_runtime
        set_default_runtime()

    dll_path = join(dirname(__file__), "runtime", "Python.Runtime.dll")
    libpython = linked_libpython()

    if libpython and _FFI is None and sys.platform != "win32":
        # Load and leak libpython handle s.t. the .NET runtime doesn't dlcloses
        # it
        import posix

        import cffi
        _FFI = cffi.FFI()
        _FFI.dlopen(libpython, posix.RTLD_NODELETE | posix.RTLD_LOCAL)

    _LOADER_ASSEMBLY = _RUNTIME.get_assembly(dll_path)

    func = _LOADER_ASSEMBLY["Python.Runtime.Loader.Initialize"]
    if func(f"{libpython or ''}".encode("utf8")) != 0:
        raise RuntimeError("Failed to initialize Python.Runtime.dll")

    import atexit
    atexit.register(unload)


def unload():
    global _RUNTIME
    if _LOADER_ASSEMBLY is not None:
        func = _LOADER_ASSEMBLY["Python.Runtime.Loader.Shutdown"]
        if func(b"") != 0:
            raise RuntimeError("Failed to call Python.NET shutdown")

    if _RUNTIME is not None:
        # TODO: Add explicit `close` to clr_loader
        _RUNTIME = None
