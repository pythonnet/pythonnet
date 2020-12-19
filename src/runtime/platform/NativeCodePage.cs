using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Python.Runtime.Platform
{
    class NativeCodePageHelper
    {
        /// <summary>
        /// Initialized by InitializeNativeCodePage.
        ///
        /// This points to a page of memory allocated using mmap or VirtualAlloc
        /// (depending on the system), and marked read and execute (not write).
        /// Very much on purpose, the page is *not* released on a shutdown and
        /// is instead leaked. See the TestDomainReload test case.
        ///
        /// The contents of the page are two native functions: one that returns 0,
        /// one that returns 1.
        ///
        /// If python didn't keep its gc list through a Py_Finalize we could remove
        /// this entire section.
        /// </summary>
        internal static IntPtr NativeCodePage = IntPtr.Zero;


        /// <summary>
        /// Structure to describe native code.
        ///
        /// Use NativeCode.Active to get the native code for the current platform.
        ///
        /// Generate the code by creating the following C code:
        /// <code>
        /// int Return0() { return 0; }
        /// int Return1() { return 1; }
        /// </code>
        /// Then compiling on the target platform, e.g. with gcc or clang:
        /// <code>cc -c -fomit-frame-pointer -O2 foo.c</code>
        /// And then analyzing the resulting functions with a hex editor, e.g.:
        /// <code>objdump -disassemble foo.o</code>
        /// </summary>
        internal class NativeCode
        {
            /// <summary>
            /// The code, as a string of bytes.
            /// </summary>
            public byte[] Code { get; private set; }

            /// <summary>
            /// Where does the "return 0" function start?
            /// </summary>
            public int Return0 { get; private set; }

            /// <summary>
            /// Where does the "return 1" function start?
            /// </summary>
            public int Return1 { get; private set; }

            public static NativeCode Active
            {
                get
                {
                    switch (RuntimeInformation.ProcessArchitecture)
                    {
                        case Architecture.X86:
                            return I386;
                        case Architecture.X64:
                            return X86_64;
                        default:
                            return null;
                    }
                }
            }

            /// <summary>
            /// Code for x86_64. See the class comment for how it was generated.
            /// </summary>
            public static readonly NativeCode X86_64 = new NativeCode()
            {
                Return0 = 0x10,
                Return1 = 0,
                Code = new byte[]
                {
                    // First Return1:
                    0xb8, 0x01, 0x00, 0x00, 0x00, // movl $1, %eax
                    0xc3, // ret

                    // Now some padding so that Return0 can be 16-byte-aligned.
                    // I put Return1 first so there's not as much padding to type in.
                    0x66, 0x2e, 0x0f, 0x1f, 0x84, 0x00, 0x00, 0x00, 0x00, 0x00, // nop

                    // Now Return0.
                    0x31, 0xc0, // xorl %eax, %eax
                    0xc3, // ret
                }
            };

            /// <summary>
            /// Code for X86.
            ///
            /// It's bitwise identical to X86_64, so we just point to it.
            /// <see cref="NativeCode.X86_64"/>
            /// </summary>
            public static readonly NativeCode I386 = X86_64;
        }

        /// <summary>
        /// Platform-dependent mmap and mprotect.
        /// </summary>
        internal interface IMemoryMapper
        {
            /// <summary>
            /// Map at least numBytes of memory. Mark the page read-write (but not exec).
            /// </summary>
            IntPtr MapWriteable(int numBytes);

            /// <summary>
            /// Sets the mapped memory to be read-exec (but not write).
            /// </summary>
            void SetReadExec(IntPtr mappedMemory, int numBytes);
        }

        class WindowsMemoryMapper : IMemoryMapper
        {
            const UInt32 MEM_COMMIT = 0x1000;
            const UInt32 MEM_RESERVE = 0x2000;
            const UInt32 PAGE_READWRITE = 0x04;
            const UInt32 PAGE_EXECUTE_READ = 0x20;

            [DllImport("kernel32.dll")]
            static extern IntPtr VirtualAlloc(IntPtr lpAddress, IntPtr dwSize, UInt32 flAllocationType, UInt32 flProtect);

            [DllImport("kernel32.dll")]
            static extern bool VirtualProtect(IntPtr lpAddress, IntPtr dwSize, UInt32 flNewProtect, out UInt32 lpflOldProtect);

            public IntPtr MapWriteable(int numBytes)
            {
                return VirtualAlloc(IntPtr.Zero, new IntPtr(numBytes),
                                    MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE);
            }

            public void SetReadExec(IntPtr mappedMemory, int numBytes)
            {
                UInt32 _;
                VirtualProtect(mappedMemory, new IntPtr(numBytes), PAGE_EXECUTE_READ, out _);
            }
        }

        class UnixMemoryMapper : IMemoryMapper
        {
            const int PROT_READ = 0x1;
            const int PROT_WRITE = 0x2;
            const int PROT_EXEC = 0x4;

            const int MAP_PRIVATE = 0x2;
            int MAP_ANONYMOUS
            {
                get
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        return 0x20;
                    }
                    else
                    {
                        // OSX, FreeBSD
                        return 0x1000;
                    }
                }
            }

            [DllImport("libc")]
            static extern IntPtr mmap(IntPtr addr, IntPtr len, int prot, int flags, int fd, IntPtr offset);

            [DllImport("libc")]
            static extern int mprotect(IntPtr addr, IntPtr len, int prot);

            public IntPtr MapWriteable(int numBytes)
            {
                // MAP_PRIVATE must be set on linux, even though MAP_ANON implies it.
                // It doesn't hurt on darwin, so just do it.
                return mmap(IntPtr.Zero, new IntPtr(numBytes), PROT_READ | PROT_WRITE, MAP_PRIVATE | MAP_ANONYMOUS, -1, IntPtr.Zero);
            }

            public void SetReadExec(IntPtr mappedMemory, int numBytes)
            {
                mprotect(mappedMemory, new IntPtr(numBytes), PROT_READ | PROT_EXEC);
            }
        }

        internal static IMemoryMapper CreateMemoryMapper()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new WindowsMemoryMapper();
            }
            else
            {
                // Linux, OSX, FreeBSD
                return new UnixMemoryMapper();
            }
        }

        /// <summary>
        /// Initializes the native code page.
        ///
        /// Safe to call if we already initialized (this function is idempotent).
        /// <see cref="NativeCodePage"/>
        /// </summary>
        internal static void InitializeNativeCodePage()
        {
            // Do nothing if we already initialized.
            if (NativeCodePage != IntPtr.Zero)
            {
                return;
            }

            // Allocate the page, write the native code into it, then set it
            // to be executable.
            IMemoryMapper mapper = CreateMemoryMapper();
            int codeLength = NativeCode.Active.Code.Length;
            NativeCodePage = mapper.MapWriteable(codeLength);
            Marshal.Copy(NativeCode.Active.Code, 0, NativeCodePage, codeLength);
            mapper.SetReadExec(NativeCodePage, codeLength);
        }
    }
}
