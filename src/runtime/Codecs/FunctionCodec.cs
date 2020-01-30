using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Python.Runtime.Codecs
{
    //converts python functions to C# actions
    class FunctionCodec : IPyObjectDecoder
    {
        public static FunctionCodec Instance { get; } = new FunctionCodec();
        public bool CanDecode(PyObject objectType, Type targetType)
        {
            if (!objectType.IsCallable()) return false;

            //TODO - handle nonzero arguments.
            var args = targetType.GetGenericArguments();
            return args.Length == 0;
        }

        public bool TryDecode<T>(PyObject pyObj, out T value)
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
            var v = (object)action;
            value = (T)v;
            return true;
        }

        public static void Register()
        {
            PyObjectConversions.RegisterDecoder(Instance);
        }
    }
}
