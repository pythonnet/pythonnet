pythonnet - Python for .NET
===========================

|Join the chat at https://gitter.im/pythonnet/pythonnet|

|appveyor shield| |travis shield| |codecov shield|

|license shield| |pypi package version| |python supported shield|
|stackexchange shield|

Python for .NET is a package that gives Python programmers nearly
seamless integration with the .NET Common Language Runtime (CLR) and
provides a powerful application scripting tool for .NET developers. It
allows Python code to interact with the CLR, and may also be used to
embed Python into a .NET application.

Calling .NET code from Python
-----------------------------

Python for .NET allows CLR namespaces to be treated essentially as
Python packages.

.. code-block::

   import clr
   from System import String
   from System.Collections import *

To load an assembly, use the ``AddReference`` function in the ``clr``
module:

.. code-block::

   import clr
   clr.AddReference("System.Windows.Forms")
   from System.Windows.Forms import Form

Embedding Python in .NET
------------------------

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
   ``2 * np.pi`` doesn’t.

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

           double c = np.cos(5) + sin(5);
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

.. code::

   1.0
   -0.958924274663
   -0.6752620892
   float64
   int32
   [  6.  10.  12.]

Information on installation, FAQ, troubleshooting, debugging, and
projects using pythonnet can be found in the Wiki:

https://github.com/pythonnet/pythonnet/wiki

.. |Join the chat at https://gitter.im/pythonnet/pythonnet| image:: https://badges.gitter.im/pythonnet/pythonnet.svg
   :target: https://gitter.im/pythonnet/pythonnet?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge
.. |appveyor shield| image:: https://img.shields.io/appveyor/ci/pythonnet/pythonnet/master.svg?label=AppVeyor
   :target: https://ci.appveyor.com/project/pythonnet/pythonnet/branch/master
.. |travis shield| image:: https://img.shields.io/travis/pythonnet/pythonnet/master.svg?label=Travis
   :target: https://travis-ci.org/pythonnet/pythonnet
.. |codecov shield| image:: https://img.shields.io/codecov/c/github/pythonnet/pythonnet/master.svg?label=Codecov
   :target: https://codecov.io/github/pythonnet/pythonnet
.. |license shield| image:: https://img.shields.io/badge/license-MIT-blue.svg?maxAge=3600
   :target: ./LICENSE
.. |pypi package version| image:: https://img.shields.io/pypi/v/pythonnet.svg
   :target: https://pypi.python.org/pypi/pythonnet
.. |python supported shield| image:: https://img.shields.io/pypi/pyversions/pythonnet.svg
   :target: https://pypi.python.org/pypi/pythonnet
.. |stackexchange shield| image:: https://img.shields.io/badge/StackOverflow-python.net-blue.svg
   :target: http://stackoverflow.com/questions/tagged/python.net
