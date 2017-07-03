namespace Python.Runtime
{
    using System;

    public static class RawMemUtils
    {
        public static unsafe bool CopyMemBlocks(IntPtr src, IntPtr dest, int size)
        {
            // XOR with 64 bit step
            var p64_1 = (ulong*)src;
            var p64_2 = (ulong*)dest;
            int c64count = size >> 3;

            int i = 0;
            while (i <c64count)
            {
                p64_2[i] = p64_1[i];
                i++;
            }

            var pn1 = (byte*)(src + (size & ~7));
            var pn2 = (byte*)(dest + (size & ~7));

            if ((size & 4) != 0)
            {
                *(uint*)pn2 = *(uint*)pn1;

                pn1 += 4;
                pn2 += 4;
            }

            if ((size & 2) != 0)
            {
                *(ushort*)pn2 = *(ushort*)pn1;

                pn1 += 2;
                pn2 += 2;
            }

            if ((size & 1) != 0)
            {
                *pn2 = *pn1;
            }

            return true;
        }

        public static unsafe bool CompareMemBlocks(IntPtr ptr1, IntPtr ptr2, int size)
        {
            // XOR with 64 bit step
            var p64_1 = (ulong*)ptr1;
            var p64_2 = (ulong*)ptr2;
            var pn1 = (byte*)(ptr1 + (size & ~7));
            var pn2 = (byte*)(ptr2 + (size & ~7));
            while (p64_1 < pn1)
            {
                if (*p64_1 != *p64_2)
                {
                    return false;
                }

                p64_1++;
                p64_2++;
            }

            if ((size & 4) != 0)
            {
                if (*(uint*)pn1 != *(uint*)pn2)
                {
                    return false;
                }

                pn1 += 4;
                pn2 += 4;
            }

            if ((size & 2) != 0)
            {
                if (*(ushort*)pn1 != *(ushort*)pn2)
                {
                    return false;
                }

                pn1 += 2;
                pn2 += 2;
            }

            if ((size & 1) != 0)
            {
                if (*pn1 != *pn2)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Calculating simple 32 bit xor hash for raw memory.
        /// </summary>
        /// <param name="mem">Memory pointer.</param>
        /// <param name="size">Size to hash.</param>
        /// <returns>32 bit hash the in signed int format.</returns>
        public static unsafe int FastXorHash(IntPtr mem, int size)
        {
            unchecked
            {
                // XOR with 64 bit step
                ulong r64 = 0;
                var p64 = (ulong*)mem;
                var pn = (byte*)(mem + (size & ~7));
                while (p64 < pn)
                {
                    r64 ^= *p64++;
                }

                uint r32 = (uint)r64 ^ (uint)(r64 >> 32);
                if ((size & 4) != 0)
                {
                    r32 ^= *(uint*)pn;
                    pn += 4;
                }

                if ((size & 2) != 0)
                {
                    r32 ^= *(ushort*)pn;
                    pn += 2;
                }

                if ((size & 1) != 0)
                {
                    r32 ^= *pn;
                }

                return (int)r32;
            }
        }
    }
}
