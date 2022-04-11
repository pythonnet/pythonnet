# -*- coding: utf-8 -*-
# TODO: move tests one out of src to project root.
# TODO: travis has numpy on their workers. Maybe add tests?

"""Helpers for testing."""

import ctypes
import os
import sys
import sysconfig
from subprocess import check_call
from tempfile import mkdtemp
import shutil

import pytest

from pythonnet import set_runtime

# Add path for `Python.Test`
cwd = os.path.dirname(__file__)
fixtures_path = os.path.join(cwd, "fixtures")
sys.path.append(fixtures_path)

def pytest_addoption(parser):
    parser.addoption(
        "--runtime",
        action="store",
        default="default",
        help="Must be one of default, netcore, netfx and mono"
    )

collect_ignore = []

def pytest_configure(config):
    global bin_path
    if "clr" in sys.modules:
        # Already loaded (e.g. by the C# test runner), skip build
        import clr
        clr.AddReference("Python.Test")
        return

    runtime_opt = config.getoption("runtime")

    test_proj_path = os.path.join(cwd, "..", "src", "testing")

    if runtime_opt not in ["netcore", "netfx", "mono", "default"]:
        raise RuntimeError(f"Invalid runtime: {runtime_opt}")

    bin_path = mkdtemp()

    # tmpdir_factory.mktemp(f"pythonnet-{runtime_opt}")

    fw = "net6.0" if runtime_opt == "netcore" else "netstandard2.0"

    check_call(["dotnet", "publish", "-f", fw, "-o", bin_path, test_proj_path])

    sys.path.append(bin_path)

    if runtime_opt == "default":
        pass
    elif runtime_opt == "netfx":
        from clr_loader import get_netfx
        runtime = get_netfx()
        set_runtime(runtime)
    elif runtime_opt == "mono":
        from clr_loader import get_mono
        runtime = get_mono()
        set_runtime(runtime)
    elif runtime_opt == "netcore":
        from clr_loader import get_coreclr
        rt_config_path = os.path.join(bin_path, "Python.Test.runtimeconfig.json")
        runtime = get_coreclr(rt_config_path)
        set_runtime(runtime)

    import clr
    clr.AddReference("Python.Test")

    soft_mode = False
    try:
        os.environ['PYTHONNET_SHUTDOWN_MODE'] == 'Soft'
    except: pass

    if config.getoption("--runtime") == "netcore" or soft_mode\
        :
        collect_ignore.append("domain_tests/test_domain_reload.py")
    else:
        domain_tests_dir = os.path.join(os.path.dirname(__file__), "domain_tests")
        bin_path = os.path.join(domain_tests_dir, "bin")
        build_cmd = ["dotnet", "build", domain_tests_dir, "-o", bin_path]
        is_64bits = sys.maxsize > 2**32
        if not is_64bits:
            build_cmd += ["/p:Prefer32Bit=True"]
        check_call(build_cmd)




def pytest_unconfigure(config):
    global bin_path
    try:
        shutil.rmtree(bin_path)
    except Exception:
        pass

def pytest_report_header(config):
    """Generate extra report headers"""
    # FIXME: https://github.com/pytest-dev/pytest/issues/2257
    is_64bits = sys.maxsize > 2**32
    arch = "x64" if is_64bits else "x86"
    ucs = ctypes.sizeof(ctypes.c_wchar)
    libdir = sysconfig.get_config_var("LIBDIR")
    shared = bool(sysconfig.get_config_var("Py_ENABLE_SHARED"))

    header = ("Arch: {arch}, UCS: {ucs}, LIBDIR: {libdir}, "
              "Py_ENABLE_SHARED: {shared}".format(**locals()))
    return header


@pytest.fixture()
def filepath():
    """Returns full filepath for file in `fixtures` directory."""

    def make_filepath(filename):
        # http://stackoverflow.com/questions/18011902/parameter-to-a-fixture
        return os.path.join(fixtures_path, filename)

    return make_filepath
