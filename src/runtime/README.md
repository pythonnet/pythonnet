`pythonnet` is a package that gives .NET programmers ability to
integrate Python engine and use Python libraries.

## Embedding Python in .NET

-  You must set `Runtime.PythonDLL` property or `PYTHONNET_PYDLL` environment variable,
   otherwise you will receive `BadPythonDllException`
   (internal, derived from `MissingMethodException`) upon calling `Initialize`.
   Typical values are `python38.dll` (Windows), `libpython3.8.dylib` (Mac),
   `libpython3.8.so` (most other *nix). Full path may be required.
-  Then call `PythonEngine.Initialize()`. If you plan to [use Python objects from
   multiple threads](https://github.com/pythonnet/pythonnet/wiki/Threading),
   also call `PythonEngine.BeginAllowThreads()`.
-  All calls to Python should be inside a
   `using (Py.GIL()) {/* Your code here */}` block.
-  Import python modules using `dynamic mod = Py.Import("mod")`, then
   you can call functions as normal, eg `mod.func(args)`.
   You can also access Python objects via `PyObject` and dervied types
   instead of using `dynamic`.
-  Use `mod.func(args, Py.kw("keywordargname", keywordargvalue))` or
   `mod.func(args, keywordargname: keywordargvalue)` to apply keyword
   arguments.
-  Mathematical operations involving python and literal/managed types
   must have the python object first, eg. `np.pi * 2` works,
   `2 * np.pi` doesn't.

## Example

```csharp
using var _ = Py.GIL();

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
```

Output:

```
1.0
-0.958924274663
-0.6752620892
float64
int32
[  6.  10.  12.]
```



## Resources

Information on installation, FAQ, troubleshooting, debugging, and
projects using pythonnet can be found in the Wiki:

https://github.com/pythonnet/pythonnet/wiki

Mailing list
    https://mail.python.org/mailman/listinfo/pythondotnet
Chat
    https://gitter.im/pythonnet/pythonnet

### .NET Foundation

This project is supported by the [.NET Foundation](https://dotnetfoundation.org).
