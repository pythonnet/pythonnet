using System;

namespace Python.Runtime
{
    /// <summary>
    /// Raised when a CLR type would be reflected into Python but an
    /// <see cref="IClrTypeFilter"/> registered on
    /// <see cref="InteropConfiguration.ClrTypeFilters"/> refused it.
    /// </summary>
    public class ClrTypeFilteredException : Exception
    {
        public Type FilteredType { get; }

        public ClrTypeFilteredException(Type type)
            : base($"Reflecting {type?.FullName ?? "<null>"} into Python was refused by an IClrTypeFilter.")
            => FilteredType = type!;
    }
}
