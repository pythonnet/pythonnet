"""
Legacy Python.NET loader for backwards compatibility
"""

def _get_netfx_path():
    import os, sys

    if sys.maxsize > 2 ** 32:
        arch = "amd64"
    else:
        arch = "x86"

    return os.path.join(os.path.dirname(__file__), "pythonnet", "netfx", arch, "clr.pyd")


def _get_mono_path():
    import os, glob

    paths = glob.glob(os.path.join(os.path.dirname(__file__), "pythonnet", "mono", "clr.*so"))
    return paths[0]


def _load_clr():
    import sys
    from importlib import util

    if sys.platform == "win32":
        path = _get_netfx_path()
    else:
        path = _get_mono_path()

    del sys.modules[__name__]

    spec = util.spec_from_file_location("clr", path)
    clr = util.module_from_spec(spec)
    spec.loader.exec_module(clr)

    sys.modules[__name__] = clr


_load_clr()
