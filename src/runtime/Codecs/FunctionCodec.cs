using System;

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
try:
    x = len(signature(f).parameters)
except:
    x = 0
", null, locals.Handle);

            var x = locals.GetItem("x");
            return new PyInt(x).ToInt32();
        }

        private static bool IsUnaryAction(Type targetType)
        {
            return targetType == typeof(Action);
        }

        private static bool IsVariadicObjectAction(Type targetType)
        {
            return targetType == typeof(Action<object[]>);
        }

        private static bool IsUnaryFunc(Type targetType)
        {
            return targetType == typeof(Func<object>);
        }

        private static bool IsVariadicObjectFunc(Type targetType)
        {
            return targetType == typeof(Func<object[], object>);
        }

        private static bool IsAction(Type targetType)
        {
            return IsUnaryAction(targetType) || IsVariadicObjectAction(targetType);
        }

        private static bool IsFunc(Type targetType)
        {
            return IsUnaryFunc(targetType) || IsVariadicObjectFunc(targetType);
        }

        private static bool IsCallable(Type targetType)
        {
            //TODO - delegate, event, ...
            return IsAction(targetType) || IsFunc(targetType);
        }

        public static FunctionCodec Instance { get; } = new FunctionCodec();
        public bool CanDecode(PyObject objectType, Type targetType)
        {
            //python object must be callable
            if (!objectType.IsCallable()) return false;

            //C# object must be callable
            if (!IsCallable(targetType))
                return false;

            //We don't know if it will work without the instance
            //The number of arguments of a unary or variadic object callable
            //is always going to be 0 or 1
            return true;
        }

        private static object ConvertUnaryAction(PyObject pyObj)
        {
            Func<object> func = (Func<object>)ConvertUnaryFunc(pyObj);
            Action action = () => { func(); };
            return (object)action;
        }

        private static object ConvertVariadicObjectAction(PyObject pyObj, int numArgs)
        {
            Func<object[], object> func = (Func<object[], object>)ConvertVariadicObjectFunc(pyObj, numArgs);
            Action<object[]> action = (object[] args) => { func(args); };
            return (object)action;
        }

        //TODO share code between ConvertUnaryFunc and ConvertVariadicObjectFunc
        private static object ConvertUnaryFunc(PyObject pyObj)
        {
            Func<object> func = () =>
            {
                Runtime.XIncref(pyObj.Handle);
                PyObject pyAction = new PyObject(pyObj.Handle);
                var pyArgs = new PyObject[0];
                using (Py.GIL())
                {
                    var pyResult = pyAction.Invoke(pyArgs);
                    Runtime.XIncref(pyResult.Handle);
                    Converter.ToManaged(pyResult.Handle, typeof(object), out var result, true);
                    return result;
                }
            };
            return (object)func;
        }

        private static object ConvertVariadicObjectFunc(PyObject pyObj, int numArgs)
        {
            Func<object[], object> func = (object[] o) =>
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
                    object result;
                    Converter.ToManaged(pyResult.Handle, typeof(object), out result, true);
                    return result;
                }
            };
            return (object)func;
        }

        public bool TryDecode<T>(PyObject pyObj, out T value)
        {
            value = default(T);
            var tT = typeof(T);
            if (!IsCallable(tT))
                return false;

            var numArgs = GetNumArgs(pyObj);

            if (IsAction(tT))
            {
                object actionObj = null;
                if (numArgs == 0)
                {
                    actionObj = ConvertUnaryAction(pyObj);
                }
                else
                {
                    actionObj = ConvertVariadicObjectAction(pyObj, numArgs);
                }

                value = (T)actionObj;
                return true;
            }
            else if (IsFunc(tT))
            {

                object funcObj = null;
                if (numArgs == 0)
                {
                    funcObj = ConvertUnaryFunc(pyObj);
                }
                else
                {
                    funcObj = ConvertVariadicObjectFunc(pyObj, numArgs);
                }

                value = (T)funcObj;
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
            PyObjectConversions.RegisterDecoder(Instance);
        }
    }
}
