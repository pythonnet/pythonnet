import os

_RUNTIME = None


def set_runtime(runtime):
    global _RUNTIME
    _RUNTIME = runtime


def load():
    dll_path = os.path.join(os.path.dirname(__file__), "dlls", "Python.Runtime.dll")

    assembly = _RUNTIME.get_assembly(dll_path)
    func = assembly["Python.Runtime.PythonEngine.InternalInitialize"]

    if func(b"") != 0:
        raise RuntimeError("Failed to initialize Python.Runtime.dll")
