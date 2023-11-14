Embedding .NET into Python
==========================

Getting Started
---------------

A key goal for this project has been that Python.NET should “work just
the way you’d expect in Python”, except for cases that are .NET-specific
(in which case the goal is to work “just the way you’d expect in C#”).

A good way to start is to interactively explore .NET usage in python
interpreter by following along with the examples in this document. If
you get stuck, there are also a number of demos and unit tests located
in the source directory of the distribution that can be helpful as
examples. Additionally, refer to the `wiki on
GitHub <https://github.com/pythonnet/pythonnet/wiki>`__, especially the
**Tutorials** there.

Installation
~~~~~~~~~~~~

Python.NET is available as a source release on
`GitHub <https://github.com/pythonnet/pythonnet/releases>`__ and as a
platform-independent binary wheel or source distribution from the `Python
Package Index <https://pypi.python.org/pypi/pythonnet>`__.

Installing from PyPI can be done using ``pip install pythonnet``.

To build from source (either the ``sdist`` or clone or snapshot of the
repository), only the .NET6 SDK (or newer) and Python itself are required. If
``dotnet`` is on the ``PATH``, building can be done using

.. code:: bash

   python setup.py build


Loading a Runtime
~~~~~~~~~~~~~~~~~

All runtimes supported by clr-loader can be used, which are

Mono (``mono``)
    Default on Linux and macOS, supported on all platforms.

.NET Framework (``netfx``)
    Default on Windows and also only supported there. Must be at least version
    4.7.2.

.NET Core (``coreclr``)
    Self-contained is not supported, must be at least version 3.1.

The runtime must be configured **before** ``clr`` is imported, otherwise the
default runtime will be initialized and used. Information on the runtime in use
can be retrieved using :py:func:`pythonnet.get_runtime_info`).

A runtime can be selected in three different ways:

Calling ``pythonnet.load``
..........................

The function :py:func:`pythonnet.load` can be called explicitly. A single
string parameter (like ``load("coreclr")`` will select the respective runtime.
All keyword arguments are passed to the underlying
``clr_loader.get_<runtime-name>`` function.

.. code:: python

   from pythonnet import load

   load("coreclr", runtime_config="/path/to/runtimeconfig.json")

.. note::
   All runtime implementations can be initialized without additional parameters.
   While previous versions of ``clr_loader`` required a ``runtimeconfig.json``
   to load .NET Core, this requirement was lifted for the version used in
   ``pythonnet``.

Via Environment Variables
.........................

The same configurability is exposed as environment variables.

``PYTHONNET_RUNTIME``
    selects the runtime (e.g. ``PYTHONNET_RUNTIME=coreclr``)

``PYTHONNET_<RUNTIME>_<PARAM>``
    is passed on as a keyword argument (e.g. ``PYTHONNET_MONO_LIBMONO=/path/to/libmono.so``)

The equivalent configuration to the ``load`` example would be

.. code:: bash

   PYTHONNET_RUNTIME=coreclr
   PYTHONNET_CORECLR_RUNTIME_CONFIG=/path/to/runtimeconfig.json

.. note::
   Only string parameters are supported this way. It has the advantage, though,
   that the same configuration will be used for subprocesses as well.

Constructing a ``Runtime`` instance
...................................

The runtime can also be explicitly constructed using using the
``clr_loader.get_*`` factory functions, and then set up using
:py:func:`pythonnet.set_runtime`:

.. code:: python

   from pythonnet import set_runtime
   from clr_loader import get_coreclr

   rt = get_coreclr(runtime_config="/path/to/runtimeconfig.json")
   set_runtime(rt)

This method is only recommended, if very fine-grained control over the runtime
construction is required.


Importing Modules
~~~~~~~~~~~~~~~~~

Python.NET allows CLR namespaces to be treated essentially as Python
packages.

.. code:: python

   from System import String
   from System.Collections import *

Types from any loaded assembly may be imported and used in this manner.
To load an assembly, use the ``AddReference`` function in the ``clr``
module:

.. code:: python

   import clr
   clr.AddReference("System.Windows.Forms")
   from System.Windows.Forms import Form

.. note::
    Earlier releases of Python.NET relied on “implicit loading” to
    support automatic loading of assemblies whose names corresponded to an
    imported namespace. This is not supported anymore, all assemblies have to be
    loaded explicitly with ``AddReference``.

Python.NET uses the PYTHONPATH (``sys.path``) to look for assemblies to load, in
addition to the usual application base and the GAC (if applicable). To ensure
that you can import an assembly, put the directory containing the assembly in
``sys.path``.

Interacting with .NET
---------------------

Using Classes
~~~~~~~~~~~~~

Python.NET allows you to use any non-private classes, structs,
interfaces, enums or delegates from Python. To create an instance of a
managed class, you use the standard instantiation syntax, passing a set
of arguments that match one of its public constructors:

.. code:: python

   from System.Drawing import Point

   p = Point(5, 5)

In many cases, Python.NET can determine the correct constructor to call
automatically based on the arguments. In some cases, it may be necessary
to call a particular overloaded constructor, which is supported by a
special ``__overloads__`` attribute.

.. note::
   For compatibility with IronPython, the same functionality is available with
   the ``Overloads`` attribute.

.. code:: python

   from System import String, Char, Int32

   s = String.Overloads[Char, Int32]('A', 10)
   s = String.__overloads__[Char, Int32]('A', 10)

Using Generics
~~~~~~~~~~~~~~

Pythonnet also supports generic types. A generic type must be bound to
create a concrete type before it can be instantiated. Generic types
support the subscript syntax to create bound types:

.. code:: python

   from System.Collections.Generic import Dictionary
   from System import *

   dict1 = Dictionary[String, String]()
   dict2 = Dictionary[String, Int32]()
   dict3 = Dictionary[String, Type]()

.. note::
   For backwards-compatibility reasons, this will also work with some native
   Python types which are mapped to corresponding .NET types (in particular
   ``str -> System.String`` and ``int -> System.Int32``). Since these mappings
   are not really one-to-one and can lead to surprising results, use of this
   functionality is discouraged and will generate a warning in the future.

Managed classes can also be subclassed in Python, though members of the
Python subclass are not visible to .NET code. See the ``helloform.py``
file in the ``/demo`` directory of the distribution for a simple Windows
Forms example that demonstrates subclassing a managed class.

Fields and Properties
~~~~~~~~~~~~~~~~~~~~~

You can get and set fields and properties of CLR objects just as if they
were regular attributes:

.. code:: python

   from System import Environment

   name = Environment.MachineName
   Environment.ExitCode = 1

Using Indexers
~~~~~~~~~~~~~~

If a managed object implements one or more indexers, one can call the
indexer using standard Python indexing syntax:

.. code:: python

   from System.Collections import Hashtable

   table = Hashtable()
   table["key 1"] = "value 1"

Overloaded indexers are supported, using the same notation one would use
in C#:

.. code:: python

   items[0, 2]
   items[0, 2, 3]

Using Methods
~~~~~~~~~~~~~

Methods of CLR objects behave generally like normal Python methods.
Static methods may be called either through the class or through an
instance of the class. All public and protected methods of CLR objects
are accessible to Python:

.. code:: python

   from System import Environment

   drives = Environment.GetLogicalDrives()

It is also possible to call managed methods "unbound" (passing the
instance as the first argument) just as with Python methods. This is
most often used to explicitly call methods of a base class.

.. note::
    There is one caveat related to calling unbound methods: it is
    possible for a managed class to declare a static method and an instance
    method with the same name. Since it is not possible for the runtime to
    know the intent when such a method is called unbound, the static method
    will always be called.

The docstring of CLR a method (``__doc__``) can be used to view the
signature of the method, including overloads if the CLR method is
overloaded. You can also use the Python ``help`` method to inspect a
managed class:

.. code:: python

   from System import Environment

   print(Environment.GetFolderPath.__doc__)

   help(Environment)


Advanced Usage
--------------

Overloaded and Generic Methods
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

While Python.NET will generally be able to figure out the right version
of an overloaded method to call automatically, there are cases where it
is desirable to select a particular method overload explicitly.

Like constructors, all CLR methods have a ``__overloads__`` property to allow
selecting particular overloads explicitly.

.. note::
   For compatibility with IronPython, the same functionality is available with
   the ``Overloads`` attribute.

.. code:: python

   from System import Console, Boolean, String, UInt32

   Console.WriteLine.__overloads__[Boolean](True)
   Console.WriteLine.Overloads[String]("string")
   Console.WriteLine.__overloads__[UInt32](42)

Similarly, generic methods may be bound at runtime using the subscript
syntax directly on the method:

.. code:: python

   someobject.SomeGenericMethod[UInt32](10)
   someobject.SomeGenericMethod[String]("10")

Out and Ref parameters
~~~~~~~~~~~~~~~~~~~~~~

When a managed method has ``out`` or ``ref`` parameters, the arguments
appear as normal arguments in Python, but the return value of the method
is modified. There are 3 cases:

1. If the method is ``void`` and has one ``out`` or ``ref`` parameter,
   the method returns the value of that parameter to Python. For
   example, if ``someobject`` has a managed method with signature
   ``void SomeMethod1(out arg)``, it is called like so:

.. code:: python

   new_arg = someobject.SomeMethod1(arg)

where the value of ``arg`` is ignored, but its type is used for overload
resolution.

2. If the method is ``void`` and has multiple ``out``/``ref``
   parameters, the method returns a tuple containing the ``out``/``ref``
   parameter values. For example, if ``someobject`` has a managed method
   with signature ``void SomeMethod2(out arg, ref arg2)``, it is called
   like so:

.. code:: python

   new_arg, new_arg2 = someobject.SomeMethod2(arg, arg2)

3. Otherwise, the method returns a tuple containing the return value
   followed by the ``out``/``ref`` parameter values. For example:

.. code:: python

   found, new_value = dictionary.TryGetValue(key, value)

Delegates and Events
~~~~~~~~~~~~~~~~~~~~

Delegates defined in managed code can be implemented in Python. A
delegate type can be instantiated and passed a callable Python object to
get a delegate instance. The resulting delegate instance is a true
managed delegate that will invoke the given Python callable when it is
called:

.. code:: python

   def my_handler(source, args):
       print('my_handler called!')

   # instantiate a delegate
   d = AssemblyLoadEventHandler(my_handler)

   # use it as an event handler
   AppDomain.CurrentDomain.AssemblyLoad += d

Delegates with ``out`` or ``ref`` parameters can be implemented in
Python by following the convention described in `Out and Ref
parameters <#out-and-ref-parameters>`__.

Multicast delegates can be implemented by adding more callable objects
to a delegate instance:

.. code:: python

   d += self.method1
   d += self.method2
   d()

Events are treated as first-class objects in Python, and behave in many
ways like methods. Python callbacks can be registered with event
attributes, and an event can be called to fire the event.

Note that events support a convenience spelling similar to that used in
C#. You do not need to pass an explicitly instantiated delegate instance
to an event (though you can if you want). Events support the ``+=`` and
``-=`` operators in a way very similar to the C# idiom:

.. code:: python

   def handler(source, args):
       print('my_handler called!')

   # register event handler
   object.SomeEvent += handler

   # unregister event handler
   object.SomeEvent -= handler

   # fire the event
   result = object.SomeEvent(...)

Exception Handling
~~~~~~~~~~~~~~~~~~

Managed exceptions can be raised and caught in the same way as ordinary Python
exceptions:

.. code:: python

   from System import NullReferenceException

   try:
       raise NullReferenceException("aiieee!")
   except NullReferenceException as e:
       print(e.Message)
       print(e.Source)

Using Arrays
~~~~~~~~~~~~

The type ``System.Array`` supports the subscript syntax in order to make
it easy to create managed arrays from Python:

.. code:: python

   from System import Array, Int32

   myarray = Array[Int32](10)

Managed arrays support the standard Python sequence protocols:

.. code:: python

   items = SomeObject.GetArray()

   # Get first item
   v = items[0]
   items[0] = v

   # Get last item
   v = items[-1]
   items[-1] = v

   # Get length
   l = len(items)

   # Containment test
   test = v in items

Multidimensional arrays support indexing using the same notation one
would use in C#:

.. code:: python

   items[0, 2]

   items[0, 2, 3]

Using Collections
~~~~~~~~~~~~~~~~~

Managed arrays and managed objects that implement the ``IEnumerable`` or
``IEnumerable<T>`` interface can be iterated over using the standard iteration
Python idioms:

.. code:: python

   domain = System.AppDomain.CurrentDomain

   for item in domain.GetAssemblies():
       name = item.GetName()

Type Conversion
---------------

Type conversion under Python.NET is fairly straightforward - most
elemental Python types (string, int, long, etc.) convert automatically
to compatible managed equivalents (String, Int32, etc.) and vice-versa.

Custom type conversions can be implemented as :ref:`Codecs <codecs>`.

Types that do not have a logical equivalent in Python are exposed as
instances of managed classes or structs (System.Decimal is an example).

The .NET architecture makes a distinction between ``value types`` and
``reference types``. Reference types are allocated on the heap, and
value types are allocated either on the stack or in-line within an
object.

A process called ``boxing`` is used in .NET to allow code to treat a
value type as if it were a reference type. Boxing causes a separate copy
of the value type object to be created on the heap, which then has
reference type semantics.

Understanding boxing and the distinction between value types and
reference types can be important when using Python.NET because the
Python language has no value type semantics or syntax - in Python
“everything is a reference”.

Here is a simple example that demonstrates an issue. If you are an
experienced C# programmer, you might write the following code:

.. code:: python

   items = System.Array.CreateInstance(Point, 3)
   for i in range(3):
       items[i] = Point(0, 0)

   items[0].X = 1 # won't work!!

While the spelling of ``items[0].X = 1`` is the same in C# and Python,
there is an important and subtle semantic difference. In C# (and other
compiled-to-IL languages), the compiler knows that Point is a value type
and can do the Right Thing here, changing the value in place.

In Python however, “everything’s a reference”, and there is really no
spelling or semantic to allow it to do the right thing dynamically. The
specific reason that ``items[0]`` itself doesn’t change is that when you
say ``items[0]``, that getitem operation creates a Python object that
holds a reference to the object at ``items[0]`` via a GCHandle. That
causes a ValueType (like Point) to be boxed, so the following setattr
(``.X = 1``) *changes the state of the boxed value, not the original
unboxed value*.

The rule in Python is essentially:

   the result of any attribute or item access is a boxed value

and that can be important in how you approach your code.

Because there are no value type semantics or syntax in Python, you may
need to modify your approach. To revisit the previous example, we can
ensure that the changes we want to make to an array item aren’t “lost”
by resetting an array member after making changes to it:

.. code:: python

   items = System.Array.CreateInstance(Point, 3)
   for i in range(3):
       items[i] = Point(0, 0)

   # This _will_ work. We get 'item' as a boxed copy of the Point
   # object actually stored in the array. After making our changes
   # we re-set the array item to update the bits in the array.

   item = items[0]
   item.X = 1
   items[0] = item

This is not unlike some of the cases you can find in C# where you have
to know about boxing behavior to avoid similar kinds of ``lost update``
problems (generally because an implicit boxing happened that was not
taken into account in the code).

This is the same thing, just the manifestation is a little different in
Python. See the .NET documentation for more details on boxing and the
differences between value types and reference types.
