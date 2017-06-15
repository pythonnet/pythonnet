# Changelog

All notable changes to Python for .NET will be documented in this file.
This project adheres to [Semantic Versioning][].

This document follows the conventions laid out in [Keep a CHANGELOG][].

## [unreleased][]

### Added
-   Added `clr.GetClrType` (#432, #433)
-   Allowed passing `None` for nullable args (#460)
-   Added keyword arguments based on C# syntax for calling CPython methods (#461)

### Changed

### Fixed

-   Fixed Visual Studio 2017 compat (#434) for setup.py
-   Fixed crash on exit of the Python interpreter if a python class
    derived from a .NET class has a `__namespace__` or `__assembly__`
    attribute (#481)
-   Fixed conversion of 'float' and 'double' values (#486)
-   Fixed 'clrmethod' for python 2 (#492)


## [2.3.0][] - 2017-03-11

### Added

-   Added Code Coverage (#345)
-   Added `PySys_SetArgvEx` (#347)
-   Added XML Documentation (#349)
-   Added `Embedded_Tests` on AppVeyor (#224)(#353)
-   Added `Embedded_Tests` on Travis (#224)(#391)
-   Added PY3 settings to solution configuration-manager (#346)
-   Added `Slack` (#384)(#383)(#386)
-   Added function of passing an arbitrary .NET object as the value
    of an attribute of `PyObject` (#370)(#373)
-   Added `Coverity scan` (#390)
-   Added `bumpversion` for version control (#319)(#398)
-   Added `tox` for local testing (#345)
-   Added `requirements.txt`
-   Added to `PythonEngine` methods `Eval` and `Exec` (#389)
-   Added implementations of `ICustomMarshal` (#407)
-   Added docker images (#322)
-   Added hooks in `pyinstaller` and `cx_freeze` for `pythonnet` (#66)

### Changed

-   Refactored python `unittests` (#329)
-   Refactored python `setup.py` (#337)
-   Refactored remaining of Build Directives on `runtime.cs` (#339)
-   Refactored `Embedded_Tests` to make easier to write tests (#369)
-   Changed `unittests` to `pytest` (#368)
-   Upgraded NUnit framework from `2.6.3` to `3.5.0` (#341)
-   Downgraded NUnit framework from `3.5.0` to `2.6.4` (#353)
-   Upgraded NUnit framework from `2.6.4` to `3.6.0` (#371)
-   Unfroze Mono version on Travis (#345)
-   Changed `conda.recipe` build to only pull-requests (#345)
-   Combine `Py_DEBUG` and `PYTHON_WITH_PYDEBUG` flags (#362)

### Deprecated

-   Deprecated `RunString` (#401)

### Fixed

-   Fixed crash during Initialization (#262)(#343)
-   Fixed crash during Shutdown (#365)
-   Fixed multiple build warnings
-   Fixed method signature match for Object Type (#203)(#377)
-   Fixed outdated version number in AssemblyInfo (#398)
-   Fixed wrong version number in `conda.recipe` (#398)
-   Fixed fixture location for Python tests and `Embedded_Tests`
-   Fixed `PythonException` crash during Shutdown (#400)
-   Fixed `AppDomain` unload during GC (#397)(#400)
-   Fixed `Py_Main` & `PySys_SetArgvEx` `no mem error` on `UCS4/PY3` (#399)
-   Fixed `Python.Runtime.dll.config` on macOS (#120)
-   Fixed crash on `PythonEngine.Version` (#413)
-   Fixed `PythonEngine.PythonPath` issues (#179)(#414)(#415)

### Removed

-   Removed `six` dependency for `unittests` (#329)
-   Removed `Mono.Unix` dependency for `UCS4` (#360)
-   Removed need for `Python.Runtime.dll.config`
-   Removed PY32 build option `PYTHON_WITH_WIDE_UNICODE` (#417)

## [2.2.2][] - 2017-01-29

### Fixed

-   Missing files from packaging (#336)

## [2.2.1][] - 2017-01-26

-   `v2.2.0` had a release issue on PyPi. Bumped to `v2.2.1`

### Added

-   Python 3.6 support (#310)
-   Added `__version__` to module (#312)
-   Added `conda` recipe (#281)
-   Nuget update on build (#268)
-   Added `__cause__` attribute on exception (#287)

### Changed

-   License to MIT (#314)
-   Project clean-up (#320)
-   Refactor `#if` directives
-   Rename Decref/Incref to XDecref/XIncre (#275)
-   Remove printing if Decref is called with NULL (#275)

### Removed

-   Python 2.6 support (#270)
-   Python 3.2 support (#270)

### Fixed

-   Fixed `isinstance` refcount_leak (#273)
-   Comparison Operators (#294)
-   Improved Linux support (#300)
-   Exception pickling (#286)

## [2.2.0-dev1][] - 2016-09-19

### Changed

-   Switch to C# 6.0 (#219)
-   `setup.py` improvements for locating build tools (#208)
-   unmanaged exports updated (#206)
-   Mono update pinned to 4.2.4.4 (#233)

### Fixed

-   Fixed relative imports (#219)
-   Fixed recursive types (#250)
-   Demo fix - stream reading (#225)

## [2.1.0][] - 2016-04-12

### Added

-   Added Python 3.2 support. (#78)
-   Added Python 3.3 support. (#78)
-   Added Python 3.4 support. (#78)
-   Added Python 3.5 support. (#163)
-   Managed types can be sub-classed in Python (#78)
-   Uses dynamic objects for cleaner code when embedding Python (#78)

### Changed

-   Better Linux support (with or without --enable-shared option) (#78)

### Removed

-   Implicit Type Casting (#131)

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
