using System;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;

namespace Python.Runtime
{
    /// <summary>
    /// The DelegateManager class manages the creation of true managed
    /// delegate instances that dispatch calls to Python methods.
    /// </summary>
    internal class DelegateManager
    {
        private Hashtable cache;
        private Type basetype;
        private Type listtype;
        private Type voidtype;
        private Type typetype;
        private Type ptrtype;
        private CodeGenerator codeGenerator;

        public DelegateManager()
        {
            basetype = typeof(Dispatcher);
            listtype = typeof(ArrayList);
            voidtype = typeof(void);
            typetype = typeof(Type);
            ptrtype = typeof(IntPtr);
            cache = new Hashtable();
            codeGenerator = new CodeGenerator();
        }

        /// <summary>
        /// Given a true delegate instance, return the PyObject handle of the
        /// Python object implementing the delegate (or IntPtr.Zero if the
        /// delegate is not implemented in Python code.
        /// </summary>
        public IntPtr GetPythonHandle(Delegate d)
        {
            if (d?.Target is Dispatcher)
            {
                var disp = (Dispatcher)d.Target;
                return disp.target;
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// GetDispatcher is responsible for creating a class that provides
        /// an appropriate managed callback method for a given delegate type.
        /// </summary>
        private Type GetDispatcher(Type dtype)
        {
            // If a dispatcher type for the given delegate type has already
            // been generated, get it from the cache. The cache maps delegate
            // types to generated dispatcher types. A possible optimization
            // for the future would be to generate dispatcher types based on
            // unique signatures rather than delegate types, since multiple
            // delegate types with the same sig could use the same dispatcher.

            object item = cache[dtype];
            if (item != null)
            {
                return (Type)item;
            }

            string name = $"__{dtype.FullName}Dispatcher";
            name = name.Replace('.', '_');
            name = name.Replace('+', '_');
            TypeBuilder tb = codeGenerator.DefineType(name, basetype);

            // Generate a constructor for the generated type that calls the
            // appropriate constructor of the Dispatcher base type.
            MethodAttributes ma = MethodAttributes.Public |
                                  MethodAttributes.HideBySig |
                                  MethodAttributes.SpecialName |
                                  MethodAttributes.RTSpecialName;
            var cc = CallingConventions.Standard;
            Type[] args = { ptrtype, typetype };
            ConstructorBuilder cb = tb.DefineConstructor(ma, cc, args);
            ConstructorInfo ci = basetype.GetConstructor(args);
            ILGenerator il = cb.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Call, ci);
            il.Emit(OpCodes.Ret);

            // Method generation: we generate a method named "Invoke" on the
            // dispatcher type, whose signature matches the delegate type for
            // which it is generated. The method body simply packages the
            // arguments and hands them to the Dispatch() method, which deals
            // with converting the arguments, calling the Python method and
            // converting the result of the call.
            MethodInfo method = dtype.GetMethod("Invoke");
            ParameterInfo[] pi = method.GetParameters();

            var signature = new Type[pi.Length];
            for (var i = 0; i < pi.Length; i++)
            {
                signature[i] = pi[i].ParameterType;
            }

            MethodBuilder mb = tb.DefineMethod("Invoke", MethodAttributes.Public, method.ReturnType, signature);

            ConstructorInfo ctor = listtype.GetConstructor(Type.EmptyTypes);
            MethodInfo dispatch = basetype.GetMethod("Dispatch");
            MethodInfo add = listtype.GetMethod("Add");

            il = mb.GetILGenerator();
            il.DeclareLocal(listtype);
            il.Emit(OpCodes.Newobj, ctor);
            il.Emit(OpCodes.Stloc_0);

            for (var c = 0; c < signature.Length; c++)
            {
                Type t = signature[c];
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Ldarg_S, (byte)(c + 1));

                if (t.IsValueType)
                {
                    il.Emit(OpCodes.Box, t);
                }

                il.Emit(OpCodes.Callvirt, add);
                il.Emit(OpCodes.Pop);
            }

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Call, dispatch);

            if (method.ReturnType == voidtype)
            {
                il.Emit(OpCodes.Pop);
            }
            else if (method.ReturnType.IsValueType)
            {
                il.Emit(OpCodes.Unbox_Any, method.ReturnType);
            }

            il.Emit(OpCodes.Ret);

            Type disp = tb.CreateType();
            cache[dtype] = disp;
            return disp;
        }

        /// <summary>
        /// Given a delegate type and a callable Python object, GetDelegate
        /// returns an instance of the delegate type. The delegate instance
        /// returned will dispatch calls to the given Python object.
        /// </summary>
        internal Delegate GetDelegate(Type dtype, IntPtr callable)
        {
            Type dispatcher = GetDispatcher(dtype);
            object[] args = { callable, dtype };
            object o = Activator.CreateInstance(dispatcher, args);
            return Delegate.CreateDelegate(dtype, o, "Invoke");
        }
    }


    /* When a delegate instance is created that has a Python implementation,
       the delegate manager generates a custom subclass of Dispatcher and
       instantiates it, passing the IntPtr of the Python callable.

       The "real" delegate is created using CreateDelegate, passing the
       instance of the generated type and the name of the (generated)
       implementing method (Invoke).

       The true delegate instance holds the only reference to the dispatcher
       instance, which ensures that when the delegate dies, the finalizer
       of the referenced instance will be able to decref the Python
       callable.

       A possible alternate strategy would be to create custom subclasses
       of the required delegate type, storing the IntPtr in it directly.
       This would be slightly cleaner, but I'm not sure if delegates are
       too "special" for this to work. It would be more work, so for now
       the 80/20 rule applies :) */

    public class Dispatcher : IPyDisposable
    {
        public IntPtr target;
        public Type dtype;
        private bool _disposed = false;
        private bool _finalized = false;

        public Dispatcher(IntPtr target, Type dtype)
        {
            Runtime.XIncref(target);
            this.target = target;
            this.dtype = dtype;
        }

        ~Dispatcher()
        {
            if (_finalized || _disposed)
            {
                return;
            }
            _finalized = true;
            Finalizer.Instance.AddFinalizedObject(this);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            Runtime.XDecref(target);
            target = IntPtr.Zero;
            dtype = null;
            GC.SuppressFinalize(this);
        }

        public object Dispatch(ArrayList args)
        {
            IntPtr gs = PythonEngine.AcquireLock();
            object ob = null;

            try
            {
                ob = TrueDispatch(args);
            }
            catch (Exception e)
            {
                PythonEngine.ReleaseLock(gs);
                throw e;
            }

            PythonEngine.ReleaseLock(gs);
            return ob;
        }

        public object TrueDispatch(ArrayList args)
        {
            MethodInfo method = dtype.GetMethod("Invoke");
            ParameterInfo[] pi = method.GetParameters();
            IntPtr pyargs = Runtime.PyTuple_New(pi.Length);
            Type rtype = method.ReturnType;

            for (var i = 0; i < pi.Length; i++)
            {
                // Here we own the reference to the Python value, and we
                // give the ownership to the arg tuple.
                IntPtr arg = Converter.ToPython(args[i], pi[i].ParameterType);
                Runtime.PyTuple_SetItem(pyargs, i, arg);
            }

            IntPtr op = Runtime.PyObject_Call(target, pyargs, IntPtr.Zero);
            Runtime.XDecref(pyargs);

            if (op == IntPtr.Zero)
            {
                var e = new PythonException();
                throw e;
            }

            if (rtype == typeof(void))
            {
                return null;
            }

            object result = null;
            if (!Converter.ToManaged(op, rtype, out result, false))
            {
                Runtime.XDecref(op);
                throw new ConversionException($"could not convert Python result to {rtype}");
            }

            Runtime.XDecref(op);
            return result;
        }

        public IntPtr[] GetTrackedHandles()
        {
            return new IntPtr[] { target };
        }
    }


    public class ConversionException : Exception
    {
        public ConversionException()
        {
        }

        public ConversionException(string msg) : base(msg)
        {
        }
    }
}
