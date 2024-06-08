using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Python.Runtime.Native
{
    [StructLayout(LayoutKind.Sequential)]
    struct StrPtr : IDisposable
    {
        public IntPtr RawPointer { get; set; }
        unsafe byte* Bytes => (byte*)this.RawPointer;

        public unsafe StrPtr(string value) : this(value, Encodings.UTF8) {}

        public unsafe StrPtr(string value, Encoding encoding)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));
            if (encoding is null) throw new ArgumentNullException(nameof(encoding));

            var bytes = encoding.GetBytes(value);
            this.RawPointer = Marshal.AllocHGlobal(checked(bytes.Length + 1));
            try
            {
                Marshal.Copy(bytes, 0, this.RawPointer, bytes.Length);
                this.Bytes[bytes.Length] = 0;
            }
            catch
            {
                this.Dispose();
                throw;
            }
        }

        public unsafe string? ToString(Encoding encoding)
        {
            if (encoding is null) throw new ArgumentNullException(nameof(encoding));
            if (this.RawPointer == IntPtr.Zero) return null;

            return encoding.GetString((byte*)this.RawPointer, byteCount: checked((int)this.ByteCount));
        }

        public unsafe nuint ByteCount
        {
            get
            {
                if (this.RawPointer == IntPtr.Zero) throw new NullReferenceException();

                nuint zeroIndex = 0;
                while (this.Bytes[zeroIndex] != 0)
                {
                    zeroIndex++;
                }
                return zeroIndex;
            }
        }

        public void Dispose()
        {
            if (this.RawPointer == IntPtr.Zero)
                return;

            Marshal.FreeHGlobal(this.RawPointer);
            this.RawPointer = IntPtr.Zero;
        }

        internal static Encoding GetEncodingByPythonName(string pyEncodingName)
        {
            // https://stackoverflow.com/a/7798749/231238
            if (pyEncodingName == "mbcs") return Encoding.Default;

            return Encoding.GetEncoding(pyEncodingName);
        }
    }
}
