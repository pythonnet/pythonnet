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
        internal static int nb_positive { get; private set; }
        internal static int nb_negative { get; private set; }
        internal static int nb_add { get; private set; }
        internal static int nb_subtract { get; private set; }
        internal static int nb_multiply { get; private set; }
        internal static int nb_true_divide { get; private set; }
        internal static int nb_and { get; private set; }
        internal static int nb_or { get; private set; }
        internal static int nb_xor { get; private set; }
        internal static int nb_int { get; private set; }
        internal static int nb_lshift { get; private set; }
        internal static int nb_rshift { get; private set; }
        internal static int nb_remainder { get; private set; }
        internal static int nb_invert { get; private set; }
        internal static int nb_inplace_add { get; private set; }
        internal static int nb_inplace_subtract { get; private set; }
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
        internal static int tp_weaklistoffset { get; private set; }
        internal static int tp_setattro { get; private set; }
        internal static int tp_str { get; private set; }
        internal static int tp_traverse { get; private set; }

        internal static void Use(ITypeOffsets offsets, int extraHeadOffset)
        {
            if (offsets is null) throw new ArgumentNullException(nameof(offsets));

            slotNames.Clear();
            var offsetProperties = typeof(TypeOffset).GetProperties(FieldFlags);
            foreach (var offsetProperty in offsetProperties)
            {
                slotNames.Add(offsetProperty.Name);

                var sourceProperty = typeof(ITypeOffsets).GetProperty(offsetProperty.Name);
                int value = (int)sourceProperty.GetValue(offsets, null);
                value += extraHeadOffset;
                offsetProperty.SetValue(obj: null, value: value, index: null);
            }

            ValidateUnusedTypeOffsetProperties(offsetProperties);
            ValidateRequiredOffsetsPresent(offsetProperties);

            SlotOffsets = GetOffsets();
        }

        static readonly BindingFlags FieldFlags = BindingFlags.NonPublic | BindingFlags.Static;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Initialized in ABI.cs
        static Dictionary<string, int> SlotOffsets;
#pragma warning restore CS8618
        internal static Dictionary<string, int> GetOffsets()
        {
            var properties = typeof(TypeOffset).GetProperties(FieldFlags);
            var result = properties.ToDictionary(
                            keySelector: p => p.Name,
                            elementSelector: p => (int)p.GetValue(obj: null, index: null));
            Debug.Assert(result.Values.Any(v => v != 0));
            return result;
        }

        public static int GetSlotOffset(string slotName)
        {
            return SlotOffsets[slotName];
        }

        public static string? GetSlotName(int offset)
            => SlotOffsets.FirstOrDefault(kv => kv.Value == offset).Key;

        static readonly HashSet<string> slotNames = new HashSet<string>();
        internal static bool IsSupportedSlotName(string name) => slotNames.Contains(name);

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

        [Conditional("DEBUG")]
        static void ValidateRequiredOffsetsPresent(PropertyInfo[] offsetProperties)
        {
            var present = new HashSet<string>(offsetProperties.Select(p => p.Name));
            var missing = new HashSet<string>();

            var thisAssembly = Assembly.GetExecutingAssembly();
            var managedTypes = thisAssembly.GetTypes()
                .Where(typeof(ManagedType).IsAssignableFrom)
                .ToList();
            foreach (var managedType in managedTypes)
            {
                var slots = managedType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                foreach(var slot in slots)
                    if (!present.Contains(slot.Name))
                        missing.Add(slot.Name);
            }
            foreach (string notSlot in new[]
            {
                "__instancecheck__",
                "__subclasscheck__",
                "AddReference",
                "FindAssembly",
                "get_SuppressDocs",
                "get_SuppressOverloads",
                "GetClrType",
                "getPreload",
                "Initialize",
                "InitializeSlots",
                "ListAssemblies",
                nameof(CLRModule._load_clr_module),
                nameof(CLRModule._add_pending_namespaces),
                "Release",
                "Reset",
                "set_SuppressDocs",
                "set_SuppressOverloads",
                "setPreload",
            })
                missing.Remove(notSlot);

            Debug.Assert(missing.Count == 0,
                         "Missing slots: " + string.Join(", ", missing));
        }
    }
}
