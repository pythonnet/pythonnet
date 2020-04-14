import os
import sys

_RUNTIME = None
_LOADER_ASSEMBLY = None
_FFI = None


def set_runtime(runtime):
    global _RUNTIME
    _RUNTIME = runtime


def _find_libpython():
    v = sys.version_info
    lib_name = f"libpython{v.major}.{v.minor}{sys.abiflags}.so"
    lib_path = os.path.join(os.path.dirname(os.path.dirname(sys.executable)), "lib", lib_name)
    # return "__Internal"
    return lib_path


def load():
    dll_path = os.path.join(os.path.dirname(__file__), "dlls", "Python.Loader.dll")
    runtime_dll_path = os.path.join(os.path.dirname(dll_path), "Python.Runtime.dll")
    libpython = _find_libpython()

    global _FFI, _LOADED
    if _FFI is None and libpython != "__Internal":
        # Load and leak libpython handle s.t. the .NET runtime doesn't dlcloses it
        import posix

        import cffi
        _FFI = cffi.FFI()
        _FFI.dlopen(libpython, posix.RTLD_NODELETE | posix.RTLD_LOCAL)

    global _LOADER_ASSEMBLY
    _LOADER_ASSEMBLY = _RUNTIME.get_assembly(dll_path)

    func = _LOADER_ASSEMBLY["Python.Internal.Initialize"]
    if func(f"{runtime_dll_path};{libpython}".encode("utf8")) != 0:
        raise RuntimeError("Failed to initialize Python.Runtime.dll")

    import atexit
    atexit.register(unload)


def unload():
    if _LOADER_ASSEMBLY is not None:
        func = _LOADER_ASSEMBLY["Python.Internal.Shutdown"]
        if func(b"") != 0:
            raise RuntimeError("Failed to call Python.NET shutdown")
