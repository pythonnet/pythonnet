using System;

using Python.Runtime.Native;
using Python.Runtime.Platform;

namespace Python.Runtime;

public unsafe partial class Runtime
{
    internal static class Delegates
    {
        static readonly ILibraryLoader libraryLoader = LibraryLoader.Instance;

        static Delegates()
        {
            Py_IncRef = (delegate* unmanaged[Cdecl]<BorrowedReference, void>)GetFunctionByName(nameof(Py_IncRef), GetUnmanagedDll(_PythonDll));
            Py_DecRef = (delegate* unmanaged[Cdecl]<StolenReference, void>)GetFunctionByName(nameof(Py_DecRef), GetUnmanagedDll(_PythonDll));
            Py_Initialize = (delegate* unmanaged[Cdecl]<void>)GetFunctionByName(nameof(Py_Initialize), GetUnmanagedDll(_PythonDll));
            Py_InitializeEx = (delegate* unmanaged[Cdecl]<int, void>)GetFunctionByName(nameof(Py_InitializeEx), GetUnmanagedDll(_PythonDll));
            Py_IsInitialized = (delegate* unmanaged[Cdecl]<int>)GetFunctionByName(nameof(Py_IsInitialized), GetUnmanagedDll(_PythonDll));
            Py_Finalize = (delegate* unmanaged[Cdecl]<void>)GetFunctionByName(nameof(Py_Finalize), GetUnmanagedDll(_PythonDll));
            Py_NewInterpreter = (delegate* unmanaged[Cdecl]<PyThreadState*>)GetFunctionByName(nameof(Py_NewInterpreter), GetUnmanagedDll(_PythonDll));
            Py_EndInterpreter = (delegate* unmanaged[Cdecl]<PyThreadState*, void>)GetFunctionByName(nameof(Py_EndInterpreter), GetUnmanagedDll(_PythonDll));
            PyThreadState_New = (delegate* unmanaged[Cdecl]<PyInterpreterState*, PyThreadState*>)GetFunctionByName(nameof(PyThreadState_New), GetUnmanagedDll(_PythonDll));
            PyThreadState_Get = (delegate* unmanaged[Cdecl]<PyThreadState*>)GetFunctionByName(nameof(PyThreadState_Get), GetUnmanagedDll(_PythonDll));
            try
            {
                // Up until Python 3.13, this function was private and named
                // slightly differently.
                PyThreadState_GetUnchecked = (delegate* unmanaged[Cdecl]<PyThreadState*>)GetFunctionByName("_PyThreadState_UncheckedGet", GetUnmanagedDll(_PythonDll));
            }
            catch (MissingMethodException)
            {

                PyThreadState_GetUnchecked = (delegate* unmanaged[Cdecl]<PyThreadState*>)GetFunctionByName(nameof(PyThreadState_GetUnchecked), GetUnmanagedDll(_PythonDll));
            }
            try
            {
                PyGILState_Check = (delegate* unmanaged[Cdecl]<int>)GetFunctionByName(nameof(PyGILState_Check), GetUnmanagedDll(_PythonDll));
            }
            catch (MissingMethodException e)
            {
                throw new NotSupportedException(Util.MinimalPythonVersionRequired, innerException: e);
            }
            PyGILState_Ensure = (delegate* unmanaged[Cdecl]<PyGILState>)GetFunctionByName(nameof(PyGILState_Ensure), GetUnmanagedDll(_PythonDll));
            PyGILState_Release = (delegate* unmanaged[Cdecl]<PyGILState, void>)GetFunctionByName(nameof(PyGILState_Release), GetUnmanagedDll(_PythonDll));
            PyGILState_GetThisThreadState = (delegate* unmanaged[Cdecl]<PyThreadState*>)GetFunctionByName(nameof(PyGILState_GetThisThreadState), GetUnmanagedDll(_PythonDll));
            PyEval_InitThreads = (delegate* unmanaged[Cdecl]<void>)GetFunctionByName(nameof(PyEval_InitThreads), GetUnmanagedDll(_PythonDll));
            PyEval_ThreadsInitialized = (delegate* unmanaged[Cdecl]<int>)GetFunctionByName(nameof(PyEval_ThreadsInitialized), GetUnmanagedDll(_PythonDll));
            PyEval_AcquireLock = (delegate* unmanaged[Cdecl]<void>)GetFunctionByName(nameof(PyEval_AcquireLock), GetUnmanagedDll(_PythonDll));
            PyEval_ReleaseLock = (delegate* unmanaged[Cdecl]<void>)GetFunctionByName(nameof(PyEval_ReleaseLock), GetUnmanagedDll(_PythonDll));
            PyEval_AcquireThread = (delegate* unmanaged[Cdecl]<PyThreadState*, void>)GetFunctionByName(nameof(PyEval_AcquireThread), GetUnmanagedDll(_PythonDll));
            PyEval_ReleaseThread = (delegate* unmanaged[Cdecl]<PyThreadState*, void>)GetFunctionByName(nameof(PyEval_ReleaseThread), GetUnmanagedDll(_PythonDll));
            PyEval_SaveThread = (delegate* unmanaged[Cdecl]<PyThreadState*>)GetFunctionByName(nameof(PyEval_SaveThread), GetUnmanagedDll(_PythonDll));
            PyEval_RestoreThread = (delegate* unmanaged[Cdecl]<PyThreadState*, void>)GetFunctionByName(nameof(PyEval_RestoreThread), GetUnmanagedDll(_PythonDll));
            PyEval_GetBuiltins = (delegate* unmanaged[Cdecl]<BorrowedReference>)GetFunctionByName(nameof(PyEval_GetBuiltins), GetUnmanagedDll(_PythonDll));
            PyEval_GetGlobals = (delegate* unmanaged[Cdecl]<BorrowedReference>)GetFunctionByName(nameof(PyEval_GetGlobals), GetUnmanagedDll(_PythonDll));
            PyEval_GetLocals = (delegate* unmanaged[Cdecl]<BorrowedReference>)GetFunctionByName(nameof(PyEval_GetLocals), GetUnmanagedDll(_PythonDll));
            Py_GetProgramName = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(Py_GetProgramName), GetUnmanagedDll(_PythonDll));
            Py_SetProgramName = (delegate* unmanaged[Cdecl]<IntPtr, void>)GetFunctionByName(nameof(Py_SetProgramName), GetUnmanagedDll(_PythonDll));
            Py_GetPythonHome = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(Py_GetPythonHome), GetUnmanagedDll(_PythonDll));
            Py_SetPythonHome = (delegate* unmanaged[Cdecl]<IntPtr, void>)GetFunctionByName(nameof(Py_SetPythonHome), GetUnmanagedDll(_PythonDll));
            Py_GetPath = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(Py_GetPath), GetUnmanagedDll(_PythonDll));
            Py_SetPath = (delegate* unmanaged[Cdecl]<IntPtr, void>)GetFunctionByName(nameof(Py_SetPath), GetUnmanagedDll(_PythonDll));
            Py_GetVersion = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(Py_GetVersion), GetUnmanagedDll(_PythonDll));
            Py_GetPlatform = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(Py_GetPlatform), GetUnmanagedDll(_PythonDll));
            Py_GetCopyright = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(Py_GetCopyright), GetUnmanagedDll(_PythonDll));
            Py_GetCompiler = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(Py_GetCompiler), GetUnmanagedDll(_PythonDll));
            Py_GetBuildInfo = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(Py_GetBuildInfo), GetUnmanagedDll(_PythonDll));
            PyRun_SimpleStringFlags = (delegate* unmanaged[Cdecl]<StrPtr, in PyCompilerFlags, int>)GetFunctionByName(nameof(PyRun_SimpleStringFlags), GetUnmanagedDll(_PythonDll));
            PyRun_StringFlags = (delegate* unmanaged[Cdecl]<StrPtr, RunFlagType, BorrowedReference, BorrowedReference, in PyCompilerFlags, NewReference>)GetFunctionByName(nameof(PyRun_StringFlags), GetUnmanagedDll(_PythonDll));
            PyEval_EvalCode = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyEval_EvalCode), GetUnmanagedDll(_PythonDll));
            Py_CompileStringObject = (delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference, int, in PyCompilerFlags, int, NewReference>)GetFunctionByName(nameof(Py_CompileStringObject), GetUnmanagedDll(_PythonDll));
            PyImport_ExecCodeModule = (delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyImport_ExecCodeModule), GetUnmanagedDll(_PythonDll));
            PyObject_HasAttrString = (delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, int>)GetFunctionByName(nameof(PyObject_HasAttrString), GetUnmanagedDll(_PythonDll));
            PyObject_GetAttrString = (delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, NewReference>)GetFunctionByName(nameof(PyObject_GetAttrString), GetUnmanagedDll(_PythonDll));
            PyObject_SetAttrString = (delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, BorrowedReference, int>)GetFunctionByName(nameof(PyObject_SetAttrString), GetUnmanagedDll(_PythonDll));
            PyObject_HasAttr = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyObject_HasAttr), GetUnmanagedDll(_PythonDll));
            PyObject_GetAttr = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyObject_GetAttr), GetUnmanagedDll(_PythonDll));
            PyObject_SetAttr = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyObject_SetAttr), GetUnmanagedDll(_PythonDll));
            PyObject_GetItem = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyObject_GetItem), GetUnmanagedDll(_PythonDll));
            PyObject_SetItem = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyObject_SetItem), GetUnmanagedDll(_PythonDll));
            PyObject_DelItem = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyObject_DelItem), GetUnmanagedDll(_PythonDll));
            PyObject_GetIter = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyObject_GetIter), GetUnmanagedDll(_PythonDll));
            PyObject_Call = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyObject_Call), GetUnmanagedDll(_PythonDll));
            PyObject_CallObject = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyObject_CallObject), GetUnmanagedDll(_PythonDll));
            PyObject_RichCompareBool = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int, int>)GetFunctionByName(nameof(PyObject_RichCompareBool), GetUnmanagedDll(_PythonDll));
            PyObject_IsInstance = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyObject_IsInstance), GetUnmanagedDll(_PythonDll));
            PyObject_IsSubclass = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyObject_IsSubclass), GetUnmanagedDll(_PythonDll));
            PyObject_ClearWeakRefs = (delegate* unmanaged[Cdecl]<BorrowedReference, void>)GetFunctionByName(nameof(PyObject_ClearWeakRefs), GetUnmanagedDll(_PythonDll));
            PyCallable_Check = (delegate* unmanaged[Cdecl]<BorrowedReference, int>)GetFunctionByName(nameof(PyCallable_Check), GetUnmanagedDll(_PythonDll));
            PyObject_IsTrue = (delegate* unmanaged[Cdecl]<BorrowedReference, int>)GetFunctionByName(nameof(PyObject_IsTrue), GetUnmanagedDll(_PythonDll));
            PyObject_Not = (delegate* unmanaged[Cdecl]<BorrowedReference, int>)GetFunctionByName(nameof(PyObject_Not), GetUnmanagedDll(_PythonDll));
            PyObject_Size = (delegate* unmanaged[Cdecl]<BorrowedReference, nint>)GetFunctionByName("PyObject_Size", GetUnmanagedDll(_PythonDll));
            PyObject_Hash = (delegate* unmanaged[Cdecl]<BorrowedReference, nint>)GetFunctionByName(nameof(PyObject_Hash), GetUnmanagedDll(_PythonDll));
            PyObject_Repr = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyObject_Repr), GetUnmanagedDll(_PythonDll));
            PyObject_Str = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyObject_Str), GetUnmanagedDll(_PythonDll));
            PyObject_Type = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyObject_Type), GetUnmanagedDll(_PythonDll));
            PyObject_Dir = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyObject_Dir), GetUnmanagedDll(_PythonDll));
            PyObject_GetBuffer = (delegate* unmanaged[Cdecl]<BorrowedReference, out Py_buffer, int, int>)GetFunctionByName(nameof(PyObject_GetBuffer), GetUnmanagedDll(_PythonDll));
            PyBuffer_Release = (delegate* unmanaged[Cdecl]<ref Py_buffer, void>)GetFunctionByName(nameof(PyBuffer_Release), GetUnmanagedDll(_PythonDll));
            try
            {
                PyBuffer_SizeFromFormat = (delegate* unmanaged[Cdecl]<StrPtr, IntPtr>)GetFunctionByName(nameof(PyBuffer_SizeFromFormat), GetUnmanagedDll(_PythonDll));
            }
            catch (MissingMethodException)
            {
                // only in 3.9+
            }
            PyBuffer_IsContiguous = (delegate* unmanaged[Cdecl]<ref Py_buffer, char, int>)GetFunctionByName(nameof(PyBuffer_IsContiguous), GetUnmanagedDll(_PythonDll));
            PyBuffer_GetPointer = (delegate* unmanaged[Cdecl]<ref Py_buffer, nint[], IntPtr>)GetFunctionByName(nameof(PyBuffer_GetPointer), GetUnmanagedDll(_PythonDll));
            PyBuffer_FromContiguous = (delegate* unmanaged[Cdecl]<ref Py_buffer, IntPtr, IntPtr, char, int>)GetFunctionByName(nameof(PyBuffer_FromContiguous), GetUnmanagedDll(_PythonDll));
            PyBuffer_ToContiguous = (delegate* unmanaged[Cdecl]<IntPtr, ref Py_buffer, IntPtr, char, int>)GetFunctionByName(nameof(PyBuffer_ToContiguous), GetUnmanagedDll(_PythonDll));
            PyBuffer_FillContiguousStrides = (delegate* unmanaged[Cdecl]<int, IntPtr, IntPtr, int, char, void>)GetFunctionByName(nameof(PyBuffer_FillContiguousStrides), GetUnmanagedDll(_PythonDll));
            PyBuffer_FillInfo = (delegate* unmanaged[Cdecl]<ref Py_buffer, BorrowedReference, IntPtr, IntPtr, int, int, int>)GetFunctionByName(nameof(PyBuffer_FillInfo), GetUnmanagedDll(_PythonDll));
            PyNumber_Long = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Long), GetUnmanagedDll(_PythonDll));
            PyNumber_Float = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Float), GetUnmanagedDll(_PythonDll));
            PyNumber_Check = (delegate* unmanaged[Cdecl]<BorrowedReference, bool>)GetFunctionByName(nameof(PyNumber_Check), GetUnmanagedDll(_PythonDll));
            PyLong_FromLongLong = (delegate* unmanaged[Cdecl]<long, NewReference>)GetFunctionByName(nameof(PyLong_FromLongLong), GetUnmanagedDll(_PythonDll));
            PyLong_FromUnsignedLongLong = (delegate* unmanaged[Cdecl]<ulong, NewReference>)GetFunctionByName(nameof(PyLong_FromUnsignedLongLong), GetUnmanagedDll(_PythonDll));
            PyLong_FromString = (delegate* unmanaged[Cdecl]<StrPtr, IntPtr, int, NewReference>)GetFunctionByName(nameof(PyLong_FromString), GetUnmanagedDll(_PythonDll));
            PyLong_AsLongLong = (delegate* unmanaged[Cdecl]<BorrowedReference, long>)GetFunctionByName(nameof(PyLong_AsLongLong), GetUnmanagedDll(_PythonDll));
            PyLong_AsUnsignedLongLong = (delegate* unmanaged[Cdecl]<BorrowedReference, ulong>)GetFunctionByName(nameof(PyLong_AsUnsignedLongLong), GetUnmanagedDll(_PythonDll));
            PyLong_FromVoidPtr = (delegate* unmanaged[Cdecl]<IntPtr, NewReference>)GetFunctionByName(nameof(PyLong_FromVoidPtr), GetUnmanagedDll(_PythonDll));
            PyLong_AsVoidPtr = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr>)GetFunctionByName(nameof(PyLong_AsVoidPtr), GetUnmanagedDll(_PythonDll));
            PyFloat_FromDouble = (delegate* unmanaged[Cdecl]<double, NewReference>)GetFunctionByName(nameof(PyFloat_FromDouble), GetUnmanagedDll(_PythonDll));
            PyFloat_FromString = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyFloat_FromString), GetUnmanagedDll(_PythonDll));
            PyFloat_AsDouble = (delegate* unmanaged[Cdecl]<BorrowedReference, double>)GetFunctionByName(nameof(PyFloat_AsDouble), GetUnmanagedDll(_PythonDll));
            PyNumber_Add = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Add), GetUnmanagedDll(_PythonDll));
            PyNumber_Subtract = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Subtract), GetUnmanagedDll(_PythonDll));
            PyNumber_Multiply = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Multiply), GetUnmanagedDll(_PythonDll));
            PyNumber_TrueDivide = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_TrueDivide), GetUnmanagedDll(_PythonDll));
            PyNumber_And = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_And), GetUnmanagedDll(_PythonDll));
            PyNumber_Xor = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Xor), GetUnmanagedDll(_PythonDll));
            PyNumber_Or = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Or), GetUnmanagedDll(_PythonDll));
            PyNumber_Lshift = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Lshift), GetUnmanagedDll(_PythonDll));
            PyNumber_Rshift = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Rshift), GetUnmanagedDll(_PythonDll));
            PyNumber_Power = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Power), GetUnmanagedDll(_PythonDll));
            PyNumber_Remainder = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Remainder), GetUnmanagedDll(_PythonDll));
            PyNumber_InPlaceAdd = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_InPlaceAdd), GetUnmanagedDll(_PythonDll));
            PyNumber_InPlaceSubtract = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_InPlaceSubtract), GetUnmanagedDll(_PythonDll));
            PyNumber_InPlaceMultiply = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_InPlaceMultiply), GetUnmanagedDll(_PythonDll));
            PyNumber_InPlaceTrueDivide = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_InPlaceTrueDivide), GetUnmanagedDll(_PythonDll));
            PyNumber_InPlaceAnd = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_InPlaceAnd), GetUnmanagedDll(_PythonDll));
            PyNumber_InPlaceXor = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_InPlaceXor), GetUnmanagedDll(_PythonDll));
            PyNumber_InPlaceOr = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_InPlaceOr), GetUnmanagedDll(_PythonDll));
            PyNumber_InPlaceLshift = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_InPlaceLshift), GetUnmanagedDll(_PythonDll));
            PyNumber_InPlaceRshift = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_InPlaceRshift), GetUnmanagedDll(_PythonDll));
            PyNumber_InPlacePower = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_InPlacePower), GetUnmanagedDll(_PythonDll));
            PyNumber_InPlaceRemainder = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_InPlaceRemainder), GetUnmanagedDll(_PythonDll));
            PyNumber_Negative = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Negative), GetUnmanagedDll(_PythonDll));
            PyNumber_Positive = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Positive), GetUnmanagedDll(_PythonDll));
            PyNumber_Invert = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Invert), GetUnmanagedDll(_PythonDll));
            PySequence_Check = (delegate* unmanaged[Cdecl]<BorrowedReference, bool>)GetFunctionByName(nameof(PySequence_Check), GetUnmanagedDll(_PythonDll));
            PySequence_GetItem = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, NewReference>)GetFunctionByName(nameof(PySequence_GetItem), GetUnmanagedDll(_PythonDll));
            PySequence_SetItem = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, BorrowedReference, int>)GetFunctionByName(nameof(PySequence_SetItem), GetUnmanagedDll(_PythonDll));
            PySequence_DelItem = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, int>)GetFunctionByName(nameof(PySequence_DelItem), GetUnmanagedDll(_PythonDll));
            PySequence_GetSlice = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, nint, NewReference>)GetFunctionByName(nameof(PySequence_GetSlice), GetUnmanagedDll(_PythonDll));
            PySequence_SetSlice = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, nint, BorrowedReference, int>)GetFunctionByName(nameof(PySequence_SetSlice), GetUnmanagedDll(_PythonDll));
            PySequence_DelSlice = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, nint, int>)GetFunctionByName(nameof(PySequence_DelSlice), GetUnmanagedDll(_PythonDll));
            PySequence_Size = (delegate* unmanaged[Cdecl]<BorrowedReference, nint>)GetFunctionByName(nameof(PySequence_Size), GetUnmanagedDll(_PythonDll));
            PySequence_Contains = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PySequence_Contains), GetUnmanagedDll(_PythonDll));
            PySequence_Concat = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PySequence_Concat), GetUnmanagedDll(_PythonDll));
            PySequence_Repeat = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, NewReference>)GetFunctionByName(nameof(PySequence_Repeat), GetUnmanagedDll(_PythonDll));
            PySequence_Index = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, nint>)GetFunctionByName(nameof(PySequence_Index), GetUnmanagedDll(_PythonDll));
            PySequence_Count = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, nint>)GetFunctionByName(nameof(PySequence_Count), GetUnmanagedDll(_PythonDll));
            PySequence_Tuple = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PySequence_Tuple), GetUnmanagedDll(_PythonDll));
            PySequence_List = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PySequence_List), GetUnmanagedDll(_PythonDll));
            PyBytes_AsString = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr>)GetFunctionByName(nameof(PyBytes_AsString), GetUnmanagedDll(_PythonDll));
            PyBytes_FromString = (delegate* unmanaged[Cdecl]<IntPtr, NewReference>)GetFunctionByName(nameof(PyBytes_FromString), GetUnmanagedDll(_PythonDll));
            PyByteArray_FromStringAndSize = (delegate* unmanaged[Cdecl]<IntPtr, nint, NewReference>)GetFunctionByName(nameof(PyByteArray_FromStringAndSize), GetUnmanagedDll(_PythonDll));
            PyBytes_Size = (delegate* unmanaged[Cdecl]<BorrowedReference, nint>)GetFunctionByName(nameof(PyBytes_Size), GetUnmanagedDll(_PythonDll));
            PyUnicode_AsUTF8 = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr>)GetFunctionByName(nameof(PyUnicode_AsUTF8), GetUnmanagedDll(_PythonDll));
            PyUnicode_DecodeUTF16 = (delegate* unmanaged[Cdecl]<IntPtr, nint, IntPtr, IntPtr, NewReference>)GetFunctionByName(nameof(PyUnicode_DecodeUTF16), GetUnmanagedDll(_PythonDll));
            PyUnicode_GetLength = (delegate* unmanaged[Cdecl]<BorrowedReference, nint>)GetFunctionByName(nameof(PyUnicode_GetLength), GetUnmanagedDll(_PythonDll));
            PyUnicode_AsUTF16String = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyUnicode_AsUTF16String), GetUnmanagedDll(_PythonDll));
            PyUnicode_ReadChar = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, int>)GetFunctionByName(nameof(PyUnicode_ReadChar), GetUnmanagedDll(_PythonDll));
            PyUnicode_FromOrdinal = (delegate* unmanaged[Cdecl]<int, NewReference>)GetFunctionByName(nameof(PyUnicode_FromOrdinal), GetUnmanagedDll(_PythonDll));
            PyUnicode_InternFromString = (delegate* unmanaged[Cdecl]<StrPtr, NewReference>)GetFunctionByName(nameof(PyUnicode_InternFromString), GetUnmanagedDll(_PythonDll));
            PyUnicode_Compare = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyUnicode_Compare), GetUnmanagedDll(_PythonDll));
            PyDict_New = (delegate* unmanaged[Cdecl]<NewReference>)GetFunctionByName(nameof(PyDict_New), GetUnmanagedDll(_PythonDll));
            PyDict_GetItem = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference>)GetFunctionByName(nameof(PyDict_GetItem), GetUnmanagedDll(_PythonDll));
            PyDict_GetItemString = (delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, BorrowedReference>)GetFunctionByName(nameof(PyDict_GetItemString), GetUnmanagedDll(_PythonDll));
            PyDict_SetItem = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyDict_SetItem), GetUnmanagedDll(_PythonDll));
            PyDict_SetItemString = (delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, BorrowedReference, int>)GetFunctionByName(nameof(PyDict_SetItemString), GetUnmanagedDll(_PythonDll));
            PyDict_DelItem = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyDict_DelItem), GetUnmanagedDll(_PythonDll));
            PyDict_DelItemString = (delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, int>)GetFunctionByName(nameof(PyDict_DelItemString), GetUnmanagedDll(_PythonDll));
            PyMapping_HasKey = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyMapping_HasKey), GetUnmanagedDll(_PythonDll));
            PyDict_Keys = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyDict_Keys), GetUnmanagedDll(_PythonDll));
            PyDict_Values = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyDict_Values), GetUnmanagedDll(_PythonDll));
            PyDict_Items = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyDict_Items), GetUnmanagedDll(_PythonDll));
            PyDict_Copy = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyDict_Copy), GetUnmanagedDll(_PythonDll));
            PyDict_Update = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyDict_Update), GetUnmanagedDll(_PythonDll));
            PyDict_Clear = (delegate* unmanaged[Cdecl]<BorrowedReference, void>)GetFunctionByName(nameof(PyDict_Clear), GetUnmanagedDll(_PythonDll));
            PyDict_Size = (delegate* unmanaged[Cdecl]<BorrowedReference, nint>)GetFunctionByName(nameof(PyDict_Size), GetUnmanagedDll(_PythonDll));
            PySet_New = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PySet_New), GetUnmanagedDll(_PythonDll));
            PySet_Add = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PySet_Add), GetUnmanagedDll(_PythonDll));
            PySet_Contains = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PySet_Contains), GetUnmanagedDll(_PythonDll));
            PyList_New = (delegate* unmanaged[Cdecl]<nint, NewReference>)GetFunctionByName(nameof(PyList_New), GetUnmanagedDll(_PythonDll));
            PyList_GetItem = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, BorrowedReference>)GetFunctionByName(nameof(PyList_GetItem), GetUnmanagedDll(_PythonDll));
            PyList_SetItem = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, StolenReference, int>)GetFunctionByName(nameof(PyList_SetItem), GetUnmanagedDll(_PythonDll));
            PyList_Insert = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, BorrowedReference, int>)GetFunctionByName(nameof(PyList_Insert), GetUnmanagedDll(_PythonDll));
            PyList_Append = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyList_Append), GetUnmanagedDll(_PythonDll));
            PyList_Reverse = (delegate* unmanaged[Cdecl]<BorrowedReference, int>)GetFunctionByName(nameof(PyList_Reverse), GetUnmanagedDll(_PythonDll));
            PyList_Sort = (delegate* unmanaged[Cdecl]<BorrowedReference, int>)GetFunctionByName(nameof(PyList_Sort), GetUnmanagedDll(_PythonDll));
            PyList_GetSlice = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, nint, NewReference>)GetFunctionByName(nameof(PyList_GetSlice), GetUnmanagedDll(_PythonDll));
            PyList_SetSlice = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, nint, BorrowedReference, int>)GetFunctionByName(nameof(PyList_SetSlice), GetUnmanagedDll(_PythonDll));
            PyList_Size = (delegate* unmanaged[Cdecl]<BorrowedReference, nint>)GetFunctionByName(nameof(PyList_Size), GetUnmanagedDll(_PythonDll));
            PyTuple_New = (delegate* unmanaged[Cdecl]<nint, NewReference>)GetFunctionByName(nameof(PyTuple_New), GetUnmanagedDll(_PythonDll));
            PyTuple_GetItem = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, BorrowedReference>)GetFunctionByName(nameof(PyTuple_GetItem), GetUnmanagedDll(_PythonDll));
            PyTuple_SetItem = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, StolenReference, int>)GetFunctionByName(nameof(PyTuple_SetItem), GetUnmanagedDll(_PythonDll));
            PyTuple_GetSlice = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, nint, NewReference>)GetFunctionByName(nameof(PyTuple_GetSlice), GetUnmanagedDll(_PythonDll));
            PyTuple_Size = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr>)GetFunctionByName(nameof(PyTuple_Size), GetUnmanagedDll(_PythonDll));
            try
            {
                PyIter_Check = (delegate* unmanaged[Cdecl]<BorrowedReference, int>)GetFunctionByName(nameof(PyIter_Check), GetUnmanagedDll(_PythonDll));
            }
            catch (MissingMethodException) { }
            PyIter_Next = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyIter_Next), GetUnmanagedDll(_PythonDll));
            PyModule_New = (delegate* unmanaged[Cdecl]<StrPtr, NewReference>)GetFunctionByName(nameof(PyModule_New), GetUnmanagedDll(_PythonDll));
            PyModule_GetDict = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference>)GetFunctionByName(nameof(PyModule_GetDict), GetUnmanagedDll(_PythonDll));
            PyModule_AddObject = (delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, IntPtr, int>)GetFunctionByName(nameof(PyModule_AddObject), GetUnmanagedDll(_PythonDll));
            PyImport_Import = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyImport_Import), GetUnmanagedDll(_PythonDll));
            PyImport_ImportModule = (delegate* unmanaged[Cdecl]<StrPtr, NewReference>)GetFunctionByName(nameof(PyImport_ImportModule), GetUnmanagedDll(_PythonDll));
            PyImport_ReloadModule = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyImport_ReloadModule), GetUnmanagedDll(_PythonDll));
            PyImport_AddModule = (delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference>)GetFunctionByName(nameof(PyImport_AddModule), GetUnmanagedDll(_PythonDll));
            PyImport_GetModuleDict = (delegate* unmanaged[Cdecl]<BorrowedReference>)GetFunctionByName(nameof(PyImport_GetModuleDict), GetUnmanagedDll(_PythonDll));
            PySys_SetArgvEx = (delegate* unmanaged[Cdecl]<int, IntPtr, int, void>)GetFunctionByName(nameof(PySys_SetArgvEx), GetUnmanagedDll(_PythonDll));
            PySys_GetObject = (delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference>)GetFunctionByName(nameof(PySys_GetObject), GetUnmanagedDll(_PythonDll));
            PySys_SetObject = (delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference, int>)GetFunctionByName(nameof(PySys_SetObject), GetUnmanagedDll(_PythonDll));
            PyType_Modified = (delegate* unmanaged[Cdecl]<BorrowedReference, void>)GetFunctionByName(nameof(PyType_Modified), GetUnmanagedDll(_PythonDll));
            PyType_IsSubtype = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, bool>)GetFunctionByName(nameof(PyType_IsSubtype), GetUnmanagedDll(_PythonDll));
            PyType_GenericNew = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyType_GenericNew), GetUnmanagedDll(_PythonDll));
            PyType_GenericAlloc = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, NewReference>)GetFunctionByName(nameof(PyType_GenericAlloc), GetUnmanagedDll(_PythonDll));
            PyType_Ready = (delegate* unmanaged[Cdecl]<BorrowedReference, int>)GetFunctionByName(nameof(PyType_Ready), GetUnmanagedDll(_PythonDll));
            _PyType_Lookup = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference>)GetFunctionByName(nameof(_PyType_Lookup), GetUnmanagedDll(_PythonDll));
            PyObject_GenericGetAttr = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyObject_GenericGetAttr), GetUnmanagedDll(_PythonDll));
            PyObject_GenericGetDict = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, NewReference>)GetFunctionByName(nameof(PyObject_GenericGetDict), GetUnmanagedDll(PythonDLL));
            PyObject_GenericSetAttr = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyObject_GenericSetAttr), GetUnmanagedDll(_PythonDll));
            PyObject_GC_Del = (delegate* unmanaged[Cdecl]<StolenReference, void>)GetFunctionByName(nameof(PyObject_GC_Del), GetUnmanagedDll(_PythonDll));
            try
            {
                PyObject_GC_IsTracked = (delegate* unmanaged[Cdecl]<BorrowedReference, int>)GetFunctionByName(nameof(PyObject_GC_IsTracked), GetUnmanagedDll(_PythonDll));
            }
            catch (MissingMethodException) { }
            PyObject_GC_Track = (delegate* unmanaged[Cdecl]<BorrowedReference, void>)GetFunctionByName(nameof(PyObject_GC_Track), GetUnmanagedDll(_PythonDll));
            PyObject_GC_UnTrack = (delegate* unmanaged[Cdecl]<BorrowedReference, void>)GetFunctionByName(nameof(PyObject_GC_UnTrack), GetUnmanagedDll(_PythonDll));
            _PyObject_Dump = (delegate* unmanaged[Cdecl]<BorrowedReference, void>)GetFunctionByName(nameof(_PyObject_Dump), GetUnmanagedDll(_PythonDll));
            PyMem_Malloc = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyMem_Malloc), GetUnmanagedDll(_PythonDll));
            PyMem_Realloc = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyMem_Realloc), GetUnmanagedDll(_PythonDll));
            PyMem_Free = (delegate* unmanaged[Cdecl]<IntPtr, void>)GetFunctionByName(nameof(PyMem_Free), GetUnmanagedDll(_PythonDll));
            PyErr_SetString = (delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, void>)GetFunctionByName(nameof(PyErr_SetString), GetUnmanagedDll(_PythonDll));
            PyErr_SetObject = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, void>)GetFunctionByName(nameof(PyErr_SetObject), GetUnmanagedDll(_PythonDll));
            PyErr_ExceptionMatches = (delegate* unmanaged[Cdecl]<BorrowedReference, int>)GetFunctionByName(nameof(PyErr_ExceptionMatches), GetUnmanagedDll(_PythonDll));
            PyErr_GivenExceptionMatches = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyErr_GivenExceptionMatches), GetUnmanagedDll(_PythonDll));
            PyErr_NormalizeException = (delegate* unmanaged[Cdecl]<ref NewReference, ref NewReference, ref NewReference, void>)GetFunctionByName(nameof(PyErr_NormalizeException), GetUnmanagedDll(_PythonDll));
            PyErr_Occurred = (delegate* unmanaged[Cdecl]<BorrowedReference>)GetFunctionByName(nameof(PyErr_Occurred), GetUnmanagedDll(_PythonDll));
            PyErr_Fetch = (delegate* unmanaged[Cdecl]<out NewReference, out NewReference, out NewReference, void>)GetFunctionByName(nameof(PyErr_Fetch), GetUnmanagedDll(_PythonDll));
            PyErr_Restore = (delegate* unmanaged[Cdecl]<StolenReference, StolenReference, StolenReference, void>)GetFunctionByName(nameof(PyErr_Restore), GetUnmanagedDll(_PythonDll));
            PyErr_Clear = (delegate* unmanaged[Cdecl]<void>)GetFunctionByName(nameof(PyErr_Clear), GetUnmanagedDll(_PythonDll));
            PyErr_Print = (delegate* unmanaged[Cdecl]<void>)GetFunctionByName(nameof(PyErr_Print), GetUnmanagedDll(_PythonDll));
            PyCell_Get = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyCell_Get), GetUnmanagedDll(_PythonDll));
            PyCell_Set = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyCell_Set), GetUnmanagedDll(_PythonDll));
            PyGC_Collect = (delegate* unmanaged[Cdecl]<nint>)GetFunctionByName(nameof(PyGC_Collect), GetUnmanagedDll(_PythonDll));
            PyCapsule_New = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, NewReference>)GetFunctionByName(nameof(PyCapsule_New), GetUnmanagedDll(_PythonDll));
            PyCapsule_GetPointer = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, IntPtr>)GetFunctionByName(nameof(PyCapsule_GetPointer), GetUnmanagedDll(_PythonDll));
            PyCapsule_SetPointer = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, int>)GetFunctionByName(nameof(PyCapsule_SetPointer), GetUnmanagedDll(_PythonDll));
            PyLong_AsUnsignedSize_t = (delegate* unmanaged[Cdecl]<BorrowedReference, nuint>)GetFunctionByName("PyLong_AsSize_t", GetUnmanagedDll(_PythonDll));
            PyLong_AsSignedSize_t = (delegate* unmanaged[Cdecl]<BorrowedReference, nint>)GetFunctionByName("PyLong_AsSsize_t", GetUnmanagedDll(_PythonDll));
            PyDict_GetItemWithError = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference>)GetFunctionByName(nameof(PyDict_GetItemWithError), GetUnmanagedDll(_PythonDll));
            PyException_GetCause = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyException_GetCause), GetUnmanagedDll(_PythonDll));
            PyException_GetTraceback = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyException_GetTraceback), GetUnmanagedDll(_PythonDll));
            PyException_SetCause = (delegate* unmanaged[Cdecl]<BorrowedReference, StolenReference, void>)GetFunctionByName(nameof(PyException_SetCause), GetUnmanagedDll(_PythonDll));
            PyException_SetTraceback = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyException_SetTraceback), GetUnmanagedDll(_PythonDll));
            PyThreadState_SetAsyncExcLLP64 = (delegate* unmanaged[Cdecl]<uint, BorrowedReference, int>)GetFunctionByName("PyThreadState_SetAsyncExc", GetUnmanagedDll(_PythonDll));
            PyThreadState_SetAsyncExcLP64 = (delegate* unmanaged[Cdecl]<ulong, BorrowedReference, int>)GetFunctionByName("PyThreadState_SetAsyncExc", GetUnmanagedDll(_PythonDll));
            PyType_GetSlot = (delegate* unmanaged[Cdecl]<BorrowedReference, TypeSlotID, IntPtr>)GetFunctionByName(nameof(PyType_GetSlot), GetUnmanagedDll(_PythonDll));
            PyType_FromSpecWithBases = (delegate* unmanaged[Cdecl]<in NativeTypeSpec, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyType_FromSpecWithBases), GetUnmanagedDll(PythonDLL));

            try
            {
                _Py_NewReference = (delegate* unmanaged[Cdecl]<BorrowedReference, void>)GetFunctionByName(nameof(_Py_NewReference), GetUnmanagedDll(_PythonDll));
            }
            catch (MissingMethodException) { }
            try
            {
                _Py_IsFinalizing = (delegate* unmanaged[Cdecl]<int>)GetFunctionByName(nameof(_Py_IsFinalizing), GetUnmanagedDll(_PythonDll));
            }
            catch (MissingMethodException) { }

            PyType_Type = GetFunctionByName(nameof(PyType_Type), GetUnmanagedDll(_PythonDll));
            Py_NoSiteFlag = (int*)GetFunctionByName(nameof(Py_NoSiteFlag), GetUnmanagedDll(_PythonDll));
        }

        static global::System.IntPtr GetUnmanagedDll(string? libraryName)
        {
            if (libraryName is null) return IntPtr.Zero;
            return libraryLoader.Load(libraryName);
        }

        static global::System.IntPtr GetFunctionByName(string functionName, global::System.IntPtr libraryHandle)
        {
            try
            {
                return libraryLoader.GetFunction(libraryHandle, functionName);
            }
            catch (MissingMethodException e) when (libraryHandle == IntPtr.Zero)
            {
                throw new BadPythonDllException(
                    "Runtime.PythonDLL was not set or does not point to a supported Python runtime DLL." +
                    " See https://github.com/pythonnet/pythonnet#embedding-python-in-net",
                    e);
            }
        }

        internal static delegate* unmanaged[Cdecl]<BorrowedReference, void> Py_IncRef { get; }
        internal static delegate* unmanaged[Cdecl]<StolenReference, void> Py_DecRef { get; }
        internal static delegate* unmanaged[Cdecl]<void> Py_Initialize { get; }
        internal static delegate* unmanaged[Cdecl]<int, void> Py_InitializeEx { get; }
        internal static delegate* unmanaged[Cdecl]<int> Py_IsInitialized { get; }
        internal static delegate* unmanaged[Cdecl]<void> Py_Finalize { get; }
        internal static delegate* unmanaged[Cdecl]<PyThreadState*> Py_NewInterpreter { get; }
        internal static delegate* unmanaged[Cdecl]<PyThreadState*, void> Py_EndInterpreter { get; }
        internal static delegate* unmanaged[Cdecl]<PyInterpreterState*, PyThreadState*> PyThreadState_New { get; }
        internal static delegate* unmanaged[Cdecl]<PyThreadState*> PyThreadState_Get { get; }
        internal static delegate* unmanaged[Cdecl]<PyThreadState*> PyThreadState_GetUnchecked { get; }
        internal static delegate* unmanaged[Cdecl]<int> PyGILState_Check { get; }
        internal static delegate* unmanaged[Cdecl]<PyGILState> PyGILState_Ensure { get; }
        internal static delegate* unmanaged[Cdecl]<PyGILState, void> PyGILState_Release { get; }
        internal static delegate* unmanaged[Cdecl]<PyThreadState*> PyGILState_GetThisThreadState { get; }
        internal static delegate* unmanaged[Cdecl]<void> PyEval_InitThreads { get; }
        internal static delegate* unmanaged[Cdecl]<int> PyEval_ThreadsInitialized { get; }
        internal static delegate* unmanaged[Cdecl]<void> PyEval_AcquireLock { get; }
        internal static delegate* unmanaged[Cdecl]<void> PyEval_ReleaseLock { get; }
        internal static delegate* unmanaged[Cdecl]<PyThreadState*, void> PyEval_AcquireThread { get; }
        internal static delegate* unmanaged[Cdecl]<PyThreadState*, void> PyEval_ReleaseThread { get; }
        internal static delegate* unmanaged[Cdecl]<PyThreadState*> PyEval_SaveThread { get; }
        internal static delegate* unmanaged[Cdecl]<PyThreadState*, void> PyEval_RestoreThread { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference> PyEval_GetBuiltins { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference> PyEval_GetGlobals { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference> PyEval_GetLocals { get; }
        internal static delegate* unmanaged[Cdecl]<IntPtr> Py_GetProgramName { get; }
        internal static delegate* unmanaged[Cdecl]<IntPtr, void> Py_SetProgramName { get; }
        internal static delegate* unmanaged[Cdecl]<IntPtr> Py_GetPythonHome { get; }
        internal static delegate* unmanaged[Cdecl]<IntPtr, void> Py_SetPythonHome { get; }
        internal static delegate* unmanaged[Cdecl]<IntPtr> Py_GetPath { get; }
        internal static delegate* unmanaged[Cdecl]<IntPtr, void> Py_SetPath { get; }
        internal static delegate* unmanaged[Cdecl]<IntPtr> Py_GetVersion { get; }
        internal static delegate* unmanaged[Cdecl]<IntPtr> Py_GetPlatform { get; }
        internal static delegate* unmanaged[Cdecl]<IntPtr> Py_GetCopyright { get; }
        internal static delegate* unmanaged[Cdecl]<IntPtr> Py_GetCompiler { get; }
        internal static delegate* unmanaged[Cdecl]<IntPtr> Py_GetBuildInfo { get; }
        internal static delegate* unmanaged[Cdecl]<StrPtr, in PyCompilerFlags, int> PyRun_SimpleStringFlags { get; }
        internal static delegate* unmanaged[Cdecl]<StrPtr, RunFlagType, BorrowedReference, BorrowedReference, in PyCompilerFlags, NewReference> PyRun_StringFlags { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, NewReference> PyEval_EvalCode { get; }
        internal static delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference, int, in PyCompilerFlags, int, NewReference> Py_CompileStringObject { get; }
        internal static delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference, NewReference> PyImport_ExecCodeModule { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, int> PyObject_HasAttrString { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, NewReference> PyObject_GetAttrString { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, BorrowedReference, int> PyObject_SetAttrString { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyObject_HasAttr { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyObject_GetAttr { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, int> PyObject_SetAttr { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyObject_GetItem { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, int> PyObject_SetItem { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyObject_DelItem { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyObject_GetIter { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, NewReference> PyObject_Call { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyObject_CallObject { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int, int> PyObject_RichCompareBool { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyObject_IsInstance { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyObject_IsSubclass { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, void> PyObject_ClearWeakRefs { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, int> PyCallable_Check { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, int> PyObject_IsTrue { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, int> PyObject_Not { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint> PyObject_Size { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint> PyObject_Hash { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyObject_Repr { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyObject_Str { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyObject_Type { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyObject_Dir { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, out Py_buffer, int, int> PyObject_GetBuffer { get; }
        internal static delegate* unmanaged[Cdecl]<ref Py_buffer, void> PyBuffer_Release { get; }
        internal static delegate* unmanaged[Cdecl]<StrPtr, nint> PyBuffer_SizeFromFormat { get; }
        internal static delegate* unmanaged[Cdecl]<ref Py_buffer, char, int> PyBuffer_IsContiguous { get; }
        internal static delegate* unmanaged[Cdecl]<ref Py_buffer, nint[], IntPtr> PyBuffer_GetPointer { get; }
        internal static delegate* unmanaged[Cdecl]<ref Py_buffer, IntPtr, IntPtr, char, int> PyBuffer_FromContiguous { get; }
        internal static delegate* unmanaged[Cdecl]<IntPtr, ref Py_buffer, IntPtr, char, int> PyBuffer_ToContiguous { get; }
        internal static delegate* unmanaged[Cdecl]<int, IntPtr, IntPtr, int, char, void> PyBuffer_FillContiguousStrides { get; }
        internal static delegate* unmanaged[Cdecl]<ref Py_buffer, BorrowedReference, IntPtr, IntPtr, int, int, int> PyBuffer_FillInfo { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyNumber_Long { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyNumber_Float { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, bool> PyNumber_Check { get; }
        internal static delegate* unmanaged[Cdecl]<long, NewReference> PyLong_FromLongLong { get; }
        internal static delegate* unmanaged[Cdecl]<ulong, NewReference> PyLong_FromUnsignedLongLong { get; }
        internal static delegate* unmanaged[Cdecl]<StrPtr, IntPtr, int, NewReference> PyLong_FromString { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, long> PyLong_AsLongLong { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, ulong> PyLong_AsUnsignedLongLong { get; }
        internal static delegate* unmanaged[Cdecl]<IntPtr, NewReference> PyLong_FromVoidPtr { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr> PyLong_AsVoidPtr { get; }
        internal static delegate* unmanaged[Cdecl]<double, NewReference> PyFloat_FromDouble { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyFloat_FromString { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, double> PyFloat_AsDouble { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_Add { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_Subtract { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_Multiply { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_TrueDivide { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_And { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_Xor { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_Or { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_Lshift { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_Rshift { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_Power { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_Remainder { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_InPlaceAdd { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_InPlaceSubtract { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_InPlaceMultiply { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_InPlaceTrueDivide { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_InPlaceAnd { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_InPlaceXor { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_InPlaceOr { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_InPlaceLshift { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_InPlaceRshift { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_InPlacePower { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_InPlaceRemainder { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyNumber_Negative { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyNumber_Positive { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyNumber_Invert { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, bool> PySequence_Check { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, NewReference> PySequence_GetItem { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, BorrowedReference, int> PySequence_SetItem { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, int> PySequence_DelItem { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, nint, NewReference> PySequence_GetSlice { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, nint, BorrowedReference, int> PySequence_SetSlice { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, nint, int> PySequence_DelSlice { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint> PySequence_Size { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PySequence_Contains { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PySequence_Concat { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, NewReference> PySequence_Repeat { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, nint> PySequence_Index { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, nint> PySequence_Count { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PySequence_Tuple { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PySequence_List { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr> PyBytes_AsString { get; }
        internal static delegate* unmanaged[Cdecl]<IntPtr, NewReference> PyBytes_FromString { get; }
        internal static delegate* unmanaged[Cdecl]<IntPtr, nint, NewReference> PyByteArray_FromStringAndSize { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint> PyBytes_Size { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr> PyUnicode_AsUTF8 { get; }
        internal static delegate* unmanaged[Cdecl]<IntPtr, nint, IntPtr, IntPtr, NewReference> PyUnicode_DecodeUTF16 { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint> PyUnicode_GetLength { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, int> PyUnicode_ReadChar { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyUnicode_AsUTF16String { get; }
        internal static delegate* unmanaged[Cdecl]<int, NewReference> PyUnicode_FromOrdinal { get; }
        internal static delegate* unmanaged[Cdecl]<StrPtr, NewReference> PyUnicode_InternFromString { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyUnicode_Compare { get; }
        internal static delegate* unmanaged[Cdecl]<NewReference> PyDict_New { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference> PyDict_GetItem { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, BorrowedReference> PyDict_GetItemString { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, int> PyDict_SetItem { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, BorrowedReference, int> PyDict_SetItemString { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyDict_DelItem { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, int> PyDict_DelItemString { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyMapping_HasKey { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyDict_Keys { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyDict_Values { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyDict_Items { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyDict_Copy { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyDict_Update { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, void> PyDict_Clear { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint> PyDict_Size { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PySet_New { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PySet_Add { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PySet_Contains { get; }
        internal static delegate* unmanaged[Cdecl]<nint, NewReference> PyList_New { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, BorrowedReference> PyList_GetItem { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, StolenReference, int> PyList_SetItem { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, BorrowedReference, int> PyList_Insert { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyList_Append { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, int> PyList_Reverse { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, int> PyList_Sort { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, nint, NewReference> PyList_GetSlice { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, nint, BorrowedReference, int> PyList_SetSlice { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint> PyList_Size { get; }
        internal static delegate* unmanaged[Cdecl]<nint, NewReference> PyTuple_New { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, BorrowedReference> PyTuple_GetItem { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, StolenReference, int> PyTuple_SetItem { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, nint, NewReference> PyTuple_GetSlice { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint> PyTuple_Size { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, int> PyIter_Check { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyIter_Next { get; }
        internal static delegate* unmanaged[Cdecl]<StrPtr, NewReference> PyModule_New { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference> PyModule_GetDict { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, IntPtr, int> PyModule_AddObject { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyImport_Import { get; }
        internal static delegate* unmanaged[Cdecl]<StrPtr, NewReference> PyImport_ImportModule { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyImport_ReloadModule { get; }
        internal static delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference> PyImport_AddModule { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference> PyImport_GetModuleDict { get; }
        internal static delegate* unmanaged[Cdecl]<int, IntPtr, int, void> PySys_SetArgvEx { get; }
        internal static delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference> PySys_GetObject { get; }
        internal static delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference, int> PySys_SetObject { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, void> PyType_Modified { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, bool> PyType_IsSubtype { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, NewReference> PyType_GenericNew { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, NewReference> PyType_GenericAlloc { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, int> PyType_Ready { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference> _PyType_Lookup { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyObject_GenericGetAttr { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, int> PyObject_GenericSetAttr { get; }
        internal static delegate* unmanaged[Cdecl]<StolenReference, void> PyObject_GC_Del { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, int> PyObject_GC_IsTracked { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, void> PyObject_GC_Track { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, void> PyObject_GC_UnTrack { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, void> _PyObject_Dump { get; }
        internal static delegate* unmanaged[Cdecl]<nint, IntPtr> PyMem_Malloc { get; }
        internal static delegate* unmanaged[Cdecl]<IntPtr, nint, IntPtr> PyMem_Realloc { get; }
        internal static delegate* unmanaged[Cdecl]<IntPtr, void> PyMem_Free { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, void> PyErr_SetString { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, void> PyErr_SetObject { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, int> PyErr_ExceptionMatches { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyErr_GivenExceptionMatches { get; }
        internal static delegate* unmanaged[Cdecl]<ref NewReference, ref NewReference, ref NewReference, void> PyErr_NormalizeException { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference> PyErr_Occurred { get; }
        internal static delegate* unmanaged[Cdecl]<out NewReference, out NewReference, out NewReference, void> PyErr_Fetch { get; }
        internal static delegate* unmanaged[Cdecl]<StolenReference, StolenReference, StolenReference, void> PyErr_Restore { get; }
        internal static delegate* unmanaged[Cdecl]<void> PyErr_Clear { get; }
        internal static delegate* unmanaged[Cdecl]<void> PyErr_Print { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyCell_Get { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyCell_Set { get; }
        internal static delegate* unmanaged[Cdecl]<nint> PyGC_Collect { get; }
        internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, NewReference> PyCapsule_New { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, IntPtr> PyCapsule_GetPointer { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, int> PyCapsule_SetPointer { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nuint> PyLong_AsUnsignedSize_t { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint> PyLong_AsSignedSize_t { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference> PyDict_GetItemWithError { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyException_GetCause { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyException_GetTraceback { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, StolenReference, void> PyException_SetCause { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyException_SetTraceback { get; }
        internal static delegate* unmanaged[Cdecl]<uint, BorrowedReference, int> PyThreadState_SetAsyncExcLLP64 { get; }
        internal static delegate* unmanaged[Cdecl]<ulong, BorrowedReference, int> PyThreadState_SetAsyncExcLP64 { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, NewReference> PyObject_GenericGetDict { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, TypeSlotID, IntPtr> PyType_GetSlot { get; }
        internal static delegate* unmanaged[Cdecl]<in NativeTypeSpec, BorrowedReference, NewReference> PyType_FromSpecWithBases { get; }
        internal static delegate* unmanaged[Cdecl]<BorrowedReference, void> _Py_NewReference { get; }
        internal static delegate* unmanaged[Cdecl]<int> _Py_IsFinalizing { get; }
        internal static IntPtr PyType_Type { get; }
        internal static int* Py_NoSiteFlag { get; }
    }
}
