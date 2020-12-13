"""
Legacy Python.NET loader for backwards compatibility
"""

def _load_clr():
    from pythonnet import load
    load()

_load_clr()
