using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Python.Runtime
{
    public class TypeSpec
    {
        public TypeSpec(string name, int basicSize, IEnumerable<Slot> slots, TypeFlags flags, int itemSize = 0)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.BasicSize = basicSize;
            this.Slots = slots.ToArray();
            this.Flags = flags;
            this.ItemSize = itemSize;
        }
        public string Name { get; }
        public int BasicSize { get; }
        public int ItemSize { get; }
        public TypeFlags Flags { get; }
        public IReadOnlyList<Slot> Slots { get; }

        [StructLayout(LayoutKind.Sequential)]
        public struct Slot
        {
            public Slot(TypeSlotID id, IntPtr value)
            {
                ID = id;
                Value = value;
            }

            public TypeSlotID ID { get; }
            public IntPtr Value { get; }
        }
    }

    public enum TypeSlotID : int
    {
        mp_ass_subscript = 3,
        mp_length = 4,
        mp_subscript = 5,
        nb_absolute = 6,
        nb_add = 7,
        nb_and = 8,
        nb_bool = 9,
        nb_divmod = 10,
        nb_float = 11,
        nb_floor_divide = 12,
        nb_index = 13,
        nb_inplace_add = 14,
        nb_inplace_and = 15,
        nb_inplace_floor_divide = 16,
        nb_inplace_lshift = 17,
        nb_inplace_multiply = 18,
        nb_inplace_or = 19,
        nb_inplace_power = 20,
        nb_inplace_remainder = 21,
        nb_inplace_rshift = 22,
        nb_inplace_subtract = 23,
        nb_inplace_true_divide = 24,
        nb_inplace_xor = 25,
        nb_int = 26,
        nb_invert = 27,
        nb_lshift = 28,
        nb_multiply = 29,
        nb_negative = 30,
        nb_or = 31,
        nb_positive = 32,
        nb_power = 33,
        nb_remainder = 34,
        nb_rshift = 35,
        nb_subtract = 36,
        nb_true_divide = 37,
        nb_xor = 38,
        sq_ass_item = 39,
        sq_concat = 40,
        sq_contains = 41,
        sq_inplace_concat = 42,
        sq_inplace_repeat = 43,
        sq_item = 44,
        sq_length = 45,
        sq_repeat = 46,
        tp_alloc = 47,
        tp_base = 48,
        tp_bases = 49,
        tp_call = 50,
        tp_clear = 51,
        tp_dealloc = 52,
        tp_del = 53,
        tp_descr_get = 54,
        tp_descr_set = 55,
        tp_doc = 56,
        tp_getattr = 57,
        tp_getattro = 58,
        tp_hash = 59,
        tp_init = 60,
        tp_is_gc = 61,
        tp_iter = 62,
        tp_iternext = 63,
        tp_methods = 64,
        tp_new = 65,
        tp_repr = 66,
        tp_richcompare = 67,
        tp_setattr = 68,
        tp_setattro = 69,
        tp_str = 70,
        tp_traverse = 71,
        tp_members = 72,
        tp_getset = 73,
        tp_free = 74,
        nb_matrix_multiply = 75,
        nb_inplace_matrix_multiply = 76,
        am_await = 77,
        am_aiter = 78,
        am_anext = 79,
        /// <remarks>New in 3.5</remarks>
        tp_finalize = 80,
    }
}
