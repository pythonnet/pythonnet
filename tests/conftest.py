# -*- coding: utf-8 -*-
# TODO: move tests one out of src to project root.
# TODO: travis has numpy on their workers. Maybe add tests?

"""Helpers for testing."""

import ctypes
import os
import sys
import sysconfig
from pathlib import Path
from subprocess import check_call
from tempfile import mkdtemp
import shutil

import pytest

# Add path for `Python.Test`
cwd = Path(__file__).parent
fixtures_path = cwd / "fixtures"
sys.path.append(str(fixtures_path))


def pytest_addoption(parser):
    parser.addoption(
        "--runtime",
        action="store",
        default="default",
        help="Must be one of default, coreclr, netfx and mono",
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
    if runtime_opt not in ["coreclr", "netfx", "mono", "default"]:
        raise RuntimeError(f"Invalid runtime: {runtime_opt}")

    test_proj_path = cwd.parent / "src" / "testing"
    bin_path = Path(mkdtemp())

    fw = "netstandard2.0"
    runtime_params = {}

    if runtime_opt == "coreclr":
        # This is optional now:
        #
        # fw = "net6.0"
        # runtime_params["runtime_config"] = str(
        #     bin_path / "Python.Test.runtimeconfig.json"
        # )
        collect_ignore.append("domain_tests/test_domain_reload.py")
    else:
        domain_tests_dir = cwd / "domain_tests"
        domain_bin_path = domain_tests_dir / "bin"
        build_cmd = [
            "dotnet",
            "build",
            str(domain_tests_dir),
            "-o",
            str(domain_bin_path),
        ]
        is_64bits = sys.maxsize > 2**32
        if not is_64bits:
            build_cmd += ["/p:Prefer32Bit=True"]
        check_call(build_cmd)

    check_call(
        ["dotnet", "publish", "-f", fw, "-o", str(bin_path), str(test_proj_path)]
    )

    import os
    os.environ["PYTHONNET_RUNTIME"] = runtime_opt
    for k, v in runtime_params.items():
        os.environ[f"PYTHONNET_{runtime_opt.upper()}_{k.upper()}"] = v

    import clr

    sys.path.append(str(bin_path))
    clr.AddReference("Python.Test")


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

    return f"Arch: {arch}, UCS: {ucs}, LIBDIR: {libdir}"


@pytest.fixture()
def filepath():
    """Returns full filepath for file in `fixtures` directory."""

    def make_filepath(filename):
        # http://stackoverflow.com/questions/18011902/parameter-to-a-fixture
        return os.path.join(fixtures_path, filename)

    return make_filepath
