using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Python.Runtime.Codecs
{
    //converts python functions to C# actions
    class FunctionCodec : IPyObjectDecoder
    {
        private static int GetNumArgs(PyObject pyCallable)
        {
            var locals = new PyDict();
            locals.SetItem("f", pyCallable);
            using (Py.GIL())
                PythonEngine.Exec(@"
from inspect import signature
x = len(signature(f).parameters)
", null, locals.Handle);

            var x = locals.GetItem("x");
            return new PyInt(x).ToInt32();
        }

        private static int GetNumArgs(Type targetType)
        {
            var args = targetType.GetGenericArguments();
            return args.Length;
        }

        private static bool IsAction(Type targetType)
        {
            return targetType.FullName.StartsWith("System.Action");
        }

        private static bool IsCallable(Type targetType)
        {
            //TODO - Func, delegate, etc
            return IsAction(targetType);
        }

        public static FunctionCodec Instance { get; } = new FunctionCodec();
        public bool CanDecode(PyObject objectType, Type targetType)
        {
            //python object must be callable
            if (!objectType.IsCallable()) return false;

            //C# object must be an Action
            if (!IsCallable(targetType))
                return false;

            return GetNumArgs(objectType) == GetNumArgs(targetType);
        }

        public bool TryDecode<T>(PyObject pyObj, out T value)
        {
            value = default(T);
            var tT = typeof(T);
            if (!IsCallable(tT))
                return false;

            var numArgs = GetNumArgs(tT);

            if (IsAction(tT))
            {
                object actionObj = null;
                if (numArgs == 0)
                {
                    Action action = () =>
                    {
                        Runtime.XIncref(pyObj.Handle);
                        PyObject pyAction = new PyObject(pyObj.Handle);
                        var pyArgs = new PyObject[0];
                        using (Py.GIL())
                        {
                            var pyResult = pyAction.Invoke(pyArgs);
                            Runtime.XIncref(pyResult.Handle);
                        }
                    };
                    actionObj = (object)action;
                }
                else
                {
                    Action<object[]> action = (object[] o) =>
                    {
                        Runtime.XIncref(pyObj.Handle);
                        PyObject pyAction = new PyObject(pyObj.Handle);
                        var pyArgs = new PyObject[numArgs];
                        int i = 0;
                        foreach (object obj in o)
                        {
                            pyArgs[i++] = new PyObject(Converter.ToPython(obj));
                        }

                        using (Py.GIL())
                        {
                            var pyResult = pyAction.Invoke(pyArgs);
                            Runtime.XIncref(pyResult.Handle);
                        }
                    };
                    actionObj = (object)action;
                }

                value = (T)actionObj;
                return true;
            }
            else
            {
                return false;
            }
        }

        public static void Register()
        {
            PyObjectConversions.RegisterDecoder(Instance);
        }
    }
}
