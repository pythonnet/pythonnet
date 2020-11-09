import subprocess
import os
import pytest

def _run_test(testname):
    dirname = os.path.split(__file__)[0]
    exename = os.path.join(dirname, 'bin', 'Python.DomainReloadTests.exe'),
    proc = subprocess.Popen([
        exename,
        testname,
    ])
    proc.wait()

    assert proc.returncode == 0

@pytest.mark.xfail(reason="Issue not yet fixed.")
def test_rename_class():
    _run_test('class_rename')

@pytest.mark.xfail(reason="Issue not yet fixed.")
def test_rename_class_member_static_function():
    _run_test('static_member_rename')

@pytest.mark.xfail(reason="Issue not yet fixed.")
def test_rename_class_member_function():
    _run_test('member_rename')
