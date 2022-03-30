using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Python.Runtime.Reflection;
using System.Reflection;

namespace Python.Runtime
{
    /// <summary>
    /// This file defines objects to support binary interop with the Python
    /// runtime. Generally, the definitions here need to be kept up to date
    /// when moving to new Python versions.
    /// </summary>
    [Serializable]
    [AttributeUsage(AttributeTargets.All)]
    public class DocStringAttribute : Attribute
    {
        public DocStringAttribute(string docStr)
        {
            DocString = docStr;
        }

        public string DocString { get; }
    }

    [Serializable]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Delegate)]
    internal class ModuleFunctionAttribute : Attribute
    {
        public ModuleFunctionAttribute()
        {
        }
    }

    [Serializable]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Delegate)]
    internal class ForbidPythonThreadsAttribute : Attribute
    {
        public ForbidPythonThreadsAttribute()
        {
        }
    }


    [Serializable]
    [AttributeUsage(AttributeTargets.Property)]
    internal class ModulePropertyAttribute : Attribute
    {
        public ModulePropertyAttribute()
        {
        }
    }

    /// <summary>
    /// TypeFlags(): The actual bit values for the Type Flags stored
    /// in a class.
    /// Note that the two values reserved for stackless have been put
    /// to good use as PythonNet specific flags (Managed and Subclass)
    /// </summary>
    // Py_TPFLAGS_*
    [Flags]
    public enum TypeFlags: long
    {
        HeapType = (1 << 9),
        BaseType = (1 << 10),
        Ready = (1 << 12),
        Readying = (1 << 13),
        HaveGC = (1 << 14),
        // 15 and 16 are reserved for stackless
        HaveStacklessExtension = 0,
        /* XXX Reusing reserved constants */
        /// <remarks>PythonNet specific</remarks>
        HasClrInstance = (1 << 15),
        /// <remarks>PythonNet specific</remarks>
        Subclass = (1 << 16),
        /* Objects support nb_index in PyNumberMethods */
        HaveVersionTag = (1 << 18),
        ValidVersionTag = (1 << 19),
        IsAbstract = (1 << 20),
        HaveNewBuffer = (1 << 21),
        // TODO: Implement FastSubclass functions
        IntSubclass = (1 << 23),
        LongSubclass = (1 << 24),
        ListSubclass = (1 << 25),
        TupleSubclass = (1 << 26),
        StringSubclass = (1 << 27),
        UnicodeSubclass = (1 << 28),
        DictSubclass = (1 << 29),
        BaseExceptionSubclass = (1 << 30),
        TypeSubclass = (1 << 31),

        Default = (
            HaveStacklessExtension |
            HaveVersionTag),
    }


    // This class defines the function prototypes (delegates) used for low
    // level integration with the CPython runtime. It also provides name
    // based lookup of the correct prototype for a particular Python type
    // slot and utilities for generating method thunks for managed methods.

    internal class Interop
    {
        static readonly Dictionary<MethodInfo, Type> delegateTypes = new();

        internal static Type GetPrototype(MethodInfo method)
        {
            if (delegateTypes.TryGetValue(method, out var delegateType))
                return delegateType;

            var parameters = method.GetParameters().Select(p => new ParameterHelper(p)).ToArray();

            foreach (var candidate in typeof(Interop).GetNestedTypes())
            {
                if (!typeof(Delegate).IsAssignableFrom(candidate))
                    continue;

                MethodInfo invoke = candidate.GetMethod("Invoke");
                var candiateParameters = invoke.GetParameters();
                if (candiateParameters.Length != parameters.Length)
                    continue;

                var parametersMatch = parameters.Zip(candiateParameters,
                    (expected, actual) => expected.Matches(actual))
                    .All(matches => matches);

                if (!parametersMatch) continue;

                if (invoke.ReturnType != method.ReturnType) continue;

                delegateTypes.Add(method, candidate);
                return candidate;
            }

            throw new NotImplementedException(method.ToString());
        }


        internal static Dictionary<IntPtr, Delegate> allocatedThunks = new();

        internal static ThunkInfo GetThunk(MethodInfo method)
        {
            Type dt = GetPrototype(method);
            Delegate d = Delegate.CreateDelegate(dt, method);
            return GetThunk(d);
        }

        internal static ThunkInfo GetThunk(Delegate @delegate)
        {
            var info = new ThunkInfo(@delegate);
            allocatedThunks[info.Address] = @delegate;
            return info;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NewReference B_N(BorrowedReference ob);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NewReference BB_N(BorrowedReference ob, BorrowedReference a);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NewReference BBB_N(BorrowedReference ob, BorrowedReference a1, BorrowedReference a2);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int B_I32(BorrowedReference ob);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int BB_I32(BorrowedReference ob, BorrowedReference a);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int BBB_I32(BorrowedReference ob, BorrowedReference a1, BorrowedReference a2);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int BP_I32(BorrowedReference ob, IntPtr arg);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr B_P(BorrowedReference ob);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NewReference BBI32_N(BorrowedReference ob, BorrowedReference a1, int a2);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NewReference BP_N(BorrowedReference ob, IntPtr arg);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void N_V(NewReference ob);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int BPP_I32(BorrowedReference ob, IntPtr a1, IntPtr a2);
    }


    internal class ThunkInfo
    {
        public readonly Delegate Target;
        public readonly IntPtr Address;

        public ThunkInfo(Delegate target)
        {
            Debug.Assert(target is not null);
            Target = target!;
            Address = Marshal.GetFunctionPointerForDelegate(target);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PyMethodDef
    {
        public IntPtr ml_name;
        public IntPtr ml_meth;
        public int ml_flags;
        public IntPtr ml_doc;
    }

}
