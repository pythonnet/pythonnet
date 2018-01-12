namespace Python.Runtime
{
    using System;

    public struct RawImmutableMemBlock: IEquatable<RawImmutableMemBlock>
    {
        private readonly int _hash;

        public RawImmutableMemBlock(IntPtr ptr, int size)
        {
            if (ptr == IntPtr.Zero)
            {
                throw new ArgumentException("Memory pointer should not be zero", nameof(ptr));
            }

            if (size < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "Size should be zero or positive.");
            }

            Ptr = ptr;
            Size = size;
            _hash = RawMemUtils.FastXorHash(ptr, size);
        }

        public RawImmutableMemBlock(RawImmutableMemBlock memBlock, IntPtr newPtr)
        {
            if (memBlock.Ptr == IntPtr.Zero)
            {
                throw new ArgumentException("Cannot copy non initialized RawImmutableMemBlock structure.", nameof(memBlock));
            }

            if (newPtr == IntPtr.Zero)
            {
                throw new ArgumentException("Cannot copy to zero pointer.");
            }

            RawMemUtils.CopyMemBlocks(memBlock.Ptr, newPtr, memBlock.Size);
            Ptr = newPtr;
            Size = memBlock.Size;
            _hash = memBlock._hash;
        }

        public IntPtr Ptr { get; }

        public int Size { get; }

        public bool Equals(RawImmutableMemBlock other)
        {
            bool preEqual = _hash == other._hash && Size == other.Size;
            if (!preEqual)
            {
                return false;
            }

            return RawMemUtils.CompareMemBlocks(Ptr, other.Ptr, Size);
        }

        /// <inheritdoc/> 
        public override bool Equals(object obj)
        {
            return obj is RawImmutableMemBlock && Equals((RawImmutableMemBlock)obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return (_hash * 397) ^ Size;
            }
        }

        public static bool operator ==(RawImmutableMemBlock left, RawImmutableMemBlock right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RawImmutableMemBlock left, RawImmutableMemBlock right)
        {
            return !left.Equals(right);
        }
    }
}
