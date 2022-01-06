using System;
using System.Runtime.InteropServices;

namespace Python.Runtime
{
    /* buffer interface */
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct Py_buffer {
        public IntPtr buf;
        public IntPtr obj;        /* owned reference */
        /// <summary>Buffer size in bytes</summary>
        [MarshalAs(UnmanagedType.SysInt)]
        public nint len;
        [MarshalAs(UnmanagedType.SysInt)]
        public nint itemsize;  /* This is Py_ssize_t so it can be
                             pointed to by strides in simple case.*/
        [MarshalAs(UnmanagedType.Bool)]
        public bool _readonly;
        public int ndim;
        [MarshalAs(UnmanagedType.LPStr)]
        public string? format;
        public IntPtr shape;
        public IntPtr strides;
        public IntPtr suboffsets;
        public IntPtr _internal;
    }

    public enum BufferOrderStyle
    {
        C,
        Fortran,
        EitherOne,
    }

    /* Flags for getting buffers */
    public enum PyBUF
    {
        /// <summary>
        /// Simple buffer without shape strides and suboffsets
        /// </summary>
        SIMPLE = 0,
        /// <summary>
        /// Controls the <see cref="PyBuffer.ReadOnly"/> field. If set, the exporter MUST provide a writable buffer or else report failure. Otherwise, the exporter MAY provide either a read-only or writable buffer, but the choice MUST be consistent for all consumers.
        /// </summary>
        WRITABLE = 0x0001,
        /// <summary>
        /// Controls the <see cref="PyBuffer.Format"/> field. If set, this field MUST be filled in correctly. Otherwise, this field MUST be NULL.
        /// </summary>
        FORMATS = 0x0004,
        /// <summary>
        /// N-Dimensional buffer with shape
        /// </summary>
        ND = 0x0008,
        /// <summary>
        /// Buffer with strides and shape
        /// </summary>
        STRIDES = (0x0010 | ND),
        /// <summary>
        /// C-Contigous buffer with strides and shape
        /// </summary>
        C_CONTIGUOUS = (0x0020 | STRIDES),
        /// <summary>
        /// F-Contigous buffer with strides and shape
        /// </summary>
        F_CONTIGUOUS = (0x0040 | STRIDES),
        /// <summary>
        /// C or Fortran contigous buffer with strides and shape
        /// </summary>
        ANY_CONTIGUOUS = (0x0080 | STRIDES),
        /// <summary>
        /// Buffer with suboffsets (if needed)
        /// </summary>
        INDIRECT = (0x0100 | STRIDES),
        /// <summary>
        /// Writable C-Contigous buffer with shape
        /// </summary>
        CONTIG = (ND | WRITABLE),
        /// <summary>
        /// Readonly C-Contigous buffer with shape
        /// </summary>
        CONTIG_RO = (ND),
        /// <summary>
        /// Writable buffer with shape and strides
        /// </summary>
        STRIDED = (STRIDES | WRITABLE),
        /// <summary>
        /// Readonly buffer with shape and strides
        /// </summary>
        STRIDED_RO = (STRIDES),
        /// <summary>
        /// Writable buffer with shape, strides and format
        /// </summary>
        RECORDS = (STRIDES | WRITABLE | FORMATS),
        /// <summary>
        /// Readonly buffer with shape, strides and format
        /// </summary>
        RECORDS_RO = (STRIDES | FORMATS),
        /// <summary>
        /// Writable indirect buffer with shape, strides, format and suboffsets (if needed)
        /// </summary>
        FULL = (INDIRECT | WRITABLE | FORMATS),
        /// <summary>
        /// Readonly indirect buffer with shape, strides, format and suboffsets (if needed)
        /// </summary>
        FULL_RO = (INDIRECT | FORMATS),
    }

    internal struct PyBufferProcs
    {
        public IntPtr Get;
        public IntPtr Release;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int GetBufferProc(BorrowedReference obj, out Py_buffer buffer, PyBUF flags);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void ReleaseBufferProc(BorrowedReference obj, ref Py_buffer buffer);
}
