.. Python.NET documentation master file, created by
   sphinx-quickstart on Thu May 26 12:32:51 2022.
   You can adapt this file completely to your liking, but it should at least
   contain the root `toctree` directive.

Welcome to Python.NET's documentation!
======================================

Python.NET (`pythonnet`) is a package that gives Python programmers nearly
seamless integration with the .NET 4.0+ Common Language Runtime (CLR) on Windows
and Mono runtime on Linux and OSX. Python.NET provides a powerful application
scripting tool for .NET developers. Using this package you can script .NET
applications or build entire applications in Python, using .NET services and
components written in any language that targets the CLR (C#, VB.NET, F#,
C++/CLI).

Note that this package does _not_ implement Python as a first-class CLR
language - it does not produce managed code (IL) from Python code. Rather,
it is an integration of the CPython engine with the .NET or Mono runtime.
This approach allows you to use CLR services and continue to use existing
Python code and C-API extensions while maintaining native execution
speeds for Python code. If you are interested in a pure managed-code
implementation of the Python language, you should check out the
`IronPython`_ project.

Python.NET is currently compatible and tested with Python releases from 3.7
onwards.

Current releases are available on `PyPi <https://pypi.org/project/pythonnet/>`_
and `Nuget.org <https://nuget.org/packages/pythonnet>`_.

To subscribe to the `Python.NET mailing list <ml_>`_ or read the
`online archives`_ of the list, see the `mailing list information <ml_>`_
page. Use the `Python.NET issue tracker`_ to report issues.

.. _IronPython: https://ironpython.net/
.. _ml: https://mail.python.org/mailman3/lists/pythonnet.python.org/
.. _online archives: https://mail.python.org/archives/list/pythonnet@python.org/
.. _Python.NET issue tracker: https://github.com/pythonnet/pythonnet/issues

.. toctree::
    :maxdepth: 2
    :caption: Contents:

    python
    dotnet
    codecs
    pyreference
    reference

Indices and tables
==================

* :ref:`genindex`
* :ref:`modindex`
* :ref:`search`

License
=======

Python.NET is released under the open source MIT License. A copy of the license
is included in the distribution, or you can find a copy of the license
online.

.NET Foundation
===============

This project is supported by the `.NET Foundation <https://dotnetfoundation.org>`_.
