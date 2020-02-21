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
            new Thread(() =>
            {
                Thread.Sleep(TimeSpan.FromSeconds(30));
                UploadDump();
                Console.Error.WriteLine("Test has been running for too long. Created memory dump");
                Environment.Exit(1);
            }) {
                IsBackground = true,
            }.Start();
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
            MiniDumpWriteDump(IntPtr.Zero, 0, IntPtr.Zero, MiniDumpType.Normal, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

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

        enum MiniDumpType
        {
            Normal,
        }
    }
}
