// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
namespace Python.Runtime.Native
{
    using System.Diagnostics.CodeAnalysis;

    [SuppressMessage("Style", "IDE1006:Naming Styles",
                     Justification = "Following CPython",
                     Scope = "type")]
    interface ITypeOffsets
    {
        int bf_getbuffer { get; }
        int mp_ass_subscript { get; }
        int mp_length { get; }
        int mp_subscript { get; }
        int name { get; }
        int nb_positive { get; }
        int nb_negative { get; }
        int nb_add { get; }
        int nb_subtract { get; }
        int nb_multiply { get; }
        int nb_true_divide { get; }
        int nb_and { get; }
        int nb_int { get; }
        int nb_or { get; }
        int nb_xor { get; }
        int nb_lshift { get; }
        int nb_rshift { get; }
        int nb_remainder { get; }
        int nb_invert { get; }
        int nb_inplace_add { get; }
        int nb_inplace_subtract { get; }
        int ob_size { get; }
        int ob_type { get; }
        int qualname { get; }
        int sq_contains { get; }
        int sq_length { get; }
        int tp_alloc { get; }
        int tp_as_buffer { get; }
        int tp_as_mapping { get; }
        int tp_as_number { get; }
        int tp_as_sequence { get; }
        int tp_base { get; }
        int tp_bases { get; }
        int tp_basicsize { get; }
        int tp_call { get; }
        int tp_clear { get; }
        int tp_dealloc { get; }
        int tp_descr_get { get; }
        int tp_descr_set { get; }
        int tp_dict { get; }
        int tp_dictoffset { get; }
        int tp_flags { get; }
        int tp_free { get; }
        int tp_getattro { get; }
        int tp_hash { get; }
        int tp_is_gc { get; }
        int tp_itemsize { get; }
        int tp_iter { get; }
        int tp_iternext { get; }
        int tp_methods { get; }
        int tp_mro { get; }
        int tp_name { get; }
        int tp_new { get; }
        int tp_repr { get; }
        int tp_richcompare { get; }
        int tp_weaklistoffset { get; }
        int tp_setattro { get; }
        int tp_str { get; }
        int tp_traverse { get; }
    }
}
