# Changelog

All notable changes to Python.NET will be documented in this file. This
project adheres to [Semantic Versioning][].

This document follows the conventions laid out in [Keep a CHANGELOG][].

## [Unreleased][]

### Added

### Changed

### Fixed

- Fixed error occuring when inheriting a class containing a virtual generic method.

## [3.0.1](https://github.com/pythonnet/pythonnet/releases/tag/v3.0.1) - 2022-11-03

### Added

-   Support for Python 3.11

### Changed

-   Allow decoders to override conversion of types derived from primitive types

### Fixed

-   Fixed objects leaking when Python attached event handlers to them even if they were later removed
-   Fixed `PyInt` conversion to `BigInteger` and `System.String` produced incorrect result for values between 128 and 255.
-   Fixed implementing a generic interface with a Python class


## [3.0.0](https://github.com/pythonnet/pythonnet/releases/tag/v3.0.0) - 2022-09-29

### Added

-   Ability to instantiate new .NET arrays using `Array[T](dim1, dim2, ...)` syntax
-   Python operator method will call C# operator method for supported binary and unary operators ([#1324][p1324]).
-   Add GetPythonThreadID and Interrupt methods in PythonEngine
-   Ability to implement delegates with `ref` and `out` parameters in Python, by returning the modified parameter values in a tuple. ([#1355][i1355])
-   Ability to override .NET methods that have `out` or `ref` in Python by returning the modified parameter values in a tuple. ([#1481][i1481])
-   `PyType` - a wrapper for Python type objects, that also permits creating new heap types from `TypeSpec`
-    Improved exception handling:
  *   exceptions can now be converted with codecs
  *   `InnerException` and `__cause__` are propagated properly
-   `__name__` and `__signature__` to reflected .NET methods
-   .NET collection types now implement standard Python collection interfaces from `collections.abc`.
See [Mixins/collections.py](src/runtime/Mixins/collections.py).
-   you can cast objects to generic .NET interfaces without specifying generic arguments as long as there is no ambiguity.
-   .NET arrays implement Python buffer protocol
-   Python integer interoperability with `System.Numerics.BigInteger`
-   Python.NET will correctly resolve .NET methods, that accept `PyList`, `PyInt`,
and other `PyObject` derived types when called from Python.
-   .NET classes, that have `__call__` method are callable from Python
-   `PyIterable` type, that wraps any iterable object in Python
-   `PythonEngine` properties for supported Python versions: `MinSupportedVersion`, `MaxSupportedVersion`, and `IsSupportedVersion`
-   The runtime that is loaded on `import clr` can now be configured via environment variables


### Changed
-   Drop support for Python 2, 3.4, 3.5, and 3.6
-   `wchar_t` size aka `Runtime.UCS` is now determined at runtime
-   `clr.AddReference` may now throw errors besides `FileNotFoundException`, that provide more
details about the cause of the failure
-   `clr.AddReference` no longer adds ".dll" implicitly
-   `PyIter(PyObject)` constructor replaced with static `PyIter.GetIter(PyObject)` method
-   Python runtime can no longer be shut down if the Python error indicator is set, as it would have unpredictable behavior
-   BREAKING: Return values from .NET methods that return an interface are now automatically
     wrapped in that interface. This is a breaking change for users that rely on being
     able to access members that are part of the implementation class, but not the
     interface.  Use the new `__implementation__` or `__raw_implementation__` properties to
     if you need to "downcast" to the implementation class.
-   BREAKING: `==` and `!=` operators on `PyObject` instances now use Python comparison
     (previously was equivalent to `object.ReferenceEquals(,)`)
-   BREAKING: Parameters marked with `ParameterAttributes.Out` are no longer returned in addition
     to the regular method return value (unless they are passed with `ref` or `out` keyword).
-   BREAKING: Drop support for the long-deprecated CLR.* prefix.
-   `PyObject` now implements `IEnumerable<PyObject>` in addition to `IEnumerable`
-   floating point values passed from Python are no longer silently truncated
when .NET expects an integer [#1342][i1342]
-   More specific error messages for method argument mismatch
-   members of `PyObject` inherited from `System.Object and `DynamicObject` now autoacquire GIL
-   BREAKING: when inheriting from .NET types in Python if you override `__init__` you
must explicitly call base constructor using `super().__init__(.....)`. Not doing so will lead
to undefined behavior.
-   BREAKING: most `PyScope` methods will never return `null`. Instead, `PyObject` `None` will be returned.
-   BREAKING: `PyScope` was renamed to `PyModule`
-   BREAKING: Methods with `ref` or `out` parameters and void return type return a tuple of only the `ref` and `out` parameters.
-   BREAKING: to call Python from .NET `Runtime.PythonDLL` property must be set to Python DLL name
or the DLL must be loaded in advance. This must be done before calling any other Python.NET functions.
-   BREAKING: `PyObject.Length()` now raises a `PythonException` when object does not support a concept of length.
-   BREAKING: disabled implicit conversion from C# enums to Python `int` and back.
One must now either use enum members (e.g. `MyEnum.Option`), or use enum constructor
(e.g. `MyEnum(42)` or `MyEnum(42, True)` when `MyEnum` does not have a member with value 42).
-   BREAKING: disabled implicit conversion from Python objects implementing sequence protocol to
.NET arrays when the target .NET type is `System.Object`. The conversion is still attempted when the
target type is a `System.Array`.
-   Sign Runtime DLL with a strong name
-   Implement loading through `clr_loader` instead of the included `ClrModule`, enables
    support for .NET Core
-   BREAKING: .NET and Python exceptions are preserved when crossing Python/.NET boundary
-   BREAKING: custom encoders are no longer called for instances of `System.Type`
-   `PythonException.Restore` no longer clears `PythonException` instance.
-   Replaced the old `__import__` hook hack with a PEP302-style Meta Path Loader
-   BREAKING: Names of .NET types (e.g. `str(__class__)`) changed to better support generic types
-   BREAKING: overload resolution will no longer prefer basic types. Instead, first matching overload will
be chosen.
-   BREAKING: acquiring GIL using `Py.GIL` no longer forces `PythonEngine` to initialize
-   BREAKING: `Exec` and `Eval` from `PythonEngine` no longer accept raw pointers.
-   BREAKING: .NET collections and arrays are no longer automatically converted to
Python collections. Instead, they implement standard Python
collection interfaces from `collections.abc`.
See [Mixins/collections.py](src/runtime/Mixins/collections.py).
-   BREAKING: When trying to convert Python `int` to `System.Object`, result will
be of type `PyInt` instead of `System.Int32` due to possible loss of information.
Python `float` will continue to be converted to `System.Double`.
-   BREAKING: Python.NET will no longer implicitly convert types like `numpy.float64`, that implement `__float__` to
`System.Single` and `System.Double`. An explicit conversion is required on Python or .NET side.
-   BREAKING: `PyObject.GetHashCode` can fail.
-   BREAKING: Python.NET will no longer implicitly convert any Python object to `System.Boolean`.
-   BREAKING: `PyObject.GetAttr(name, default)` now only ignores `AttributeError` (previously ignored all exceptions).
-   BREAKING: `PyObject` no longer implements `IEnumerable<PyObject>`.
Instead, `PyIterable` does that.
-   BREAKING: `IPyObjectDecoder.CanDecode` `objectType` parameter type changed from `PyObject` to `PyType`

### Fixed

-   Fix incorrect dereference of wrapper object in `tp_repr`, which may result in a program crash
-   Fixed parameterless .NET constructor being silently called when a matching constructor overload is not found ([#238][i238])
-   Fix incorrect dereference in params array handling
-   Fixes issue with function resolution when calling overloaded function with keyword arguments from python ([#1097][i1097])
-   Fix `object[]` parameters taking precedence when should not in overload resolution
-   Fixed a bug where all .NET class instances were considered Iterable
-   Fix incorrect choice of method to invoke when using keyword arguments.
-   Fix non-delegate types incorrectly appearing as callable.
-   Indexers can now be used with interface objects
-   Fixed a bug where indexers could not be used if they were inherited
-   Made it possible to use `__len__` also on `ICollection<>` interface objects
-   Fixed issue when calling PythonException.Format where another exception would be raise for unnormalized exceptions
-   Made it possible to call `ToString`, `GetHashCode`, and `GetType` on inteface objects
-   Fixed objects returned by enumerating `PyObject` being disposed too soon
-   Incorrectly using a non-generic type with type parameters now produces a helpful Python error instead of throwing NullReferenceException
-   `import` may now raise errors with more detail than "No module named X"
-   Exception stacktraces on `PythonException.StackTrace` are now properly formatted
-   Providing an invalid type parameter to a generic type or method produces a helpful Python error
-   Empty parameter names (as can be generated from F#) do not cause crashes
-   Unicode strings with surrogates were truncated when converting from Python
-   `Reload` mode now supports generic methods (previously Python would stop seeing them after reload)
-   Temporarily fixed issue resolving method overload when method signature has `out` parameters ([#1672](i1672))
-   Decimal default parameters are now correctly taken into account

### Removed

-   `ShutdownMode` has been removed. The only shutdown mode supported now is an equivalent of `ShutdownMode.Reload`.
There is no need to specify it.
-   implicit assembly loading (you have to explicitly `clr.AddReference` before doing import)
-   messages in `PythonException` no longer start with exception type
-   `PyScopeManager`, `PyScopeException`, `PyScope` (use `PyModule` instead)
-   support for .NET Framework 4.0-4.6; Mono before 5.4. Python.NET now requires .NET Standard 2.0
(see [the matrix](https://docs.microsoft.com/en-us/dotnet/standard/net-standard#net-implementation-support))

## [2.5.2](https://github.com/pythonnet/pythonnet/releases/tag/v2.5.2) - 2021-02-05

Bugfix release.

### Fixed
-   Fix `object[]` parameters taking precedence when should not in overload resolution
-   Empty parameter names (as can be generated from F#) do not cause crashes

## [2.5.1](https://github.com/pythonnet/pythonnet/releases/tag/v2.5.1) - 2020-06-18

Bugfix release.

### Fixed

-    Fix incorrect dereference of wrapper object in `tp_repr`, which may result in a program crash
-    Fix incorrect dereference in params array handling

## [2.5.0](https://github.com/pythonnet/pythonnet/releases/tag/v2.5.0) - 2020-06-14

This version improves performance on benchmarks significantly compared to 2.3.

### Added

-   Automatic NuGet package generation in appveyor and local builds
-   Function that sets `Py_NoSiteFlag` to 1.
-   Support for Jetson Nano.
-   Support for `__len__` for .NET classes that implement ICollection
-   `PyExport` attribute to hide .NET types from Python
-   `PythonException.Format` method to format exceptions the same as
    `traceback.format_exception`
-   `Runtime.None` to be able to pass `None` as parameter into Python from .NET
-   `PyObject.IsNone()` to check if a Python object is None in .NET.
-   Support for Python 3.8
-   Codecs as the designated way to handle automatic conversions between
    .NET and Python types
-   Added Python 3 buffer api support and PyBuffer interface for fast byte and numpy array read/write ([#980][p980])

### Changed

-   Added argument types information to "No method matches given arguments" message
-   Moved wheel import in setup.py inside of a try/except to prevent pip collection failures
-   Removes `PyLong_GetMax` and `PyClass_New` when targetting Python3
-   Improved performance of calls from Python to C#
-   Added support for converting python iterators to C# arrays
-   Changed usage of the obsolete function
    `GetDelegateForFunctionPointer(IntPtr, Type)` to
    `GetDelegateForFunctionPointer<TDelegate>(IntPtr)`
-   When calling C# from Python, enable passing argument of any type to a
    parameter of C# type `object` by wrapping it into `PyObject` instance.
    ([#881][i881])
-   Added support for kwarg parameters when calling .NET methods from Python
-   Changed method for finding MSBuild using vswhere
-   Reworked `Finalizer`. Now objects drop into its queue upon finalization,
    which is periodically drained when new objects are created.
-   Marked `Runtime.OperatingSystemName` and `Runtime.MachineName` as
    `Obsolete`, should never have been `public` in the first place. They also
    don't necessarily return a result that matches the `platform` module's.
-   Unconditionally depend on `pycparser` for the interop module generation

### Fixed

-   Fixed runtime that fails loading when using pythonnet in an environment
    together with Nuitka
-   Fixes bug where delegates get casts (dotnetcore)
-   Determine size of interpreter longs at runtime
-   Handling exceptions ocurred in ModuleObject's getattribute
-   Fill `__classcell__` correctly for Python subclasses of .NET types
-   Fixed issue with params methods that are not passed an array.
-   Use UTF8 to encode strings passed to `PyRun_String` on Python 3

## [2.4.0][] - 2019-05-15

### Added

-   Added support for embedding python into dotnet core 2.0 (NetStandard 2.0)
-   Added new build system (pythonnet.15.sln) based on dotnetcore-sdk/xplat(crossplatform msbuild).
    Currently there two side-by-side build systems that produces the same output (net40) from the same sources.
    After a some transition time, current (mono/ msbuild 14.0) build system will be removed.
-   NUnit upgraded to 3.7 (eliminates travis-ci random bug)
-   Added C# `PythonEngine.AddShutdownHandler` to help client code clean up on shutdown.
-   Added `clr.GetClrType` ([#432][i432])([#433][p433])
-   Allowed passing `None` for nullable args ([#460][p460])
-   Added keyword arguments based on C# syntax for calling CPython methods ([#461][p461])
-   Catches exceptions thrown in C# iterators (yield returns) and rethrows them in python ([#475][i475])([#693][p693])
-   Implemented GetDynamicMemberNames() for PyObject to allow dynamic object members to be visible in the debugger ([#443][i443])([#690][p690])
-   Incorporated reference-style links to issues and pull requests in the CHANGELOG ([#608][i608])
-   Added PyObject finalizer support, Python objects referred by C# can be auto collect now ([#692][p692]).
-   Added detailed comments about aproaches and dangers to handle multi-app-domains ([#625][p625])
-   Python 3.7 support, builds and testing added. Defaults changed from Python 3.6 to 3.7 ([#698][p698])
-   Added support for C# types to provide `__repr__` ([#680][p680])

### Changed

-   PythonException included C# call stack
-   Reattach python exception traceback information (#545)
-   PythonEngine.Intialize will now call `Py_InitializeEx` with a default value of 0, so signals will not be configured by default on embedding. This is different from the previous behaviour, where `Py_Initialize` was called instead, which sets initSigs to 1. ([#449][i449])
-   Refactored MethodBinder.Bind in preparation to make it extensible (#829)
-   Look for installed Windows 10 sdk's during installation instead of relying on specific versions.
-   Remove `LoadLibrary` call. ([#880][p880])

### Fixed

-   Fixed secondary PythonEngine.Initialize call, all sensitive static variables now reseted.
    This is a hidden bug. Once python cleaning up enough memory, objects from previous engine run becomes corrupted. ([#534][p534])
-   Fixed Visual Studio 2017 compat ([#434][i434]) for setup.py
-   Fixed crashes when integrating pythonnet in Unity3d ([#714][i714]),
    related to unloading the Application Domain
-   Fixed interop methods with Py_ssize_t. NetCoreApp 2.0 is more sensitive than net40 and requires this fix. ([#531][p531])
-   Fixed crash on exit of the Python interpreter if a python class
    derived from a .NET class has a `__namespace__` or `__assembly__`
    attribute ([#481][i481])
-   Fixed conversion of 'float' and 'double' values ([#486][i486])
-   Fixed 'clrmethod' for python 2 ([#492][i492])
-   Fixed double calling of constructor when deriving from .NET class ([#495][i495])
-   Fixed `clr.GetClrType` when iterating over `System` members ([#607][p607])
-   Fixed `LockRecursionException` when loading assemblies ([#627][i627])
-   Fixed errors breaking .NET Remoting on method invoke ([#276][i276])
-   Fixed PyObject.GetHashCode ([#676][i676])
-   Fix memory leaks due to spurious handle incrementation ([#691][i691])
-   Fix spurious assembly loading exceptions from private types ([#703][i703])
-   Fix inheritance of non-abstract base methods ([#755][i755])


## [2.3.0][] - 2017-03-11

### Added

-   Added Code Coverage ([#345][p345])
-   Added `PySys_SetArgvEx` ([#347][p347])
-   Added XML Documentation ([#349][p349])
-   Added `Embedded_Tests` on AppVeyor ([#224][i224])([#353][p353])
-   Added `Embedded_Tests` on Travis ([#224][i224])([#391][p391])
-   Added PY3 settings to solution configuration-manager ([#346][p346])
-   Added `Slack` ([#384][p384])([#383][i383])([#386][p386])
-   Added function of passing an arbitrary .NET object as the value
    of an attribute of `PyObject` ([#370][i370])([#373][p373])
-   Added `Coverity scan` ([#390][i390])
-   Added `bumpversion` for version control ([#319][i319])([#398][p398])
-   Added `tox` for local testing ([#345][p345])
-   Added `requirements.txt`
-   Added to `PythonEngine` methods `Eval` and `Exec` ([#389][p389])
-   Added implementations of `ICustomMarshal` ([#407][p407])
-   Added docker images ([#322][i322])
-   Added hooks in `pyinstaller` and `cx_freeze` for `pythonnet` ([#66][i66])

### Changed

-   Refactored python `unittests` ([#329][p329])
-   Refactored python `setup.py` ([#337][p337])
-   Refactored remaining of Build Directives on `runtime.cs` ([#339][p339])
-   Refactored `Embedded_Tests` to make easier to write tests ([#369][p369])
-   Changed `unittests` to `pytest` ([#368][p368])
-   Upgraded NUnit framework from `2.6.3` to `3.5.0` ([#341][p341])
-   Downgraded NUnit framework from `3.5.0` to `2.6.4` ([#353][p353])
-   Upgraded NUnit framework from `2.6.4` to `3.6.0` ([#371][p371])
-   Unfroze Mono version on Travis ([#345][p345])
-   Changed `conda.recipe` build to only pull-requests ([#345][p345])
-   Combine `Py_DEBUG` and `PYTHON_WITH_PYDEBUG` flags ([#362][i362])

### Deprecated

-   Deprecated `RunString` ([#401][i401])

### Fixed

-   Fixed crash during Initialization ([#262][i262])([#343][p343])
-   Fixed crash during Shutdown ([#365][p365])
-   Fixed multiple build warnings
-   Fixed method signature match for Object Type ([#203][i203])([#377][p377])
-   Fixed outdated version number in AssemblyInfo ([#398][p398])
-   Fixed wrong version number in `conda.recipe` ([#398][p398])
-   Fixed fixture location for Python tests and `Embedded_Tests`
-   Fixed `PythonException` crash during Shutdown ([#400][p400])
-   Fixed `AppDomain` unload during GC ([#397][i397])([#400][p400])
-   Fixed `Py_Main` & `PySys_SetArgvEx` `no mem error` on `UCS4/PY3` ([#399][p399])
-   Fixed `Python.Runtime.dll.config` on macOS ([#120][i120])
-   Fixed crash on `PythonEngine.Version` ([#413][i413])
-   Fixed `PythonEngine.PythonPath` issues ([#179][i179])([#414][i414])([#415][p415])
-   Fixed missing information on 'No method matches given arguments' by adding the method name

### Removed

-   Removed `six` dependency for `unittests` ([#329][p329])
-   Removed `Mono.Unix` dependency for `UCS4` ([#360][p360])
-   Removed need for `Python.Runtime.dll.config`
-   Removed PY32 build option `PYTHON_WITH_WIDE_UNICODE` ([#417][i417])

## [2.2.2][] - 2017-01-29

### Fixed

-   Missing files from packaging ([#336][i336])

## [2.2.1][] - 2017-01-26

-   `v2.2.0` had a release issue on PyPi. Bumped to `v2.2.1`

### Added

-   Python 3.6 support ([#310][p310])
-   Added `__version__` to module ([#312][p312])
-   Added `conda` recipe ([#281][p281])
-   Nuget update on build ([#268][p268])
-   Added `__cause__` attribute on exception ([#287][p287])

### Changed

-   License to MIT ([#314][p314])
-   Project clean-up ([#320][p320])
-   Refactor `#if` directives
-   Rename Decref/Incref to XDecref/XIncre ([#275][p275])
-   Remove printing if Decref is called with NULL ([#275][p275])

### Removed

-   Python 2.6 support ([#270][i270])
-   Python 3.2 support ([#270][i270])

### Fixed

-   Fixed `isinstance` refcount_leak ([#273][p273])
-   Comparison Operators ([#294][p294])
-   Improved Linux support ([#300][p300])
-   Exception pickling ([#286][p286])

## [2.2.0-dev1][] - 2016-09-19

### Changed

-   Switch to C# 6.0 ([#219][p219])
-   `setup.py` improvements for locating build tools ([#208][p208])
-   unmanaged exports updated ([#206][p206])
-   Mono update pinned to 4.2.4.4 ([#233][p233])

### Fixed

-   Fixed relative imports ([#219][p219])
-   Fixed recursive types ([#250][p250])
-   Demo fix - stream reading ([#225][p225])

## [2.1.0][] - 2016-04-12

### Added

-   Added Python 3.2 support. ([#78][p78])
-   Added Python 3.3 support. ([#78][p78])
-   Added Python 3.4 support. ([#78][p78])
-   Added Python 3.5 support. ([#163][p163])
-   Managed types can be sub-classed in Python ([#78][p78])
-   Uses dynamic objects for cleaner code when embedding Python ([#78][p78])

### Changed

-   Better Linux support (with or without --enable-shared option) ([#78][p78])

### Removed

-   Implicit Type Casting ([#131][i131])

## [2.0.0][] - 2015-06-26

-   Release

## 2.0.0-alpha.2

### Changed

-   First work on Python 2.5 compatibility. The destination version can be
    set by defining PYTHON24 or PYTHON25. Python 2.6 compatibility is in
    work.

-   Added VS 2005 solution and project files including a UnitTest
    configuration which runs the unit test suite.

-   Enhanced unit test suite. All test cases are combined in a single
    test suite now.

-   Fixed bugs in generics support for all Python versions.

-   Fixed exception bugs for Python 2.5+. When compiled for Python 2.5+ all
    managed exceptions are based on Python's `exceptions.Exception` class.

-   Added deprecation warnings for importing from `CLR.*` and the CLR module.

-   Implemented support for methods with variable arguments
    `spam(params object[] egg)`

-   Fixed Mono support by adding a custom marshaler for UCS-4 unicode,
    fixing a some ref counter bugs and creating a new makefile.mono.

-   Added a standard python extension to load the clr environment.
    The `src/monoclr/` directory contains additional sample code like a
    Python binary linked against `libpython2.x.so` and some example code
    how to embed Mono and PythonNet in a C application.

-   Added yet another python prompt. This time it's a C application that
    embedds both Python and Mono. It may be useful as an example app for
    others and I need it to debug a nasty bug.

-   Implemented `ModuleFunctionAttribute` and added
    `ForbidPythonThreadsAttribute`. The latter is required for module
    functions which invoke Python methods.

-   Added `clr.setPreload()`, `clr.getPreload()`,
    `clr.AddReference("assembly name")`, `clr.FindAssembly("name")`
    and `clr.ListAssemblies(verbose)`. Automatic preloading can be enabled
    with clr.setPreload/True). Preloading is automatically enabled for
    interactive Python shells and disabled in all other cases.

-   New Makefile that works for Windows and Mono and autodetects the Python
    version and UCS 2/4 setting.

-   Added code for Python 2.3. PythonNet can be build for Python 2.3 again
    but it is not fully supported.

-   Changed the PythonException.Message value so it displays the name of
    the exception class `Exception` instead of its representation
    `<type 'exceptions.Exception'>`.

-   Added `Python.Runtime.dll.config`.

## 2.0.0-alpha.1

### Changed

-   Moved the Python for .NET project to Sourceforge and moved version
    control to Subversion.

-   Removed `CallConvCdecl` attributes and the IL hack that they supported.
    .NET 2.x now supports `UnmanagedFunctionPointer`, which does the right
    thing without the hackery required in 1.x. This removes a dependency
    on ILASM to build the package and better supports Mono (in theory).

-   Refactored import and assembly management machinery. The old `CLR.`
    syntax for import is deprecated, but still supported until 3.x. The
    recommended style now is to use `from System import xxx`, etc. We
    also now support `from X import *` correctly.

-   Implemented a (lowercase) `clr` module to match IronPython for code
    compatibility. Methods of this module should be used to explicitly
    load assemblies. Implicit (name-based) assembly loading will still
    work until 3.x, but it is deprecated.

-   Implemented support for generic types and generic methods using the
    same patterns and syntax as IronPython. See the documentation for
    usage details.

-   Many small and large performance improvements, switched to generic
    collections for some internals, better algorithms for assembly
    scanning, etc.

-   Fixed an unboxing issue in generated delegate implementation code
    that affected delegates that return value types.

## [1.0.0][] - 2006-04-08

### Changed

-   Backported the refactored import and assembly management from the 2.x
    line, mainly to improve the possibility of code-compatibility with
    IronPython.

## 1.0.0-rc.2

### Changed

-   Changed some uses of Finalize as a static method name that confused the
    Mono compiler and people reading the code. Note that this may be a
    breaking change if anyone was calling `PythonEngine.Finalize()`. If so,
    you should now use `PythonEngine.Shutdown()`.

-   Tweaked assembly lookup to ensure that assemblies can be found in the
    current working directory, even after changing directories using things
    like `os.chdir()` from Python.

-   Fixed some incorrect finalizers (thanks to Greg Chapman for the report)
    that may have caused some threading oddities.

-   Tweaked support for out and ref parameters. If a method has a return
    type of void and a single ref or out parameter, that parameter will be
    returned as the result of the method. This matches the current behavior
    of IronPython and makes it more likely that code can be moved between
    Python for .NET and IP in the future.

-   Refactored part of the assembly manager to remove a potential case of
    thread-deadlock in multi-threaded applications.

-   Added a `__str__` method to managed exceptions that returns the Message
    attribute of the exception and the StackTrace (if available).

## 1.0.0-rc.1

### Changed

-   Implemented a workaround for the fact that exceptions cannot be new-style
    classes in the CPython interpreter. Managed exceptions can now be raised
    and caught naturally from Python (hooray!)

-   Implemented support for invoking methods with out and ref parameters.
    Because there is no real equivalent to these in Python, methods that
    have out or ref parameters will return a tuple. The tuple will contain
    the result of the method as its first item, followed by out parameter
    values in the order of their declaration in the method signature.

-   Fixed a refcount problem that caused a crash when CLR was imported in
    an existing installed Python interpreter.

-   Added an automatic conversion from Python strings to `byte[]`. This makes
    it easier to pass `byte[]` data to managed methods (or set properties,
    etc.) as a Python string without having to write explicit conversion
    code. Also works for sbyte arrays. Note that `byte` and `sbyte` arrays
    returned from managed methods or obtained from properties or fields
    do _not_ get converted to Python strings - they remain instances of
    `Byte[]` or `SByte[]`.

-   Added conversion of generic Python sequences to object arrays when
    appropriate (thanks to Mackenzie Straight for the patch).

-   Added a bit of cautionary documentation for embedders, focused on
    correct handling of the Python global interpreter lock from managed
    code for code that calls into Python.

-   `PyObject.FromManagedObject` now correctly returns the Python None object
    if the input is a null reference. Also added a new `AsManagedObject`
    method to `PyObject`, making it easier to convert a Python-wrapped managed
    object to the real managed object.

-   Created a simple installer for windows platforms.

## 1.0.0-beta.5

### Changed

-   Refactored and fixed threading and global interpreter lock handling,
    which was badly broken before. Also added a number of threading and
    GIL-handling tests.

-   Related to the GIL fixes, added a note to embedders in the README
    about using the AcquireLock and ReleaseLock methods of the PythonEngine
    class to manage the GIL.

-   Fixed a problem in `Single <--> float` conversion for cultures that use
    different decimal symbols than Python.

-   Added a new `ReloadModule` method to the `PythonEngine` class that hooks
    Python module reloading (`PyImport_ReloadModule`).

-   Added a new `StringAsModule` method to the PythonEngine class that can
    create a module from a managed string of code.

-   Added a default `__str__` implementation for Python wrappers of managed
    objects that calls the `ToString` method of the managed object.

## 1.0.0-beta.4

### Changed

-   Fixed a problem that made it impossible to override "special" methods
    like `__getitem__` in subclasses of managed classes. Now the tests all
    pass, and there is much rejoicing.

-   Managed classes reflected to Python now have an `__doc__` attribute that
    contains a listing of the class constructor signatures.

-   Fixed a problem that prevented passing null (None) for array arguments.

-   Added a number of new argument conversion tests. Thanks to Laurent
    Caumont for giving Python for .NET a good workout with managed DirectX.

-   Updated the bundled C Python runtime and libraries to Python 2.4. The
    current release is known to also run with Python 2.3. It is known
    _not_ to work with older versions due to changes in CPython type
    object structure.

-   Mostly fixed the differences in the way that import works depending
    on whether you are using the bundled interpreter or an existing Python
    interpreter. The hack I used makes import work uniformly for imports
    done in Python modules. Unfortunately, there is still a limitation
    when using the interpreter interactively: you need to do `import CLR`
    first before importing any sub-names when running with an existing
    Python interpreter.

    The reason is that the first import of `CLR` installs the CLR import
    hook, but for an existing interpreter the standard importer is still
    in control for the duration of that first import, so sub-names won't
    be found until the next import, which will use the now-installed hook.

-   Added support to directly iterate over objects that support IEnumerator
    (as well as IEnumerable). Thanks to Greg Chapman for prodding me ;)

-   Added a section to the README dealing with rebuilding Python for .NET
    against other CPython versions.

-   Fixed a problem with accessing properties when only the interface for
    an object is known. For example, `ICollection(ob).Count` failed because
    Python for .NET mistakenly decided that Count was abstract.

-   Fixed some problems with how COM-based objects are exposed and how
    members of inherited interfaces are exposed. Thanks to Bruce Dodson
    for patches on this.

-   Changed the Runtime class to use a const string to target the
    appropriate CPython dll in DllImport attributes. Now you only
    have to change one line to target a new Python version.

## 1.0.0-beta.3

### Changed

-   A dumb bug that could cause a crash on startup on some platforms was
    fixed. Decided to update the beta for this, as a number of people
    were running into the problem.

## 1.0.0-beta.2

### Changed

-   Exceptions raised as a result of getting or setting properties were
    not very helpful (target invokation exception). This has been changed
    to pass through the inner exception the way that methods do, which is
    much more likely to be the real exception that caused the problem.

-   Events were refactored as the implementation was based on some bad
    assumptions. As a result, subscription and unsubscription now works
    correctly. A change from beta 1 is that event objects are no longer
    directly callable - this was not appropriate, since the internal
    implementation of an event is private and cant work reliably. Instead,
    you should the appropriate `OnSomeEvent` method published by a class
    to fire an event.

-   The distribution did not include the key file, making it a pain for
    people to build from source. Added the key file to the distribution
    buildout for beta 2.

-   Assemblies can now be found and loaded if they are on the PYTHONPATH.
    Previously only the appbase and the GAC were checked. The system now
    checks PYTHONPATH first, then the appbase, then the GAC.

-   Fixed a bug in constructor invokation during object instantiation.

## 1.0.0-beta.1

### Changed

-   Added the baseline of the managed embedding API. Some of the details
    are still subject to change based on some real-world use and feedback.

    The embedding API is based on the `PyObject` class, along with a number
    of specific `PyDict`, `PyList`, (etc.) classes that expose the respective
    interfaces of the built-in Python types. The basic structure and usage
    is intended be familar to anyone who has used Python / C++ wrapper
    libraries like CXX or Boost.

-   Started integrating NUnit2 to support unit tests for the embedding
    layer - still need to add the embedding tests (many already exist,
    but were written for an older version of NUnit).

-   Added Python iteration protocol support for arrays and managed objects
    that implement IEnumerable. This means that you can now use the Python
    idiom `for item in object:` on any array or IEnumerable object.

-   Added automatic conversion from Python sequence types to managed array
    types. This means, for example, that you can now call a managed method
    like AddRange that expects an array with any Python object that supports
    the Python sequence protocol, provided the items of the sequence are
    convertible to the item type of the managed array.

-   Added new demo scripts, mostly more substantial winforms examples.

-   Finished the unit tests for event support, and fixed lots of problems
    with events and delegates as a result. This is one of the trickier
    parts of the integration layer, and there is good coverage of these
    in the unit tests now.

-   Did a fair amount of profiling with an eval version of ANTS (which is
    quite nice, BTW) and made a few changes as a result.

-   Type management was refactored, fixing the issue that caused segfaults
    when GC was enabled. Unit tests, stress tests and demo apps now all run
    nicely with Python GC enabled. There are one or two things left to fix,
    but the fixes should not have any user impact.

-   Changed to base PythonNet on Python 2.3.2. This is considered the most
    stable release, and a good 25 - 30% faster as well.

-   Added a new `CLR.dll` that acts as an extension module that allows an
    existing unmodified Python 2.3 installation to simply `import CLR` to
    bootstrap the managed integration layer.

-   A bug was causing managed methods to only expose overloads declared in
    a particular class, hiding inherited overloads of the same name. Fixed
    the bug and added some unit tests.

-   Added a virtual `__doc__` attribute to managed methods that contains
    the signature of the method. This also means that the Python `help`
    function now provides signature info when used on a managed class.

-   Calling managed methods and events `unbound` (passing the instance as
    the first argument) now works. There is a caveat for methods - if a
    class declares both static and instance methods with the same name,
    it is not possible to call that instance method unbound (the static
    method will always be called).

-   Overload selection for overloaded methods is now much better and uses
    a method resolution algorithm similar to that used by Jython.

-   Changed the managed python.exe wrapper to run as an STA thread, which
    seems to be more compatible with winforms apps. This needs a better
    solution long-term. One possibility would be a command line switch
    so that -sta or -mta could control the python.exe apartment state.

-   Added support for the Python boolean type (True, False). Bool values
    now appear as True or False to Python.

## 1.0.0-alpha.2

### Changed

-   Added a Mono makefile. Thanks to Camilo Uribe for help in testing and
    working out problems on Mono. Note that it not currently possible to
    build PythonNet using mono, due to the use of some IL attributes that
    the mono assembler / disassembler doesn't support yet.

-   Preliminary tests show that PythonNet _does_ actually run under mono,
    though the test suite bombs out before the end with an "out of memory"
    error from the mono runtime. It's just a guess at this point, but I
    suspect there may be a limited pool for allocating certain reflection
    structures, and Python uses the reflection infrastructure quite heavily.

-   Removed decoys like the non-working embedding APIs; lots of internal
    refactoring.

-   Implemented indexer support. Managed instances that implement indexers
    can now be used naturally from Python (e.g. `someobject[0]`).

-   Implemented sequence protocol support for managed arrays.

-   Implemented basic thread state management; calls to managed methods
    no longer block Python. I won't go so far as to say the thread
    choreography is "finished", as I don't have a comprehensive set of
    tests to back that up yet (and it will take some work to write a
    sufficiently large and evil set of tests).

-   Fixed a bug that caused conversions of managed strings to PyUnicode to
    produce mangled values in certain situations.

-   Fixed a number of problems related to subclassing a managed class,
    including the fact that it didn't work :)

-   Fixed all of the bugs that were causing tests to fail. This release
    contains all new bugs and new failing tests. Progress! :)

## 1.0.0-alpha.1

### Added

-   Initial (mostly) working experimental release.

[keep a changelog]: http://keepachangelog.com/

[semantic versioning]: http://semver.org/

[unreleased]: ../../compare/v2.3.0...HEAD

[2.3.0]: ../../compare/v2.2.2...v2.3.0

[2.2.2]: ../../compare/v2.2.1...v2.2.2

[2.2.1]: ../../compare/v2.2.0-dev1...v2.2.1

[2.2.0-dev1]: ../../compare/v2.1.0...v2.2.0-dev1

[2.1.0]: ../../compare/v2.0.0...v2.1.0

[2.0.0]: ../../compare/1.0...v2.0.0

[1.0.0]: https://github.com/pythonnet/pythonnet/releases/tag/1.0

[i714]: https://github.com/pythonnet/pythonnet/issues/714
[i608]: https://github.com/pythonnet/pythonnet/issues/608
[i443]: https://github.com/pythonnet/pythonnet/issues/443
[p690]: https://github.com/pythonnet/pythonnet/pull/690
[i475]: https://github.com/pythonnet/pythonnet/issues/475
[p693]: https://github.com/pythonnet/pythonnet/pull/693
[i432]: https://github.com/pythonnet/pythonnet/issues/432
[p433]: https://github.com/pythonnet/pythonnet/pull/433
[p460]: https://github.com/pythonnet/pythonnet/pull/460
[p461]: https://github.com/pythonnet/pythonnet/pull/461
[p433]: https://github.com/pythonnet/pythonnet/pull/433
[i434]: https://github.com/pythonnet/pythonnet/issues/434
[i481]: https://github.com/pythonnet/pythonnet/issues/481
[i486]: https://github.com/pythonnet/pythonnet/issues/486
[i492]: https://github.com/pythonnet/pythonnet/issues/492
[i495]: https://github.com/pythonnet/pythonnet/issues/495
[p607]: https://github.com/pythonnet/pythonnet/pull/607
[i627]: https://github.com/pythonnet/pythonnet/issues/627
[i276]: https://github.com/pythonnet/pythonnet/issues/276
[i676]: https://github.com/pythonnet/pythonnet/issues/676
[p345]: https://github.com/pythonnet/pythonnet/pull/345
[p347]: https://github.com/pythonnet/pythonnet/pull/347
[p349]: https://github.com/pythonnet/pythonnet/pull/349
[i224]: https://github.com/pythonnet/pythonnet/issues/224
[p353]: https://github.com/pythonnet/pythonnet/pull/353
[p391]: https://github.com/pythonnet/pythonnet/pull/391
[p346]: https://github.com/pythonnet/pythonnet/pull/346
[p384]: https://github.com/pythonnet/pythonnet/pull/384
[i383]: https://github.com/pythonnet/pythonnet/issues/383
[p386]: https://github.com/pythonnet/pythonnet/pull/386
[i370]: https://github.com/pythonnet/pythonnet/issues/370
[p373]: https://github.com/pythonnet/pythonnet/pull/373
[i390]: https://github.com/pythonnet/pythonnet/issues/390
[i319]: https://github.com/pythonnet/pythonnet/issues/319
[p398]: https://github.com/pythonnet/pythonnet/pull/398
[p345]: https://github.com/pythonnet/pythonnet/pull/345
[p389]: https://github.com/pythonnet/pythonnet/pull/389
[p407]: https://github.com/pythonnet/pythonnet/pull/407
[i322]: https://github.com/pythonnet/pythonnet/issues/322
[i66]: https://github.com/pythonnet/pythonnet/issues/66
[p329]: https://github.com/pythonnet/pythonnet/pull/329
[p337]: https://github.com/pythonnet/pythonnet/pull/337
[p339]: https://github.com/pythonnet/pythonnet/pull/339
[p369]: https://github.com/pythonnet/pythonnet/pull/369
[p368]: https://github.com/pythonnet/pythonnet/pull/368
[p341]: https://github.com/pythonnet/pythonnet/pull/341
[p353]: https://github.com/pythonnet/pythonnet/pull/353
[p371]: https://github.com/pythonnet/pythonnet/pull/371
[p345]: https://github.com/pythonnet/pythonnet/pull/345
[i362]: https://github.com/pythonnet/pythonnet/issues/362
[i401]: https://github.com/pythonnet/pythonnet/issues/401
[i262]: https://github.com/pythonnet/pythonnet/issues/262
[p343]: https://github.com/pythonnet/pythonnet/pull/343
[p365]: https://github.com/pythonnet/pythonnet/pull/365
[i203]: https://github.com/pythonnet/pythonnet/issues/203
[p377]: https://github.com/pythonnet/pythonnet/pull/377
[p398]: https://github.com/pythonnet/pythonnet/pull/398
[p400]: https://github.com/pythonnet/pythonnet/pull/400
[i397]: https://github.com/pythonnet/pythonnet/issues/397
[p399]: https://github.com/pythonnet/pythonnet/pull/399
[i120]: https://github.com/pythonnet/pythonnet/issues/120
[i413]: https://github.com/pythonnet/pythonnet/issues/413
[i179]: https://github.com/pythonnet/pythonnet/issues/179
[i414]: https://github.com/pythonnet/pythonnet/issues/414
[p415]: https://github.com/pythonnet/pythonnet/pull/415
[p329]: https://github.com/pythonnet/pythonnet/pull/329
[p360]: https://github.com/pythonnet/pythonnet/pull/360
[i417]: https://github.com/pythonnet/pythonnet/issues/417
[i336]: https://github.com/pythonnet/pythonnet/issues/336
[p310]: https://github.com/pythonnet/pythonnet/pull/310
[p312]: https://github.com/pythonnet/pythonnet/pull/312
[p281]: https://github.com/pythonnet/pythonnet/pull/281
[p268]: https://github.com/pythonnet/pythonnet/pull/268
[p287]: https://github.com/pythonnet/pythonnet/pull/287
[p314]: https://github.com/pythonnet/pythonnet/pull/314
[p320]: https://github.com/pythonnet/pythonnet/pull/320
[p275]: https://github.com/pythonnet/pythonnet/pull/275
[i270]: https://github.com/pythonnet/pythonnet/issues/270
[p273]: https://github.com/pythonnet/pythonnet/pull/273
[p294]: https://github.com/pythonnet/pythonnet/pull/294
[p300]: https://github.com/pythonnet/pythonnet/pull/300
[p286]: https://github.com/pythonnet/pythonnet/pull/286
[p219]: https://github.com/pythonnet/pythonnet/pull/219
[p208]: https://github.com/pythonnet/pythonnet/pull/208
[p206]: https://github.com/pythonnet/pythonnet/pull/206
[p233]: https://github.com/pythonnet/pythonnet/pull/233
[p219]: https://github.com/pythonnet/pythonnet/pull/219
[p250]: https://github.com/pythonnet/pythonnet/pull/250
[p225]: https://github.com/pythonnet/pythonnet/pull/225
[p78]: https://github.com/pythonnet/pythonnet/pull/78
[p163]: https://github.com/pythonnet/pythonnet/pull/163
[p625]: https://github.com/pythonnet/pythonnet/pull/625
[i131]: https://github.com/pythonnet/pythonnet/issues/131
[p531]: https://github.com/pythonnet/pythonnet/pull/531
[i755]: https://github.com/pythonnet/pythonnet/pull/755
[p534]: https://github.com/pythonnet/pythonnet/pull/534
[i449]: https://github.com/pythonnet/pythonnet/issues/449
[i1342]: https://github.com/pythonnet/pythonnet/issues/1342
[i238]: https://github.com/pythonnet/pythonnet/issues/238
[i1481]: https://github.com/pythonnet/pythonnet/issues/1481
[i1672]: https://github.com/pythonnet/pythonnet/pull/1672
