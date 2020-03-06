using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Python.Runtime.Codecs
{
    //like MethodWrapper but not static, so we can throw some state into it.
    internal class MethodWrapper2
    {
        public IntPtr mdef;
        public IntPtr ptr;
        private bool _disposed = false;
        private ThunkInfo _thunk;

        public MethodWrapper2(object instance, string name, string funcType = null)
        {
            // Turn the managed method into a function pointer
            var type = instance.GetType();
            _thunk = GetThunk(instance, type.GetMethod(name), funcType);

            // Allocate and initialize a PyMethodDef structure to represent
            // the managed method, then create a PyCFunction.

            mdef = Runtime.PyMem_Malloc(4 * IntPtr.Size);
            TypeManager.WriteMethodDef(mdef, name, _thunk.Address, 0x0003);
            ptr = Runtime.PyCFunction_NewEx(mdef, IntPtr.Zero, IntPtr.Zero);
        }

        internal static ThunkInfo GetThunk(object instance, System.Reflection.MethodInfo method, string funcType = null)
        {
            Type dt;
            if (funcType != null)
                dt = typeof(Interop).GetNestedType(funcType) as Type;
            else
                dt = Interop.GetPrototype(method.Name);

            if (dt == null)
            {
                return ThunkInfo.Empty;
            }
            Delegate d = Delegate.CreateDelegate(dt, instance, method);
            var info = new ThunkInfo(d);
            return info;
        }

        /*public IntPtr Call(IntPtr args, IntPtr kw)
        {
            return Runtime.PyCFunction_Call(ptr, args, kw);
        }*/

        public void Release()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            bool freeDef = Runtime.Refcount(ptr) == 1;
            Runtime.XDecref(ptr);
            if (freeDef && mdef != IntPtr.Zero)
            {
                Runtime.PyMem_Free(mdef);
                mdef = IntPtr.Zero;
            }
        }
    }

    internal class CallableHelper
    {
        public static object[] ConvertArgs(IntPtr args)
        {
            var numArgs = Runtime.PyTuple_Size(args);
            object[] managedArgs = null;
            if (numArgs > 0)
            {
                managedArgs = new object[numArgs];
                for (int idx = 0; idx < numArgs; ++idx)
                {
                    IntPtr item = Runtime.PyTuple_GetItem(args, idx);
                    object result;
                    if (!Converter.ToManaged(item, typeof(object), out result, false))
                        throw new Exception();

                    managedArgs[idx] = result;
                }
            }
            return managedArgs;
        }
    }

    interface ICallable
    {
        IntPtr RunAction(IntPtr self, IntPtr args);
    }

    //class which wraps a thing
    internal class ActionWrapper : ICallable
    {
        Action<object[]> _action = null;
        internal ActionWrapper(Action action)
        {
            _action = (object[] args) => { action(); };
        }

        internal ActionWrapper(Action<object[]> action)
        {
            _action = action;
        }

        public virtual IntPtr RunAction(IntPtr self, IntPtr args)
        {
            try
            {
                object[] managedArgs = CallableHelper.ConvertArgs(args);
                _action(managedArgs);
            }
            catch
            {
                Exceptions.SetError(Exceptions.TypeError, "Error in RunAction");
                return IntPtr.Zero;
            }

            return Runtime.PyNone;
        }
    }

    internal class FunctionWrapper : ICallable
    {
        Func<object[], object> _func = null;

        internal FunctionWrapper(Func<object> func)
        {
            _func = (object[] args) => { return func(); };
        }

        internal FunctionWrapper(Func<object[], object> func)
        {
            _func = func;
        }

        public virtual IntPtr RunAction(IntPtr self, IntPtr args)
        {
            try
            {
                object[] managedArgs = CallableHelper.ConvertArgs(args);
                var retVal = _func(managedArgs);
                return Converter.ToPython(retVal);
            }
            catch
            {
                Exceptions.SetError(Exceptions.TypeError, "Error in RunAction");
                return IntPtr.Zero;
            }
        }
    }

    //converts python functions to C# actions
    class FunctionCodec : IPyObjectDecoder, IPyObjectEncoder
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

            //C# object must be an Action
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
                    object result;
                    Converter.ToManaged(pyResult.Handle, typeof(object), out result, true);
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
            PyObjectConversions.RegisterEncoder(Instance);
        }

        public bool CanEncode(Type type)
        {
            return IsCallable(type);
        }

        public PyObject TryEncode(object value)
        {
            if (value == null) return null;

            var targetType = value.GetType();
            ICallable callable = null;
            if (IsUnaryAction(targetType))
            {
                callable = new ActionWrapper(value as Action);

            }
            else if (IsVariadicObjectAction(targetType))
            {
                callable = new ActionWrapper(value as Action<object[]>);
            }
            else if (IsUnaryFunc(targetType))
            {
                callable = new FunctionWrapper(value as Func<object>);
            }
            else if (IsVariadicObjectFunc(targetType))
            {
                callable = new FunctionWrapper(value as Func<object[], object>);
            }
            else
            {
                throw new Exception("object cannot be encoded!");
            }

            var methodWrapper = new MethodWrapper2(callable, "RunAction", "BinaryFunc");

            //TODO - lifetime??
            return new PyObject(methodWrapper.ptr);
        }
    }
}
