# -*- coding: utf-8 -*-
# TODO: move tests one out of src. Pythonnet doesn't run...

"""Helpers for testing."""

import io
import os
import sys

import pytest
import clr

sys.path.append('C:/testdir/')
clr.AddReference("Python.Test")
clr.AddReference("System.Collections")
clr.AddReference("System.Data")

DIR_PATH = os.path.dirname(__file__)
FILES_DIR = os.path.join(DIR_PATH, 'files')


@pytest.fixture()
def filepath():
    """Returns full file path for test files."""

    def make_filepath(filename):
        # http://stackoverflow.com/questions/18011902/parameter-to-a-fixture
        # Alternate solution is to use paramtrization `inderect=True`
        # http://stackoverflow.com/a/33879151
        # Syntax is noisy and requires specific variable names
        return os.path.join(FILES_DIR, filename)

    return make_filepath


@pytest.fixture()
def load_file(filepath):
    """Opens filename with encoding and return its contents."""

    def make_load_file(filename, encoding='utf-8'):
        # http://stackoverflow.com/questions/18011902/parameter-to-a-fixture
        # Alternate solution is to use paramtrization `inderect=True`
        # http://stackoverflow.com/a/33879151
        # Syntax is noisy and requires specific variable names
        # And seems to be limited to only 1 argument.
        with io.open(filepath(filename), encoding=encoding) as f:
            return f.read().strip()

    return make_load_file


@pytest.fixture()
def get_stream(filepath):
    def make_stream(filename, encoding='utf-8'):
        return io.open(filepath(filename), encoding=encoding)

    return make_stream
