pythonnet
=========

Python for .NET is a package that gives Python programmers nearly seamless integration with the .NET Common Language Runtime (CLR) and provides a powerful application scripting tool for .NET developers.

[![Build Status](https://travis-ci.org/pythonnet/pythonnet.png?branch=develop)](https://travis-ci.org/pythonnet/pythonnet)

[![Build status](https://ci.appveyor.com/api/projects/status/u3p6pkiqpgu0qoku/branch/python3)](https://ci.appveyor.com/project/TonyRoberts/pythonnet/branch/python3)


**Features not yet integrated into the main branch**:
- Python 3 support
- Subclassing managed types in Python

--------------------------------------------------------------------------------------------------------

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
    dynamic a = np.array(new List<float> { 1, 2, 3 });
    dynamic b = np.array(new List<float> { 6, 5, 4 }, Py.kw("dtype", np.int32));
    Console.WriteLine(a.dtype);
    Console.WriteLine(b.dtype);
    Console.WriteLine(a * b);
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
