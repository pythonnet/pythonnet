# pythonnet - Python for .NET

[![Join the chat at https://gitter.im/pythonnet/pythonnet](https://badges.gitter.im/pythonnet/pythonnet.svg)](https://gitter.im/pythonnet/pythonnet?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

[![appveyor shield][]](https://ci.appveyor.com/project/pythonnet/pythonnet/branch/master)
[![travis shield][]](https://travis-ci.org/pythonnet/pythonnet)
[![codecov shield][]](https://codecov.io/github/pythonnet/pythonnet)
[![coverity shield][]](https://scan.coverity.com/projects/pythonnet)

[![license shield][]](./LICENSE)
[![pypi package version][]](https://pypi.python.org/pypi/pythonnet)
[![python supported shield][]](https://pypi.python.org/pypi/pythonnet)
[![stackexchange shield][]](http://stackoverflow.com/questions/tagged/python.net)
[![slack][]](https://pythonnet.slack.com)

Python for .NET is a package that gives Python programmers nearly
seamless integration with the .NET Common Language Runtime (CLR) and
provides a powerful application scripting tool for .NET developers.
It allows Python code to interact with the CLR, and may also be used to
embed Python into a .NET application.

## Calling .NET code from Python

Python for .NET allows CLR namespaces to be treated essentially
as Python packages.

```python
import clr
from System import String
from System.Collections import *
```

To load an assembly, use the `AddReference` function in the `clr` module:

```python
import clr
clr.AddReference("System.Windows.Forms")
from System.Windows.Forms import Form
```

## Embedding Python in .NET

-   All calls to python should be inside
    a `using (Py.GIL()) {/* Your code here */}` block.
-   Import python modules using `dynamic mod = Py.Import("mod")`,
    then you can call functions as normal, eg `mod.func(args)`.
-   Use `mod.func(args, Py.kw("keywordargname", keywordargvalue))` or `mod.func(args, keywordargname=keywordargvalue)`
    to apply keyword arguments.
-   All python objects should be declared as `dynamic` type.
-   Mathematical operations involving python and literal/managed types must
    have the python object first, eg. `np.pi * 2` works, `2 * np.pi` doesn't.

### Example

```csharp
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

        dynamic b = np.array(new List<float> { 6, 5, 4 }, dtype=np.int32);
        Console.WriteLine(b.dtype);

        Console.WriteLine(a * b);
        Console.ReadKey();
    }
}
```

Output:

```c
1.0
-0.958924274663
-0.6752620892
float64
int32
[  6.  10.  12.]
```

[appveyor shield]: https://img.shields.io/appveyor/ci/pythonnet/pythonnet/master.svg?label=AppVeyor

[codecov shield]: https://img.shields.io/codecov/c/github/pythonnet/pythonnet/master.svg?label=Codecov

[coverity shield]: https://img.shields.io/coverity/scan/7830.svg

[license shield]: https://img.shields.io/badge/license-MIT-blue.svg?maxAge=3600

[pypi package version]: https://img.shields.io/pypi/v/pythonnet.svg

[python supported shield]: https://img.shields.io/pypi/pyversions/pythonnet.svg

[slack]: https://img.shields.io/badge/chat-slack-color.svg?style=social

[stackexchange shield]: https://img.shields.io/badge/StackOverflow-python.net-blue.svg

[travis shield]: https://img.shields.io/travis/pythonnet/pythonnet/master.svg?label=Travis
