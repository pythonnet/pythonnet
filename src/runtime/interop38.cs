
// Auto-generated by geninterop.py.
// DO NOT MODIFY BY HAND.

// Python 3.8: ABI flags: ''

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

using Python.Runtime.Native;

namespace Python.Runtime
{
    [SuppressMessage("Style", "IDE1006:Naming Styles",
                     Justification = "Following CPython",
                     Scope = "type")]

    [StructLayout(LayoutKind.Sequential)]
    internal class TypeOffset38 : GeneratedTypeOffsets, ITypeOffsets
    {
        public TypeOffset38() { }
        // Auto-generated from PyHeapTypeObject in Python.h
        public int ob_refcnt { get; private set; }
        public int ob_type { get; private set; }
        public int ob_size { get; private set; }
        public int tp_name { get; private set; }
        public int tp_basicsize { get; private set; }
        public int tp_itemsize { get; private set; }
        public int tp_dealloc { get; private set; }
        public int tp_vectorcall_offset { get; private set; }
        public int tp_getattr { get; private set; }
        public int tp_setattr { get; private set; }
        public int tp_as_async { get; private set; }
        public int tp_repr { get; private set; }
        public int tp_as_number { get; private set; }
        public int tp_as_sequence { get; private set; }
        public int tp_as_mapping { get; private set; }
        public int tp_hash { get; private set; }
        public int tp_call { get; private set; }
        public int tp_str { get; private set; }
        public int tp_getattro { get; private set; }
        public int tp_setattro { get; private set; }
        public int tp_as_buffer { get; private set; }
        public int tp_flags { get; private set; }
        public int tp_doc { get; private set; }
        public int tp_traverse { get; private set; }
        public int tp_clear { get; private set; }
        public int tp_richcompare { get; private set; }
        public int tp_weaklistoffset { get; private set; }
        public int tp_iter { get; private set; }
        public int tp_iternext { get; private set; }
        public int tp_methods { get; private set; }
        public int tp_members { get; private set; }
        public int tp_getset { get; private set; }
        public int tp_base { get; private set; }
        public int tp_dict { get; private set; }
        public int tp_descr_get { get; private set; }
        public int tp_descr_set { get; private set; }
        public int tp_dictoffset { get; private set; }
        public int tp_init { get; private set; }
        public int tp_alloc { get; private set; }
        public int tp_new { get; private set; }
        public int tp_free { get; private set; }
        public int tp_is_gc { get; private set; }
        public int tp_bases { get; private set; }
        public int tp_mro { get; private set; }
        public int tp_cache { get; private set; }
        public int tp_subclasses { get; private set; }
        public int tp_weaklist { get; private set; }
        public int tp_del { get; private set; }
        public int tp_version_tag { get; private set; }
        public int tp_finalize { get; private set; }
        public int tp_vectorcall { get; private set; }
        public int tp_print { get; private set; }
        public int am_await { get; private set; }
        public int am_aiter { get; private set; }
        public int am_anext { get; private set; }
        public int nb_add { get; private set; }
        public int nb_subtract { get; private set; }
        public int nb_multiply { get; private set; }
        public int nb_remainder { get; private set; }
        public int nb_divmod { get; private set; }
        public int nb_power { get; private set; }
        public int nb_negative { get; private set; }
        public int nb_positive { get; private set; }
        public int nb_absolute { get; private set; }
        public int nb_bool { get; private set; }
        public int nb_invert { get; private set; }
        public int nb_lshift { get; private set; }
        public int nb_rshift { get; private set; }
        public int nb_and { get; private set; }
        public int nb_xor { get; private set; }
        public int nb_or { get; private set; }
        public int nb_int { get; private set; }
        public int nb_reserved { get; private set; }
        public int nb_float { get; private set; }
        public int nb_inplace_add { get; private set; }
        public int nb_inplace_subtract { get; private set; }
        public int nb_inplace_multiply { get; private set; }
        public int nb_inplace_remainder { get; private set; }
        public int nb_inplace_power { get; private set; }
        public int nb_inplace_lshift { get; private set; }
        public int nb_inplace_rshift { get; private set; }
        public int nb_inplace_and { get; private set; }
        public int nb_inplace_xor { get; private set; }
        public int nb_inplace_or { get; private set; }
        public int nb_floor_divide { get; private set; }
        public int nb_true_divide { get; private set; }
        public int nb_inplace_floor_divide { get; private set; }
        public int nb_inplace_true_divide { get; private set; }
        public int nb_index { get; private set; }
        public int nb_matrix_multiply { get; private set; }
        public int nb_inplace_matrix_multiply { get; private set; }
        public int mp_length { get; private set; }
        public int mp_subscript { get; private set; }
        public int mp_ass_subscript { get; private set; }
        public int sq_length { get; private set; }
        public int sq_concat { get; private set; }
        public int sq_repeat { get; private set; }
        public int sq_item { get; private set; }
        public int was_sq_slice { get; private set; }
        public int sq_ass_item { get; private set; }
        public int was_sq_ass_slice { get; private set; }
        public int sq_contains { get; private set; }
        public int sq_inplace_concat { get; private set; }
        public int sq_inplace_repeat { get; private set; }
        public int bf_getbuffer { get; private set; }
        public int bf_releasebuffer { get; private set; }
        public int name { get; private set; }
        public int ht_slots { get; private set; }
        public int qualname { get; private set; }
        public int ht_cached_keys { get; private set; }
    }

#if PYTHON38
    [StructLayout(LayoutKind.Sequential)]
    internal struct PyNumberMethods
    {
        public IntPtr nb_add;
        public IntPtr nb_subtract;
        public IntPtr nb_multiply;
        public IntPtr nb_remainder;
        public IntPtr nb_divmod;
        public IntPtr nb_power;
        public IntPtr nb_negative;
        public IntPtr nb_positive;
        public IntPtr nb_absolute;
        public IntPtr nb_bool;
        public IntPtr nb_invert;
        public IntPtr nb_lshift;
        public IntPtr nb_rshift;
        public IntPtr nb_and;
        public IntPtr nb_xor;
        public IntPtr nb_or;
        public IntPtr nb_int;
        public IntPtr nb_reserved;
        public IntPtr nb_float;
        public IntPtr nb_inplace_add;
        public IntPtr nb_inplace_subtract;
        public IntPtr nb_inplace_multiply;
        public IntPtr nb_inplace_remainder;
        public IntPtr nb_inplace_power;
        public IntPtr nb_inplace_lshift;
        public IntPtr nb_inplace_rshift;
        public IntPtr nb_inplace_and;
        public IntPtr nb_inplace_xor;
        public IntPtr nb_inplace_or;
        public IntPtr nb_floor_divide;
        public IntPtr nb_true_divide;
        public IntPtr nb_inplace_floor_divide;
        public IntPtr nb_inplace_true_divide;
        public IntPtr nb_index;
        public IntPtr nb_matrix_multiply;
        public IntPtr nb_inplace_matrix_multiply;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PySequenceMethods
    {
        public IntPtr sq_length;
        public IntPtr sq_concat;
        public IntPtr sq_repeat;
        public IntPtr sq_item;
        public IntPtr was_sq_slice;
        public IntPtr sq_ass_item;
        public IntPtr was_sq_ass_slice;
        public IntPtr sq_contains;
        public IntPtr sq_inplace_concat;
        public IntPtr sq_inplace_repeat;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PyMappingMethods
    {
        public IntPtr mp_length;
        public IntPtr mp_subscript;
        public IntPtr mp_ass_subscript;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PyAsyncMethods
    {
        public IntPtr am_await;
        public IntPtr am_aiter;
        public IntPtr am_anext;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PyBufferProcs
    {
        public IntPtr bf_getbuffer;
        public IntPtr bf_releasebuffer;
    }

    internal static partial class SlotTypes
    {
        public static readonly Type[] Types = {
            typeof(PyNumberMethods),
            typeof(PySequenceMethods),
            typeof(PyMappingMethods),
            typeof(PyAsyncMethods),
            typeof(PyBufferProcs),
        };
    }

#endif
}
