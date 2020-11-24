import subprocess
import os

import pytest

def _run_test(testname):
    dirname = os.path.split(__file__)[0]
    exename = os.path.join(dirname, 'bin', 'Python.DomainReloadTests.exe')
    proc = subprocess.Popen([
        exename,
        testname,
    ])
    proc.wait()

    assert proc.returncode == 0

def test_rename_class():
    _run_test('class_rename')

def test_rename_class_member_static_function():
    _run_test('static_member_rename')

def test_rename_class_member_function():
    _run_test('member_rename')

def test_rename_class_member_field():
    _run_test('field_rename')

def test_rename_class_member_property():
    _run_test('property_rename')

def test_rename_namespace():
    _run_test('namespace_rename')

def test_field_visibility_change():
    _run_test("field_visibility_change")

def test_method_visibility_change():
    _run_test("method_visibility_change")

def test_property_visibility_change():
    _run_test("property_visibility_change")

def test_class_visibility_change():
    _run_test("class_visibility_change")

def test_method_parameters_change():
    _run_test("method_parameters_change")

def test_method_return_type_change():
    _run_test("method_return_type_change")

def test_field_type_change():
    _run_test("field_type_change")

@pytest.mark.xfail(reason="Events not yet serializable")
def test_rename_event():
    _run_test('event_rename')

@pytest.mark.xfail(reason="newly instanced object uses PyType_GenericAlloc")
def test_construct_removed_class():
    _run_test("construct_removed_class")

def test_out_to_ref_param():
    _run_test("out_to_ref_param")