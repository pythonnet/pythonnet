pythonnet
=========

Python for .NET is a package that gives Python programmers nearly seamless integration with the .NET Common Language Runtime (CLR) and provides a powerful application scripting tool for .NET developers. It allows Python code to interact with the CLR, and may also be used to embed Python into a .NET application.

[![Build Status](https://travis-ci.org/pythonnet/pythonnet.png?branch=master)](https://travis-ci.org/pythonnet/pythonnet)

[![Build status](https://ci.appveyor.com/api/projects/status/c8k0miljb3n1c7be/branch/master)](https://ci.appveyor.com/project/TonyRoberts/pythonnet-480xs)

**Calling .NET code from Python**

Python for .NET allows CLR namespaces to be treated essentially as Python packages.

```python
    import clr
    from System import String
    from System.Collections import *
```
To load an assembly, use the "AddReference" function in the "clr" module:

```python
    import clr
    clr.AddReference("System.Windows.Forms")
    from System.Windows.Forms import Form
```

**Embedding Python in .NET**

+ All calls to python should be inside a "using (Py.GIL()) {/* Your code here */}" block.
+ Import python modules using dynamic mod = Py.Import("mod"), then you can call functions as normal, eg mod.func(args).
+ Use mod.func(args, Py.kw("keywordargname", keywordargvalue)) to apply keyword arguments.
+ All python objects should be declared as 'dynamic' type.
+ Mathematical operations involving python and literal/managed types must have the python object first, eg np.pi*2 works, 2*np.pi doesn't

EG:
```csharp
static void Main(string[] args)
{
  using (Py.GIL()) {
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
    Console.WriteLine(a * b);
    */
    Console.ReadKey();
  }
}
```
outputs:
```
1.0
-0.958924274663
-0.6752620892
float64
int32
[  6.  10.  12.]
```
