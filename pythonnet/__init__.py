import os
import sys

_RUNTIME = None


def set_runtime(runtime):
    global _RUNTIME
    _RUNTIME = runtime


def _find_libpython():
    v = sys.version_info
    lib_name = f"libpython{v.major}.{v.minor}{sys.abiflags}.so"
    lib_path = os.path.join(os.path.dirname(os.path.dirname(sys.executable)), "lib", lib_name)
    return lib_path


def load():
    dll_path = os.path.join(os.path.dirname(__file__), "dlls", "Python.Loader.dll")
    runtime_dll_path = os.path.join(os.path.dirname(dll_path), "Python.Runtime.dll")

    assembly = _RUNTIME.get_assembly(dll_path)

    print("Got assembly", assembly)
    func = assembly["Python.Internal.Initialize"]

    print("Got func:", func)

    if func(f"{runtime_dll_path};{_find_libpython()}".encode("utf8")) != 0:
        raise RuntimeError("Failed to initialize Python.Runtime.dll")
