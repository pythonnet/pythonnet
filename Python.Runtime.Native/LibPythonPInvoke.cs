using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using Python.Runtime.Interfaces;

namespace Python.Runtime.Native
{
    public partial class LibPythonPInvoke : ILibPython
    {
        // C# compiler copies constants to the assemblies that references this library.
        // We needs to replace all public constants to static readonly fields to allow
        // binary substitution of different Python.Runtime.dll builds in a target application.

        public static int UCS => _UCS;

#if !UCS2
        internal const int _UCS = 4;

        /// <summary>
        /// EntryPoint to be used in DllImport to map to correct Unicode
        /// methods prior to PEP393. Only used for PY27.
        /// </summary>
        private const string PyUnicodeEntryPoint = "PyUnicodeUCS4_";
#else
        internal const int _UCS = 2;

        /// <summary>
        /// EntryPoint to be used in DllImport to map to correct Unicode
        /// methods prior to PEP393. Only used for PY27.
        /// </summary>
        private const string PyUnicodeEntryPoint = "PyUnicodeUCS2_";
#endif

        internal const string _PythonDll = "__Internal";

        // set to true when python is finalizing
        internal static bool Is32Bit = IntPtr.Size == 4;

        // .NET core: System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        internal static bool IsWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;

        /// <summary>
        /// Encoding to use to convert Unicode to/from Managed to Native
        /// </summary>
        internal static readonly Encoding PyEncoding = _UCS == 2 ? Encoding.Unicode : Encoding.UTF32;

        /// <summary>
        /// Export of Macro Py_XIncRef. Use XIncref instead.
        /// Limit this function usage for Testing and Py_Debug builds
        /// </summary>
        /// <param name="ob">PyObject Ptr</param>
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_IncRef(IntPtr ob);

        /// <summary>
        /// Export of Macro Py_XDecRef. Use XDecref instead.
        /// Limit this function usage for Testing and Py_Debug builds
        /// </summary>
        /// <param name="ob">PyObject Ptr</param>
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_DecRef(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_Initialize();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_InitializeEx(int initsigs);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Py_IsInitialized();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_Finalize();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_NewInterpreter();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_EndInterpreter(IntPtr threadState);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyThreadState_New(IntPtr istate);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyThreadState_Get();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyThread_get_key_value(IntPtr key);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyThread_get_thread_ident();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyThread_set_key_value(IntPtr key, IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyThreadState_Swap(IntPtr key);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyGILState_Ensure();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyGILState_Release(IntPtr gs);


        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyGILState_GetThisThreadState();

#if !PYTHON2
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Py_Main(
            int argc,
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(StrArrayMarshaler))] string[] argv
        );
#else
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Py_Main(int argc, string[] argv);
#endif

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyEval_InitThreads();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyEval_ThreadsInitialized();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyEval_AcquireLock();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyEval_ReleaseLock();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyEval_AcquireThread(IntPtr tstate);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyEval_ReleaseThread(IntPtr tstate);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyEval_SaveThread();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyEval_RestoreThread(IntPtr tstate);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyEval_GetBuiltins();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyEval_GetGlobals();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyEval_GetLocals();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_GetProgramName();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_SetProgramName(IntPtr name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_GetPythonHome();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_SetPythonHome(IntPtr home);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_GetPath();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_SetPath(IntPtr home);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_GetVersion();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_GetPlatform();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_GetCopyright();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_GetCompiler();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_GetBuildInfo();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyRun_SimpleString(string code);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyRun_String(string code, IntPtr st, IntPtr globals, IntPtr locals);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyEval_EvalCode(IntPtr co, IntPtr globals, IntPtr locals);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_CompileString(string code, string file, IntPtr tok);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyImport_ExecCodeModule(string name, IntPtr code);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyCFunction_NewEx(IntPtr ml, IntPtr self, IntPtr mod);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyCFunction_Call(IntPtr func, IntPtr args, IntPtr kw);

#if PYTHON2
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyClass_New(IntPtr bases, IntPtr dict, IntPtr name);
#endif

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyInstance_New(IntPtr cls, IntPtr args, IntPtr kw);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyInstance_NewRaw(IntPtr cls, IntPtr dict);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyMethod_New(IntPtr func, IntPtr self, IntPtr cls);


        //====================================================================
        // Python abstract object API
        //====================================================================

        /// <summary>
        /// A macro-like method to get the type of a Python object. This is
        /// designed to be lean and mean in IL &amp; avoid managed &lt;-&gt; unmanaged
        /// transitions. Note that this does not incref the type object.
        /// </summary>
        internal static unsafe IntPtr PyObject_TYPE(IntPtr op)
        {
            var p = (void*)op;
            if ((void*)0 == p)
            {
                return IntPtr.Zero;
            }
#if PYTHON_WITH_PYDEBUG
            var n = 3;
#else
            var n = 1;
#endif
            return Is32Bit
                ? new IntPtr((void*)(*((uint*)p + n)))
                : new IntPtr((void*)(*((ulong*)p + n)));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_HasAttrString(IntPtr pointer, string name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_GetAttrString(IntPtr pointer, string name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_SetAttrString(IntPtr pointer, string name, IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_HasAttr(IntPtr pointer, IntPtr name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_GetAttr(IntPtr pointer, IntPtr name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_SetAttr(IntPtr pointer, IntPtr name, IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_GetItem(IntPtr pointer, IntPtr key);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_SetItem(IntPtr pointer, IntPtr key, IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_DelItem(IntPtr pointer, IntPtr key);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_GetIter(IntPtr op);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Call(IntPtr pointer, IntPtr args, IntPtr kw);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_CallObject(IntPtr pointer, IntPtr args);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_RichCompareBool(IntPtr value1, IntPtr value2, int opid);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_IsInstance(IntPtr ob, IntPtr type);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_IsSubclass(IntPtr ob, IntPtr type);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyCallable_Check(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_IsTrue(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_Not(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PyObject_Size")]
        private static extern IntPtr _PyObject_Size(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Hash(IntPtr op);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Repr(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Str(IntPtr pointer);

#if !PYTHON2
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyObject_Str")]
        internal static extern IntPtr PyObject_Unicode(IntPtr pointer);
#else
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Unicode(IntPtr pointer);
#endif

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Dir(IntPtr pointer);


        //====================================================================
        // Python number API
        //====================================================================

#if !PYTHON2
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyNumber_Long")]
        internal static extern IntPtr PyNumber_Int(IntPtr ob);
#else
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Int(IntPtr ob);
#endif

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Long(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Float(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool PyNumber_Check(IntPtr ob);

#if !PYTHON2
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_FromLong")]
        private static extern IntPtr PyInt_FromLong(IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_AsLong")]
        internal static extern int PyInt_AsLong(IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_FromString")]
        internal static extern IntPtr PyInt_FromString(string value, IntPtr end, int radix);
#else
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyInt_FromLong(IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyInt_AsLong(IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyInt_FromString(string value, IntPtr end, int radix);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyInt_GetMax();
#endif

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyLong_FromLong(long value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_FromUnsignedLong")]
        internal static extern IntPtr PyLong_FromUnsignedLong32(uint value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_FromUnsignedLong")]
        internal static extern IntPtr PyLong_FromUnsignedLong64(ulong value);

        internal static IntPtr PyLong_FromUnsignedLong(object value)
        {
            if(Is32Bit || IsWindows)
                return PyLong_FromUnsignedLong32(Convert.ToUInt32(value));
            else
                return PyLong_FromUnsignedLong64(Convert.ToUInt64(value));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyLong_FromDouble(double value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyLong_FromLongLong(long value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyLong_FromUnsignedLongLong(ulong value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyLong_FromString(string value, IntPtr end, int radix);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyLong_AsLong(IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_AsUnsignedLong")]
        internal static extern uint PyLong_AsUnsignedLong32(IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_AsUnsignedLong")]
        internal static extern ulong PyLong_AsUnsignedLong64(IntPtr value);

        internal static object PyLong_AsUnsignedLong(IntPtr value)
        {
            if (Is32Bit || IsWindows)
                return PyLong_AsUnsignedLong32(value);
            else
                return PyLong_AsUnsignedLong64(value);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern long PyLong_AsLongLong(IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ulong PyLong_AsUnsignedLongLong(IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyFloat_FromDouble(double value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyFloat_FromString(IntPtr value, IntPtr junk);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern double PyFloat_AsDouble(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Add(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Subtract(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Multiply(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_TrueDivide(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_And(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Xor(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Or(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Lshift(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Rshift(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Power(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Remainder(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceAdd(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceSubtract(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceMultiply(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceTrueDivide(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceAnd(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceXor(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceOr(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceLshift(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceRshift(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlacePower(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceRemainder(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Negative(IntPtr o1);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Positive(IntPtr o1);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Invert(IntPtr o1);


        //====================================================================
        // Python sequence API
        //====================================================================

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool PySequence_Check(IntPtr pointer);

        internal static IntPtr PySequence_GetItem(IntPtr pointer, long index)
        {
            return PySequence_GetItem(pointer, new IntPtr(index));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PySequence_GetItem(IntPtr pointer, IntPtr index);

        internal static int PySequence_SetItem(IntPtr pointer, long index, IntPtr value)
        {
            return PySequence_SetItem(pointer, new IntPtr(index), value);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PySequence_SetItem(IntPtr pointer, IntPtr index, IntPtr value);

        internal static int PySequence_DelItem(IntPtr pointer, long index)
        {
            return PySequence_DelItem(pointer, new IntPtr(index));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PySequence_DelItem(IntPtr pointer, IntPtr index);

        internal static IntPtr PySequence_GetSlice(IntPtr pointer, long i1, long i2)
        {
            return PySequence_GetSlice(pointer, new IntPtr(i1), new IntPtr(i2));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PySequence_GetSlice(IntPtr pointer, IntPtr i1, IntPtr i2);

        internal static int PySequence_SetSlice(IntPtr pointer, long i1, long i2, IntPtr v)
        {
            return PySequence_SetSlice(pointer, new IntPtr(i1), new IntPtr(i2), v);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PySequence_SetSlice(IntPtr pointer, IntPtr i1, IntPtr i2, IntPtr v);

        internal static int PySequence_DelSlice(IntPtr pointer, long i1, long i2)
        {
            return PySequence_DelSlice(pointer, new IntPtr(i1), new IntPtr(i2));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PySequence_DelSlice(IntPtr pointer, IntPtr i1, IntPtr i2);

        internal static long PySequence_Size(IntPtr pointer)
        {
            return (long) _PySequence_Size(pointer);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PySequence_Size")]
        private static extern IntPtr _PySequence_Size(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PySequence_Contains(IntPtr pointer, IntPtr item);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PySequence_Concat(IntPtr pointer, IntPtr other);

        internal static IntPtr PySequence_Repeat(IntPtr pointer, long count)
        {
            return PySequence_Repeat(pointer, new IntPtr(count));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PySequence_Repeat(IntPtr pointer, IntPtr count);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PySequence_Index(IntPtr pointer, IntPtr item);

        internal static long PySequence_Count(IntPtr pointer, IntPtr value)
        {
            return (long) _PySequence_Count(pointer, value);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PySequence_Count")]
        private static extern IntPtr _PySequence_Count(IntPtr pointer, IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PySequence_Tuple(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PySequence_List(IntPtr pointer);


        //====================================================================
        // Python string API
        //====================================================================

        internal static IntPtr PyString_FromString(string value)
        {
#if !PYTHON2
            return PyUnicode_FromKindAndData(_UCS, value, value.Length);
#else
            return PyString_FromStringAndSize(value, value.Length);
#endif
        }

#if !PYTHON2
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyBytes_FromString(string op);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PyBytes_Size")]
        private static extern IntPtr _PyBytes_Size(IntPtr op);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyUnicode_FromStringAndSize")]
        internal static extern IntPtr _PyString_FromStringAndSize(
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Utf8Marshaler))] string value,
            IntPtr size
        );

        internal static IntPtr PyUnicode_FromStringAndSize(IntPtr value, long size)
        {
            return PyUnicode_FromStringAndSize(value, new IntPtr(size));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyUnicode_FromStringAndSize(IntPtr value, IntPtr size);
#else
        internal static IntPtr PyString_FromStringAndSize(string value, long size)
        {
            return PyString_FromStringAndSize(value, new IntPtr(size));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyString_FromStringAndSize(string value, IntPtr size);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyString_AsString(IntPtr op);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyString_Size(IntPtr pointer);
#endif

#if !PYTHON2
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyUnicode_FromObject(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyUnicode_FromEncodedObject(IntPtr ob, IntPtr enc, IntPtr err);

        internal static IntPtr PyUnicode_FromKindAndData(int kind, string s, long size)
        {
            return PyUnicode_FromKindAndData(kind, s, new IntPtr(size));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyUnicode_FromKindAndData(
            int kind,
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(UcsMarshaler))] string s,
            IntPtr size
        );

        internal static IntPtr PyUnicode_FromUnicode(string s, long size)
        {
            return PyUnicode_FromKindAndData(_UCS, s, size);
        }

        internal static long PyUnicode_GetSize(IntPtr ob)
        {
            return (long)_PyUnicode_GetSize(ob);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PyUnicode_GetSize")]
        private static extern IntPtr _PyUnicode_GetSize(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyUnicode_AsUnicode(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyUnicode_FromOrdinal(int c);
#else
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = PyUnicodeEntryPoint + "FromObject")]
        internal static extern IntPtr PyUnicode_FromObject(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = PyUnicodeEntryPoint + "FromEncodedObject")]
        internal static extern IntPtr PyUnicode_FromEncodedObject(IntPtr ob, IntPtr enc, IntPtr err);

        internal static IntPtr PyUnicode_FromUnicode(string s, long size)
        {
            return PyUnicode_FromUnicode(s, new IntPtr(size));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = PyUnicodeEntryPoint + "FromUnicode")]
        private static extern IntPtr PyUnicode_FromUnicode(
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(UcsMarshaler))] string s,
            IntPtr size
        );

        internal static long PyUnicode_GetSize(IntPtr ob)
        {
            return (long) _PyUnicode_GetSize(ob);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = PyUnicodeEntryPoint + "GetSize")]
        internal static extern IntPtr _PyUnicode_GetSize(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = PyUnicodeEntryPoint + "AsUnicode")]
        internal static extern IntPtr PyUnicode_AsUnicode(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = PyUnicodeEntryPoint + "FromOrdinal")]
        internal static extern IntPtr PyUnicode_FromOrdinal(int c);
#endif

        //====================================================================
        // Python dictionary API
        //====================================================================

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDict_New();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDictProxy_New(IntPtr dict);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDict_GetItem(IntPtr pointer, IntPtr key);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDict_GetItemString(IntPtr pointer, string key);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyDict_SetItem(IntPtr pointer, IntPtr key, IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyDict_SetItemString(IntPtr pointer, string key, IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyDict_DelItem(IntPtr pointer, IntPtr key);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyDict_DelItemString(IntPtr pointer, string key);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyMapping_HasKey(IntPtr pointer, IntPtr key);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDict_Keys(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDict_Values(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDict_Items(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDict_Copy(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyDict_Update(IntPtr pointer, IntPtr other);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyDict_Clear(IntPtr pointer);

        internal static long PyDict_Size(IntPtr pointer)
        {
            return (long) _PyDict_Size(pointer);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PyDict_Size")]
        internal static extern IntPtr _PyDict_Size(IntPtr pointer);


        //====================================================================
        // Python list API
        //====================================================================

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyList_New(IntPtr size);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyList_AsTuple(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyList_GetItem(IntPtr pointer, IntPtr index);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PyList_SetItem(IntPtr pointer, IntPtr index, IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PyList_Insert(IntPtr pointer, IntPtr index, IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyList_Append(IntPtr pointer, IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyList_Reverse(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyList_Sort(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyList_GetSlice(IntPtr pointer, IntPtr start, IntPtr end);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PyList_SetSlice(IntPtr pointer, IntPtr start, IntPtr end, IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PyList_Size")]
        private static extern IntPtr _PyList_Size(IntPtr pointer);

        //====================================================================
        // Python tuple API
        //====================================================================

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyTuple_New(IntPtr size);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyTuple_GetItem(IntPtr pointer, IntPtr index);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PyTuple_SetItem(IntPtr pointer, IntPtr index, IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyTuple_GetSlice(IntPtr pointer, IntPtr start, IntPtr end);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PyTuple_Size")]
        private static extern IntPtr _PyTuple_Size(IntPtr pointer);


        //====================================================================
        // Python iterator API
        //====================================================================

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyIter_Next(IntPtr pointer);


        //====================================================================
        // Python module API
        //====================================================================

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyModule_New(string name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern string PyModule_GetName(IntPtr module);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyModule_GetDict(IntPtr module);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern string PyModule_GetFilename(IntPtr module);

#if !PYTHON2
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyModule_Create2(IntPtr module, int apiver);
#endif

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyImport_Import(IntPtr name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyImport_ImportModule(string name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyImport_ReloadModule(IntPtr module);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyImport_AddModule(string name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyImport_GetModuleDict();

#if !PYTHON2
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PySys_SetArgvEx(
            int argc,
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(StrArrayMarshaler))] string[] argv,
            int updatepath
        );
#else
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PySys_SetArgvEx(
            int argc,
            string[] argv,
            int updatepath
        );
#endif

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PySys_GetObject(string name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PySys_SetObject(string name, IntPtr ob);


        //====================================================================
        // Python type object API
        //====================================================================

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyType_Modified(IntPtr type);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool PyType_IsSubtype(IntPtr t1, IntPtr t2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyType_GenericNew(IntPtr type, IntPtr args, IntPtr kw);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyType_GenericAlloc(IntPtr type, IntPtr n);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyType_Ready(IntPtr type);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr _PyType_Lookup(IntPtr type, IntPtr name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_GenericGetAttr(IntPtr obj, IntPtr name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_GenericSetAttr(IntPtr obj, IntPtr name, IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr _PyObject_GetDictPtr(IntPtr obj);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_GC_New(IntPtr tp);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyObject_GC_Del(IntPtr tp);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyObject_GC_Track(IntPtr tp);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyObject_GC_UnTrack(IntPtr tp);


        //====================================================================
        // Python memory API
        //====================================================================

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyMem_Malloc(IntPtr size);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyMem_Realloc(IntPtr ptr, IntPtr size);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyMem_Free(IntPtr ptr);


        //====================================================================
        // Python exception API
        //====================================================================

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_SetString(IntPtr ob, string message);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_SetObject(IntPtr ob, IntPtr message);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyErr_SetFromErrno(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_SetNone(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyErr_ExceptionMatches(IntPtr exception);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyErr_GivenExceptionMatches(IntPtr ob, IntPtr val);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_NormalizeException(IntPtr ob, IntPtr val, IntPtr tb);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyErr_Occurred();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_Fetch(ref IntPtr ob, ref IntPtr val, ref IntPtr tb);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_Restore(IntPtr ob, IntPtr val, IntPtr tb);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_Clear();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_Print();


        //====================================================================
        // Miscellaneous
        //====================================================================

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyMethod_Self(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyMethod_Function(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Py_AddPendingCall(IntPtr func, IntPtr arg);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Py_MakePendingCalls();


        static void SetPyNoSiteFlag(int val) {

        }

        static int GetPyNoSiteFlag() {
            return 0;
        }

        /*
        {
            var loader = LibraryLoader.Get(OperatingSystem);

            Py_NoSiteFlag = loader.GetFunction(IntPtr.Zero, "Py_NoSiteFlag");
            Marshal.WriteInt32(Py_NoSiteFlag, 1);
        } */
    }
}
