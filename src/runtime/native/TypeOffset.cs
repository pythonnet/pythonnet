// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
namespace Python.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Reflection;

    using Python.Runtime.Native;

    [SuppressMessage("Style", "IDE1006:Naming Styles",
                     Justification = "Following CPython",
                     Scope = "type")]
    static partial class TypeOffset
    {
        internal static int bf_getbuffer { get; private set; }
        internal static int mp_ass_subscript { get; private set; }
        internal static int mp_length { get; private set; }
        internal static int mp_subscript { get; private set; }
        internal static int name { get; private set; }
        internal static int nb_add { get; private set; }
        internal static int ob_size { get; private set; }
        internal static int ob_type { get; private set; }
        internal static int qualname { get; private set; }
        internal static int sq_contains { get; private set; }
        internal static int sq_length { get; private set; }
        internal static int tp_alloc { get; private set; }
        internal static int tp_as_buffer { get; private set; }
        internal static int tp_as_mapping { get; private set; }
        internal static int tp_as_number { get; private set; }
        internal static int tp_as_sequence { get; private set; }
        internal static int tp_base { get; private set; }
        internal static int tp_bases { get; private set; }
        internal static int tp_basicsize { get; private set; }
        internal static int tp_call { get; private set; }
        internal static int tp_clear { get; private set; }
        internal static int tp_dealloc { get; private set; }
        internal static int tp_descr_get { get; private set; }
        internal static int tp_descr_set { get; private set; }
        internal static int tp_dict { get; private set; }
        internal static int tp_dictoffset { get; private set; }
        internal static int tp_flags { get; private set; }
        internal static int tp_free { get; private set; }
        internal static int tp_getattro { get; private set; }
        internal static int tp_hash { get; private set; }
        internal static int tp_is_gc { get; private set; }
        internal static int tp_itemsize { get; private set; }
        internal static int tp_iter { get; private set; }
        internal static int tp_iternext { get; private set; }
        internal static int tp_methods { get; private set; }
        internal static int tp_mro { get; private set; }
        internal static int tp_name { get; private set; }
        internal static int tp_new { get; private set; }
        internal static int tp_repr { get; private set; }
        internal static int tp_richcompare { get; private set; }
        internal static int tp_setattro { get; private set; }
        internal static int tp_str { get; private set; }
        internal static int tp_traverse { get; private set; }

        internal static void Use(ITypeOffsets offsets)
        {
            if (offsets is null) throw new ArgumentNullException(nameof(offsets));

            var offsetProperties = typeof(TypeOffset).GetProperties(FieldFlags);
            foreach (var offsetProperty in offsetProperties)
            {
                var sourceProperty = typeof(ITypeOffsets).GetProperty(offsetProperty.Name);
                int value = (int)sourceProperty.GetValue(offsets, null);
                offsetProperty.SetValue(obj: null, value: value, index: null);
            }

            ValidateUnusedTypeOffsetProperties(offsetProperties);
        }

        static readonly BindingFlags FieldFlags = BindingFlags.NonPublic | BindingFlags.Static;
        internal static Dictionary<string, int> GetOffsets()
        {
            var properties = typeof(TypeOffset).GetProperties(FieldFlags);
            return properties.ToDictionary(
                    keySelector: p => p.Name,
                    elementSelector: p => (int)p.GetValue(obj: null, index: null));
        }

        internal static int GetOffsetUncached(string name)
        {
            var property = typeof(TypeOffset).GetProperty(name, FieldFlags);
            return (int)property.GetValue(obj: null, index: null);
        }

        [Conditional("DEBUG")]
        static void ValidateUnusedTypeOffsetProperties(PropertyInfo[] offsetProperties)
        {
            var extras = new List<string>();
            foreach (var property in typeof(ITypeOffsets).GetProperties(FieldFlags))
            {
                if (!offsetProperties.Any(prop => prop.Name == property.Name))
                    extras.Add(property.Name);
            }
            extras.Sort();
            Debug.Assert(extras.Count == 0, message: string.Join(", ", extras));
        }
    }
}
