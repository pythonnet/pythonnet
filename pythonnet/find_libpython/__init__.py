#!/usr/bin/env python

"""
Locate libpython associated with this Python executable.
"""

# License
#
# Copyright 2018, Takafumi Arakaki
#
# Permission is hereby granted, free of charge, to any person obtaining
# a copy of this software and associated documentation files (the
# "Software"), to deal in the Software without restriction, including
# without limitation the rights to use, copy, modify, merge, publish,
# distribute, sublicense, and/or sell copies of the Software, and to
# permit persons to whom the Software is furnished to do so, subject to
# the following conditions:
#
# The above copyright notice and this permission notice shall be
# included in all copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
# EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
# MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
# NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
# LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
# OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
# WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

from __future__ import print_function, absolute_import

from logging import getLogger
import ctypes.util
import functools
import os
import sys
import sysconfig

logger = getLogger("find_libpython")

is_windows = os.name == "nt"
is_apple = sys.platform == "darwin"

SHLIB_SUFFIX = sysconfig.get_config_var("SHLIB_SUFFIX")
if SHLIB_SUFFIX is None:
    if is_windows:
        SHLIB_SUFFIX = ".dll"
    else:
        SHLIB_SUFFIX = ".so"
if is_apple:
    # sysconfig.get_config_var("SHLIB_SUFFIX") can be ".so" in macOS.
    # Let's not use the value from sysconfig.
    SHLIB_SUFFIX = ".dylib"


def linked_libpython():
    """
    Find the linked libpython using dladdr (in *nix).

    Returns
    -------
    path : str or None
        A path to linked libpython.  Return `None` if statically linked.
    """
    if is_windows:
        return _linked_libpython_windows()
    return _linked_libpython_unix()


class Dl_info(ctypes.Structure):
    _fields_ = [
        ("dli_fname", ctypes.c_char_p),
        ("dli_fbase", ctypes.c_void_p),
        ("dli_sname", ctypes.c_char_p),
        ("dli_saddr", ctypes.c_void_p),
    ]


def _linked_libpython_unix():
    if not sysconfig.get_config_var("Py_ENABLE_SHARED"):
        return None

    libdl = ctypes.CDLL(ctypes.util.find_library("dl"))
    libdl.dladdr.argtypes = [ctypes.c_void_p, ctypes.POINTER(Dl_info)]
    libdl.dladdr.restype = ctypes.c_int

    dlinfo = Dl_info()
    retcode = libdl.dladdr(
        ctypes.cast(ctypes.pythonapi.Py_GetVersion, ctypes.c_void_p),
        ctypes.pointer(dlinfo))
    if retcode == 0:  # means error
        return None
    path = os.path.realpath(dlinfo.dli_fname.decode())
    return path


def _linked_libpython_windows():
    """
    Based on: https://stackoverflow.com/a/16659821
    """
    from ctypes.wintypes import HANDLE, LPWSTR, DWORD

    GetModuleFileName = ctypes.windll.kernel32.GetModuleFileNameW
    GetModuleFileName.argtypes = [HANDLE, LPWSTR, DWORD]
    GetModuleFileName.restype = DWORD

    MAX_PATH = 260
    try:
        buf = ctypes.create_unicode_buffer(MAX_PATH)
        GetModuleFileName(ctypes.pythonapi._handle, buf, MAX_PATH)
        return buf.value
    except (ValueError, OSError):
        return None



def library_name(name, suffix=SHLIB_SUFFIX, is_windows=is_windows):
    """
    Convert a file basename `name` to a library name (no "lib" and ".so" etc.)

    >>> library_name("libpython3.7m.so")                   # doctest: +SKIP
    'python3.7m'
    >>> library_name("libpython3.7m.so", suffix=".so", is_windows=False)
    'python3.7m'
    >>> library_name("libpython3.7m.dylib", suffix=".dylib", is_windows=False)
    'python3.7m'
    >>> library_name("python37.dll", suffix=".dll", is_windows=True)
    'python37'
    """
    if not is_windows and name.startswith("lib"):
        name = name[len("lib"):]
    if suffix and name.endswith(suffix):
        name = name[:-len(suffix)]
    return name


def append_truthy(list, item):
    if item:
        list.append(item)


def uniquifying(items):
    """
    Yield items while excluding the duplicates and preserving the order.

    >>> list(uniquifying([1, 2, 1, 2, 3]))
    [1, 2, 3]
    """
    seen = set()
    for x in items:
        if x not in seen:
            yield x
        seen.add(x)


def uniquified(func):
    """ Wrap iterator returned from `func` by `uniquifying`. """
    @functools.wraps(func)
    def wrapper(*args, **kwds):
        return uniquifying(func(*args, **kwds))
    return wrapper


@uniquified
def candidate_names(suffix=SHLIB_SUFFIX):
    """
    Iterate over candidate file names of libpython.

    Yields
    ------
    name : str
        Candidate name libpython.
    """
    LDLIBRARY = sysconfig.get_config_var("LDLIBRARY")
    if LDLIBRARY and not LDLIBRARY.endswith(".a"):
        yield LDLIBRARY

    LIBRARY = sysconfig.get_config_var("LIBRARY")
    if LIBRARY and not LIBRARY.endswith(".a"):
        yield os.path.splitext(LIBRARY)[0] + suffix

    dlprefix = "" if is_windows else "lib"
    sysdata = dict(
        v=sys.version_info,
        # VERSION is X.Y in Linux/macOS and XY in Windows:
        VERSION=(sysconfig.get_python_version() or
                 "{v.major}.{v.minor}".format(v=sys.version_info) or
                 sysconfig.get_config_var("VERSION")),
        ABIFLAGS=(sysconfig.get_config_var("ABIFLAGS") or
                  sysconfig.get_config_var("abiflags") or ""),
    )

    for stem in [
            "python{VERSION}{ABIFLAGS}".format(**sysdata),
            "python{VERSION}".format(**sysdata),
            "python{v.major}".format(**sysdata),
            "python",
            ]:
        yield dlprefix + stem + suffix



@uniquified
def candidate_paths(suffix=SHLIB_SUFFIX):
    """
    Iterate over candidate paths of libpython.

    Yields
    ------
    path : str or None
        Candidate path to libpython.  The path may not be a fullpath
        and may not exist.
    """

    yield linked_libpython()

    # List candidates for directories in which libpython may exist
    lib_dirs = []
    append_truthy(lib_dirs, sysconfig.get_config_var('LIBPL'))
    append_truthy(lib_dirs, sysconfig.get_config_var('srcdir'))
    append_truthy(lib_dirs, sysconfig.get_config_var("LIBDIR"))

    # LIBPL seems to be the right config_var to use.  It is the one
    # used in python-config when shared library is not enabled:
    # https://github.com/python/cpython/blob/v3.7.0/Misc/python-config.in#L55-L57
    #
    # But we try other places just in case.

    if is_windows:
        lib_dirs.append(os.path.join(os.path.dirname(sys.executable)))
    else:
        lib_dirs.append(os.path.join(
            os.path.dirname(os.path.dirname(sys.executable)),
            "lib"))

    # For macOS:
    append_truthy(lib_dirs, sysconfig.get_config_var("PYTHONFRAMEWORKPREFIX"))

    lib_dirs.append(sys.exec_prefix)
    lib_dirs.append(os.path.join(sys.exec_prefix, "lib"))

    lib_basenames = list(candidate_names(suffix=suffix))

    for directory in lib_dirs:
        for basename in lib_basenames:
            yield os.path.join(directory, basename)

    # In macOS and Windows, ctypes.util.find_library returns a full path:
    for basename in lib_basenames:
        yield ctypes.util.find_library(library_name(basename))

# Possibly useful links:
# * https://packages.ubuntu.com/bionic/amd64/libpython3.6/filelist
# * https://github.com/Valloric/ycmd/issues/518
# * https://github.com/Valloric/ycmd/pull/519


def normalize_path(path, suffix=SHLIB_SUFFIX, is_apple=is_apple):
    """
    Normalize shared library `path` to a real path.

    If `path` is not a full path, `None` is returned.  If `path` does
    not exists, append `SHLIB_SUFFIX` and check if it exists.
    Finally, the path is canonicalized by following the symlinks.

    Parameters
    ----------
    path : str ot None
        A candidate path to a shared library.
    """
    if not path:
        return None
    if not os.path.isabs(path):
        return None
    if os.path.exists(path):
        return os.path.realpath(path)
    if os.path.exists(path + suffix):
        return os.path.realpath(path + suffix)
    if is_apple:
        return normalize_path(_remove_suffix_apple(path),
                              suffix=".so", is_apple=False)
    return None


def _remove_suffix_apple(path):
    """
    Strip off .so or .dylib.

    >>> _remove_suffix_apple("libpython.so")
    'libpython'
    >>> _remove_suffix_apple("libpython.dylib")
    'libpython'
    >>> _remove_suffix_apple("libpython3.7")
    'libpython3.7'
    """
    if path.endswith(".dylib"):
        return path[:-len(".dylib")]
    if path.endswith(".so"):
        return path[:-len(".so")]
    return path


@uniquified
def finding_libpython():
    """
    Iterate over existing libpython paths.

    The first item is likely to be the best one.

    Yields
    ------
    path : str
        Existing path to a libpython.
    """
    logger.debug("is_windows = %s", is_windows)
    logger.debug("is_apple = %s", is_apple)
    for path in candidate_paths():
        logger.debug("Candidate: %s", path)
        normalized = normalize_path(path)
        if normalized:
            logger.debug("Found: %s", normalized)
            yield normalized
        else:
            logger.debug("Not found.")


def find_libpython():
    """
    Return a path (`str`) to libpython or `None` if not found.

    Parameters
    ----------
    path : str or None
        Existing path to the (supposedly) correct libpython.
    """
    for path in finding_libpython():
        return os.path.realpath(path)


def print_all(items):
    for x in items:
        print(x)


def cli_find_libpython(cli_op, verbose, export):
    import logging
    # Importing `logging` module here so that using `logging.debug`
    # instead of `logger.debug` outside of this function becomes an
    # error.

    if verbose:
        logging.basicConfig(
            format="%(levelname)s %(message)s",
            level=logging.DEBUG)

    if cli_op == "list-all":
        print_all(finding_libpython())
    elif cli_op == "candidate-names":
        print_all(candidate_names())
    elif cli_op == "candidate-paths":
        print_all(p for p in candidate_paths() if p and os.path.isabs(p))
    else:
        path = find_libpython()
        if path is None:
            return 1
        if export:
            print(f"PYTHONNET_PYDLL={path}")
        else:
            print(path, end="")


def main(args=None):
    import argparse
    parser = argparse.ArgumentParser(
        description=__doc__)
    parser.add_argument(
        "--verbose", "-v", action="store_true",
        help="Print debugging information.")

    group = parser.add_mutually_exclusive_group()
    group.add_argument(
        "--list-all",
        action="store_const", dest="cli_op", const="list-all",
        help="Print list of all paths found.")
    group.add_argument(
        "--candidate-names",
        action="store_const", dest="cli_op", const="candidate-names",
        help="Print list of candidate names of libpython.")
    group.add_argument(
        "--candidate-paths",
        action="store_const", dest="cli_op", const="candidate-paths",
        help="Print list of candidate paths of libpython.")
    group.add_argument(
        "--export",
        action="store_true",
        help="Print as an environment export expression"
    )

    ns = parser.parse_args(args)
    parser.exit(cli_find_libpython(**vars(ns)))
