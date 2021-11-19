pythonnet - Python.NET
===========================

|Join the chat at https://gitter.im/pythonnet/pythonnet| |stackexchange shield|

|gh shield|

|license shield|

|pypi package version| |conda-forge version| |python supported shield|

|nuget preview shield| |nuget release shield|

Python.NET is a package that gives Python programmers nearly
seamless integration with the .NET Common Language Runtime (CLR) and
provides a powerful application scripting tool for .NET developers. It
allows Python code to interact with the CLR, and may also be used to
embed Python into a .NET application.

.. note::
   The master branch of this repository tracks the ongoing development of version 3.0.
   Backports of patches to 2.5 are tracked in the
   `backports-2.5 branch <https://github.com/pythonnet/pythonnet/tree/backports-2.5>`_.

Calling .NET code from Python
-----------------------------

Python.NET allows CLR namespaces to be treated essentially as Python packages.

.. code-block:: python

   import clr
   from System import String
   from System.Collections import *

To load an assembly, use the ``AddReference`` function in the ``clr``
module:

.. code-block:: python

   import clr
   clr.AddReference("System.Windows.Forms")
   from System.Windows.Forms import Form

Embedding Python in .NET
------------------------

-  You must set ``Runtime.PythonDLL`` property or ``PYTHONNET_PYDLL`` environment variable
   starting with version 3.0, otherwise you will receive ``BadPythonDllException``
   (internal, derived from ``MissingMethodException``) upon calling ``Initialize``.
   Typical values are ``python38.dll`` (Windows), ``libpython3.8.dylib`` (Mac),
   ``libpython3.8.so`` (most other Unix-like operating systems).
-  All calls to python should be inside a
   ``using (Py.GIL()) {/* Your code here */}`` block.
-  Import python modules using ``dynamic mod = Py.Import("mod")``, then
   you can call functions as normal, eg ``mod.func(args)``.
-  Use ``mod.func(args, Py.kw("keywordargname", keywordargvalue))`` or
   ``mod.func(args, keywordargname: keywordargvalue)`` to apply keyword
   arguments.
-  All python objects should be declared as ``dynamic`` type.
-  Mathematical operations involving python and literal/managed types
   must have the python object first, eg. ``np.pi * 2`` works,
   ``2 * np.pi`` doesn't.

Example
~~~~~~~

.. code-block:: csharp

   static void Main(string[] args)
   {
       using (Py.GIL())
       {
           dynamic np = Py.Import("numpy");
           Console.WriteLine(np.cos(np.pi * 2));

           dynamic sin = np.sin;
           Console.WriteLine(sin(5));

           double c = (double)(np.cos(5) + sin(5));
           Console.WriteLine(c);

           dynamic a = np.array(new List<float> { 1, 2, 3 });
           Console.WriteLine(a.dtype);

           dynamic b = np.array(new List<float> { 6, 5, 4 }, dtype: np.int32);
           Console.WriteLine(b.dtype);

           Console.WriteLine(a * b);
           Console.ReadKey();
       }
   }

Output:

.. code:: csharp

   1.0
   -0.958924274663
   -0.6752620892
   float64
   int32
   [  6.  10.  12.]



Resources
---------

Information on installation, FAQ, troubleshooting, debugging, and
projects using pythonnet can be found in the Wiki:

https://github.com/pythonnet/pythonnet/wiki

Mailing list
    https://mail.python.org/mailman/listinfo/pythondotnet
Chat
    https://gitter.im/pythonnet/pythonnet

.NET Foundation
---------------
This project is supported by the `.NET Foundation <https://dotnetfoundation.org>`_.

.. |Join the chat at https://gitter.im/pythonnet/pythonnet| image:: https://badges.gitter.im/pythonnet/pythonnet.svg
   :target: https://gitter.im/pythonnet/pythonnet?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge
.. |license shield| image:: https://img.shields.io/badge/license-MIT-blue.svg?maxAge=3600
   :target: ./LICENSE
.. |pypi package version| image:: https://img.shields.io/pypi/v/pythonnet.svg
   :target: https://pypi.python.org/pypi/pythonnet
.. |python supported shield| image:: https://img.shields.io/pypi/pyversions/pythonnet.svg
   :target: https://pypi.python.org/pypi/pythonnet
.. |stackexchange shield| image:: https://img.shields.io/badge/StackOverflow-python.net-blue.svg
   :target: http://stackoverflow.com/questions/tagged/python.net
.. |conda-forge version| image:: https://img.shields.io/conda/vn/conda-forge/pythonnet.svg
   :target: https://anaconda.org/conda-forge/pythonnet
.. |nuget preview shield| image:: https://img.shields.io/nuget/vpre/pythonnet
   :target: https://www.nuget.org/packages/pythonnet/
.. |nuget release shield| image:: https://img.shields.io/nuget/v/pythonnet
   :target: https://www.nuget.org/packages/pythonnet/
.. |gh shield| image:: https://github.com/pythonnet/pythonnet/workflows/GitHub%20Actions/badge.svg
   :target: https://github.com/pythonnet/pythonnet/actions?query=branch%3Amaster
