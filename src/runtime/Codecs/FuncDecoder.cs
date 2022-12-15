using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;

namespace Python.Runtime.Codecs
{
    public class FuncDecoder : IPyObjectDecoder
    {
        private ModuleBuilder _moduleBuilder;
        private Dictionary<Type, (Type, MethodBuilder)> _typeCache = new Dictionary<Type, (Type, MethodBuilder)>();
        private static FuncDecoder? _instance;
        FuncDecoder()
        {
            AssemblyName assemblyName = new AssemblyName("decoded");
            AssemblyBuilder builder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            _moduleBuilder = builder.DefineDynamicModule("MainModule");
        }

        public static FuncDecoder Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new FuncDecoder();
                return _instance;
            }
        }

        bool IPyObjectDecoder.CanDecode(PyType objectType, Type targetType)
        {
            if (objectType.Name == "function" && targetType.Name.StartsWith("Func`") && targetType.Namespace == typeof(Func<double>).Namespace)
                return true;
            return false;
        }

        static int counter = 0;
        bool IPyObjectDecoder.TryDecode<T1>(PyObject pyObj, out T1 value)
        {
            bool retVal = TryDecode(pyObj, typeof(T1), out Delegate handler);
            value = (T1)Convert.ChangeType(handler, typeof(T1));
            return retVal;
        }

        MethodInfo? FindMethod(Type type, string name, BindingFlags flags, params Type[] expectedTypes)
        {
            foreach (var method in type.GetMethods(flags))
            {
                if (method.Name == name)
                {
                    var parameters = method.GetParameters();
                    if (parameters.Length == expectedTypes.Length)
                    {
                        bool found = true;
                        for (int i = 0; i < expectedTypes.Length; ++i)
                        {
                            if (parameters[i].ParameterType != expectedTypes[i])
                            {
                                found = false;
                                break;
                            }
                        }
                        if (found)
                            return method;
                    }
                }
            }
            return null;
        }

        private (Type, MethodBuilder) CreateType(Type funcType)
        {
            TypeBuilder typeBuilder = _moduleBuilder.DefineType($"narf{counter++}", TypeAttributes.Public | TypeAttributes.AnsiClass | TypeAttributes.AutoClass | TypeAttributes.AutoLayout | TypeAttributes.BeforeFieldInit | TypeAttributes.Class, null);

            var field = typeBuilder.DefineField("_pyObject", typeof(PyObject), FieldAttributes.Private);
            var constructor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(PyObject) });
            var generator = constructor.GetILGenerator();
            // Invoke Base Class Constructor
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));
            // Load this and argument of constructor, apply it to this.field
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Stfld, field);
            // Return
            generator.Emit(OpCodes.Ret);

            Type[] arguments = funcType.GetGenericArguments();
            Type[] parameters = new Type[0];
            if (arguments.Length > 1)
                parameters = arguments.Take(arguments.Length - 1).ToArray();
            Type returnType = arguments.Last();

            var invokeMethod = typeBuilder.DefineMethod("Invoke", MethodAttributes.Public, returnType, parameters);
            generator = invokeMethod.GetILGenerator();
            // Define local variables
            generator.DeclareLocal(typeof(Py.GILState));
            generator.DeclareLocal(typeof(bool));
            // Invoke Py.GIL and store state to local-0
            generator.Emit(OpCodes.Call, typeof(Py).GetMethod("GIL", BindingFlags.Static | BindingFlags.Public));
            generator.Emit(OpCodes.Stloc_0);

            // Try {...
            var exceptionBlock = generator.BeginExceptionBlock();

            // load field _pyObject from this - we need this for the invoke right after we have created and set the arguments
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldfld, field);
            // Create argument array for invoke call with proper number of arguments
            generator.Emit(OpCodes.Ldc_I4, parameters.Length);
            generator.Emit(OpCodes.Newarr, typeof(PyObject));
            // Set parameters by invoking ToPython on each of them
            for (int i = 0; i < parameters.Length; ++i)
            {
                // Duplicate array as the reference gets lost otherwise
                generator.Emit(OpCodes.Dup);
                // Take constant index i as index into argument array
                generator.Emit(OpCodes.Ldc_I4, i);
                // use the n'th + 1 argument passed into the function as arg0 == this
                generator.Emit(OpCodes.Ldarg, i + 1);
                // Box if argument is value type
                if (parameters[i].IsValueType)
                    generator.Emit(OpCodes.Box, parameters[i]);
                generator.Emit(OpCodes.Call, typeof(ConverterExtension).GetMethod(nameof(ConverterExtension.ToPython), BindingFlags.Public | BindingFlags.Static));
                // Store result into argument array
                generator.Emit(OpCodes.Stelem_Ref);
            }
            // Invoke Invoke-method of _pyObject
            generator.Emit(OpCodes.Callvirt, FindMethod(typeof(PyObject), nameof(PyObject.Invoke), BindingFlags.Public | BindingFlags.Instance, typeof(PyObject[])));
            // As<bool> was hard, this is a try for AsManagedObject but also not convienent
            generator.Emit(OpCodes.Ldtoken, returnType);
            generator.Emit(OpCodes.Call, typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle), BindingFlags.Public | BindingFlags.Static));
            generator.Emit(OpCodes.Callvirt, typeof(PyObject).GetMethod(nameof(PyObject.AsManagedObject), BindingFlags.Public | BindingFlags.Instance));
            generator.Emit(OpCodes.Unbox_Any, returnType);
            // Store Return-Value
            generator.Emit(OpCodes.Stloc_1);
            // End Try }
            generator.Emit(OpCodes.Leave_S, exceptionBlock);
            generator.BeginFinallyBlock();

            generator.Emit(OpCodes.Ldloc_0);
            // Not sure if dispose can be found like that
            generator.Emit(OpCodes.Callvirt, typeof(Py.GILState).GetMethod(nameof(Py.GILState.Dispose), BindingFlags.Public | BindingFlags.Instance));
            // End exception bloc
            generator.EndExceptionBlock();

            // Return result
            generator.Emit(OpCodes.Ldloc_1);
            generator.Emit(OpCodes.Ret);

            for (int i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                invokeMethod.DefineParameter(i + 1, ParameterAttributes.None, $"p{i}");
            }

            return (typeBuilder.CreateType(), invokeMethod);
        }

        private bool TryDecode(PyObject pyObj, Type funcType, out Delegate value)
        {
            if (!_typeCache.TryGetValue(funcType, out (Type type, MethodBuilder methodBuilder) wrapper))
            {
                wrapper = CreateType(funcType);
                _typeCache[funcType] = wrapper;
            }

            object instance = Activator.CreateInstance(wrapper.type, pyObj);
            value = Delegate.CreateDelegate(funcType, instance, wrapper.type.GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance));

            return true;
        }
    }
}
