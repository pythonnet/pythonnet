# pythonnet - Python for .NET

Python for .NET is a package that gives Python programmers nearly
seamless integration with the .NET Common Language Runtime (CLR) and
provides a powerful application scripting tool for .NET developers.
It allows Python code to interact with the CLR, and may also be used to
embed Python into a .NET application.

[![travis shield][]](https://travis-ci.org/pythonnet/pythonnet)
[![appveyor shield][]](https://ci.appveyor.com/project/pythonnet/pythonnet-0kq5d/branch/master)
[![license shield][]](./LICENSE)

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
    a `using (Py.GIL()) {/_ Your code here _/}` block.
-   Import python modules using `dynamic mod = Py.Import("mod")`,
    then you can call functions as normal, eg `mod.func(args)`.
-   Use `mod.func(args, Py.kw("keywordargname", keywordargvalue))`
    to apply keyword arguments.
-   All python objects should be declared as `dynamic` type.
-   Mathematical operations involving python and literal/managed types must
    have the python object first, eg `np.pi_2` works, `2_np.pi` doesn't.

### Example

```csharp
static void Main(string[] args)
{
    using (Py.GIL())
    {
        dynamic np = Py.Import("numpy");
        dynamic sin = np.sin;
        Console.WriteLine(np.cos(np.pi*2));
        Console.WriteLine(sin(5));
        double c = np.cos(5) + sin(5);
        Console.WriteLine(c);
        /* this block is temporarily disabled due to regression
        dynamic a = np.array(new List<float> { 1, 2, 3 });
        dynamic b = np.array(new List<float> { 6, 5, 4 }, Py.kw("dtype", np.int32));
        Console.WriteLine(a.dtype);
        Console.WriteLine(b.dtype);
        Console.WriteLine(a * b); */
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
[6.  10.  12.]
```

[travis shield]: https://img.shields.io/travis/pythonnet/pythonnet/master.svg?style=flat-square

[appveyor shield]: https://img.shields.io/appveyor/ci/pythonnet/pythonnet-0kq5d/master.svg?style=flat-square&logo=data%3Aimage%2Fsvg%2Bxml%2C%3Csvg+xmlns%3D%27http%3A%2F%2Fwww.w3.org%2F2000%2Fsvg%27+width%3D%2740%27+height%3D%2740%27+viewBox%3D%270+0+40+40%27%3E%3Cpath+fill%3D%27%23BBB%27+d%3D%27M20+0c11+0+20+9+20+20s-9+20-20+20S0+31+0+20+9+0+20+0zm4.9+23.9c2.2-2.8+1.9-6.8-.9-8.9-2.7-2.1-6.7-1.6-9+1.2-2.2+2.8-1.9+6.8.9+8.9+2.8+2.1+6.8+1.6+9-1.2zm-10.7+13c1.2.5+3.8+1+5.1+1L28+25.3c2.8-4.2+2.1-9.9-1.8-13-3.5-2.8-8.4-2.7-11.9+0L2.2+21.6c.3+3.2+1.2+4.8+1.2+4.9l6.9-7.5c-.5+3.3.7+6.7+3.5+8.8+2.4+1.9+5.3+2.4+8.1+1.8l-7.7+7.3z%27%2F%3E%3C%2Fsvg%3E

[license shield]: https://img.shields.io/badge/license-MIT-blue.svg?style=flat-square
