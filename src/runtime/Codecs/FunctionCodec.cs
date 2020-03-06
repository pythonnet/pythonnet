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

    //class which wraps a thing
    internal class ActionWrapper
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
            var numArgs = Runtime.PyTuple_Size(args);
            {
                object[] managedArgs = null;
                if (numArgs > 0)
                {
                    managedArgs = new object[numArgs];
                    for (int idx = 0; idx < numArgs; ++idx)
                    {
                        IntPtr item = Runtime.PyTuple_GetItem(args, idx);
                        object result;
                        //this will cause an exception to be raised if there is a failure,
                        // so we can safely return IntPtr.Zero
                        bool setError = true;
                        if (!Converter.ToManaged(item, typeof(object), out result, setError))
                            return IntPtr.Zero;

                        managedArgs[idx] = result;
                    }
                }

                //get the args out, convert them each to C# one by one.

                //call the action with the C# args
                try
                {
                    _action(managedArgs);
                }
                catch
                {
                    Exceptions.SetError(Exceptions.TypeError, "Action threw an exception");
                    return IntPtr.Zero;
                }
            }

            return Runtime.PyNone;
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

        private static int GetNumArgs(Type targetType)
        {
            var args = targetType.GetGenericArguments();
            return args.Length;
        }

        private static bool IsUnaryAction(Type targetType)
        {
            return targetType == typeof(Action);
        }

        private static bool IsVariadicObjectAction(Type targetType)
        {
            return targetType == typeof(Action<object[]>);
        }

        private static bool IsAction(Type targetType)
        {
            return IsUnaryAction(targetType) || IsVariadicObjectAction(targetType);
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
            ActionWrapper wrapper = null;
            if (IsUnaryAction(targetType))
                wrapper = new ActionWrapper(value as Action);
            else if (IsVariadicObjectAction(targetType))
                wrapper = new ActionWrapper(value as Action<object[]>);

            var methodWrapper = new MethodWrapper2(wrapper, "RunAction", "BinaryFunc");

            //TODO - lifetime??
            return new PyObject(methodWrapper.ptr);
        }
    }
}
