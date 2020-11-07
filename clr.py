"""
Legacy Python.NET loader for backwards compatibility
"""

def _load():
    import os, sys
    import importlib.util as util

    if sys.maxsize > 2 ** 32:
        arch = "amd64"
    else:
        arch = "x86"

    path = os.path.join(os.path.dirname(__file__), "pythonnet", "dlls", arch, "clr.pyd")
    del sys.modules["clr"]

    spec = util.spec_from_file_location("clr", path)
    clr = util.module_from_spec(spec)
    spec.loader.exec_module(clr)

    sys.modules["clr"] = clr

_load()