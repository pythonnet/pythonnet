using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Python.Runtime.Native
{
    [StructLayout(LayoutKind.Sequential)]
    struct NativeTypeSpec : IDisposable
    {
        public readonly StrPtr Name;
        public readonly int BasicSize;
        public readonly int ItemSize;
        public readonly int Flags;
        public IntPtr Slots;

        public NativeTypeSpec(TypeSpec spec)
        {
            if (spec is null) throw new ArgumentNullException(nameof(spec));

            this.Name = new StrPtr(spec.Name);
            this.BasicSize = spec.BasicSize;
            this.ItemSize = spec.ItemSize;
            this.Flags = (int)spec.Flags;

            unsafe
            {
                int slotsBytes = checked((spec.Slots.Count + 1) * Marshal.SizeOf<TypeSpec.Slot>());
                var slots = (TypeSpec.Slot*)Marshal.AllocHGlobal(slotsBytes);
                for (int slotIndex = 0; slotIndex < spec.Slots.Count; slotIndex++)
                    slots[slotIndex] = spec.Slots[slotIndex];
                slots[spec.Slots.Count] = default;
                this.Slots = (IntPtr)slots;
            }
        }

        public void Dispose()
        {
            // we have to leak the name
            // this.Name.Dispose();
            Marshal.FreeHGlobal(this.Slots);
            this.Slots = IntPtr.Zero;
        }
    }
}
