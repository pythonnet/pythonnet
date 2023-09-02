Embedding Python into .NET
==========================

.. warning::
    Because Python code running under Python.NET is inherently
    unverifiable, it runs totally under the radar of the security
    infrastructure of the CLR so you should restrict use of the Python
    assembly to trusted code.

The Python runtime assembly defines a number of public classes that
provide a subset of the functionality provided by the Python C-API.

These classes include PyObject, PyList, PyDict, PyTuple, etc.

At a very high level, to embed Python in your application one will need
to:

-  Reference ``Python.Runtime.dll`` (e.g. via a ``PackageReference``)
-  Call ``PythonEngine.Initialize()`` to initialize Python
-  Call ``var mod = PyModule.Import(name)`` to import a module as ``mod``

The module you import can either start working with your managed app
environment at the time its imported, or you can explicitly lookup and
call objects in a module you import.

For general-purpose information on embedding Python in applications, use
www.python.org or Google to find (C) examples. Because Python.NET is so
closely integrated with the managed environment, one will generally be
better off importing a module and deferring to Python code as early as
possible rather than writing a lot of managed embedding code.

.. note::
    Python is not free-threaded and uses a
    global interpreter lock to allow multi-threaded applications to interact
    safely with the Python interpreter. Much more information about this is
    available in the Python C-API documentation on the www.python.org
    Website.

When embedding Python in a managed application, one has to manage the
GIL in just the same way you would when embedding Python in a C or C++
application.

Before interacting with any of the objects or APIs provided by the
``Python.Runtime`` namespace, calling code must have acquired the Python
global interpreter lock by ``using'' ``Py.GIL()``. The only exception to
this rule is the ``PythonEngine.Initialize`` method, which may be called
at startup without having acquired the GIL. The GIL is released again
by disposing the return value of `Py.GIL()`:

.. code:: csharp

   using (Py.GIL())
   {
       PythonEngine.Exec("doStuff()");
   }
   
   // or
   {
       using var _ = Py.GIL()
       PythonEngine.Exec("doStuff()");
   }
   
   // or
   var gil = Py.GIL();
   try
   {
       PythonEngine.Exec("doStuff()");
   }
   finally
   {
       gil.Dispose();
   }

The ``Py.GIL()'' object is a thin wrapper over the unmanaged
``PyGILState_Ensure`` (on construction) and ``PyGILState_Release`` (on
disposal) functions from the Python API, and the documentation for those
APIs applies to the managed versions.

Passing C# Objects to the Python Engine
---------------------------------------

This section demonstrates how to pass a C# object to the Python runtime.
The example uses the following ``Person`` class:

.. code:: csharp

   public class Person
   {
       public Person(string firstName, string lastName)
       {
           FirstName = firstName;
           LastName = lastName;
       }

       public string FirstName { get; set; }
       public string LastName { get; set; }
   }

In order to pass a C# object to the Python runtime, it must be converted
to a ``PyObject``. This is done using the ``ToPython()`` extension
method. The ``PyObject`` may then be set as a variable in a ``PyScope``.
Code executed from the scope will have access to the variable:

.. code:: csharp

   // create a person object
   Person person = new Person("John", "Smith");

   // acquire the GIL before using the Python interpreter
   using (Py.GIL())
   {
       // create a Python scope
       using (PyModule scope = Py.CreateScope())
       {
           // convert the Person object to a PyObject
           PyObject pyPerson = person.ToPython();

           // create a Python variable "person"
           scope.Set("person", pyPerson);

           // the person object may now be used in Python
           string code = "fullName = person.FirstName + ' ' + person.LastName";
           scope.Exec(code);
       }
   }
