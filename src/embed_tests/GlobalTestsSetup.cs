using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.Win32.SafeHandles;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    // As the SetUpFixture, the OneTimeTearDown of this class is executed after
    // all tests have run.
    [SetUpFixture]
    public class GlobalTestsSetup
    {
        [OneTimeSetUp]
        public void GlobalSetup()
        {
            if (!Debugger.IsAttached)
            {
                new Thread(() =>
                {
                    Thread.Sleep(TimeSpan.FromSeconds(30));
                    UploadDump();
                    Console.Error.WriteLine("Test has been running for too long. Created memory dump");
                    Environment.Exit(1);
                })
                {
                    IsBackground = true,
                }.Start();
            }
        }

        [OneTimeTearDown]
        public void FinalCleanup()
        {
            if (PythonEngine.IsInitialized)
            {
                PythonEngine.Shutdown();
            }
        }

        static void UploadDump()
        {
            var self = Process.GetCurrentProcess();

            const string dumpPath = "memory.dmp";

            // ensure DbgHelp is loaded
            MiniDumpWriteDump(IntPtr.Zero, 0, IntPtr.Zero,
                MiniDumpType.WithFullMemory | MiniDumpType.IgnoreInaccessibleMemory,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

            using (var fileHandle = CreateFile(dumpPath, FileAccess.Write, FileShare.Write,
                securityAttrs: IntPtr.Zero, dwCreationDisposition: FileMode.Create, dwFlagsAndAttributes: 0,
                hTemplateFile: IntPtr.Zero))
            {
                if (fileHandle.IsInvalid)
                    throw new Win32Exception();

                if (!MiniDumpWriteDump(self.Handle, self.Id, fileHandle.DangerousGetHandle(), MiniDumpType.Normal,
                    causeException: IntPtr.Zero, userStream: IntPtr.Zero, callback: IntPtr.Zero))
                    throw new Win32Exception();
            }

            const string archivePath = "memory.zip";
            string filesToPack = '"' + dumpPath + '"';
            filesToPack += ' ' + GetFilesToPackFor(Assembly.GetExecutingAssembly());
            filesToPack += ' ' + GetFilesToPackFor(typeof(PyObject).Assembly);

            Process.Start("7z", $"a {archivePath} {filesToPack}").WaitForExit();
            Process.Start("appveyor", arguments: $"PushArtifact -Path \"{archivePath}\"").WaitForExit();
        }

        static string GetFilesToPackFor(Assembly assembly)
        {
            string result = '"' + assembly.Location + '"';
            string pdb = Path.ChangeExtension(assembly.Location, ".pdb");
            if (File.Exists(pdb))
            {
                result += $" \"{pdb}\"";
            }
            return result;
        }

        [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            FileAccess dwDesiredAccess,
            FileShare dwShareMode,
            IntPtr securityAttrs,
            FileMode dwCreationDisposition,
            int dwFlagsAndAttributes,
            IntPtr hTemplateFile);
        [DllImport("DbgHelp", SetLastError = true)]
        static extern bool MiniDumpWriteDump(IntPtr hProcess,
            int processID, IntPtr hFile, MiniDumpType dumpType,
            IntPtr causeException, IntPtr userStream, IntPtr callback);

        [Flags]
        enum MiniDumpType
        {
            Normal = 0x00000000,
            WithDataSegments = 0x00000001,
            WithFullMemory = 0x00000002,
            WithHandleData = 0x00000004,
            FilterMemory = 0x00000008,
            ScanMemory = 0x00000010,
            WithUnloadedModules = 0x00000020,
            WithIndirectlyReferencedMemory = 0x00000040,
            FilterModulePaths = 0x00000080,
            WithProcessThreadData = 0x00000100,
            WithPrivateReadWriteMemory = 0x00000200,
            WithoutOptionalData = 0x00000400,
            WithFullMemoryInfo = 0x00000800,
            WithThreadInfo = 0x00001000,
            WithCodeSegments = 0x00002000,
            WithoutAuxiliaryState = 0x00004000,
            WithFullAuxiliaryState = 0x00008000,
            WithPrivateWriteCopyMemory = 0x00010000,
            IgnoreInaccessibleMemory = 0x00020000,
            WithTokenInformation = 0x00040000,
            WithModuleHeaders = 0x00080000,
            FilterTriage = 0x00100000,
            WithAvxXStateContext = 0x00200000,
            ValidTypeFlags = 0x003fffff,
        }
    }
}
