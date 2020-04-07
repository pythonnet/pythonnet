using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Python.Runtime
{
    public static class CustomConverter
    {
        private static Dictionary<Type, Func<object, PyObject>> _converterToPython;
        private static Dictionary<Type, Func<PyObject, object>> _converterToNet;

        static CustomConverter()
        {
            _converterToNet = new Dictionary<Type, Func<PyObject, object>>();
            _converterToPython = new Dictionary<Type, Func<object, PyObject>>();
        }

        #region ToPython
        public static void RegisterConverterToPython<T>(Func<T, PyObject> converter)
        {
            RegisterToPython(typeof(T), t => converter((T)t));
        }

        private static void RegisterToPython(Type type, Func<object, PyObject> converter)
        {
            if (!_converterToPython.ContainsKey(type))
                _converterToPython.Add(type, converter);
        }

        public static Func<object, PyObject>[] PythonConverter => _converterToPython.Values.ToArray();

        public static bool TryGetPythonConverter(Type type, out Func<object, PyObject> converter)
        {
            if (_converterToPython.TryGetValue(type, out converter))
                return true;
            converter = null;
            return false;
        }
        #endregion


        #region ToNet
        public static void RegisterConverterToNet<T>(Func<PyObject, T> converter)
        {
            RegisterToNet(typeof(T), t => converter(t));
        }

        public static void RegisterToNet(Type type, Func<PyObject, object> converter)
        {
            if (!_converterToNet.ContainsKey(type))
                _converterToNet.Add(type, converter);
        }

        public static Func<PyObject, object>[] NetConverter => _converterToNet.Values.ToArray();

        public static bool TryGetNetConverter(Type type, out Func<PyObject, object> converter)
        {
            if (_converterToNet.TryGetValue(type, out converter))
                return true;
            converter = null;
            return false;
        }
        #endregion
    }
}
