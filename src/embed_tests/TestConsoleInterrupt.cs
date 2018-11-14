using Microsoft.CSharp;
using NUnit.Framework;
using Python.Runtime;
using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Python.EmbeddingTest
{
    /// <summary>
    /// This class tests that a console application with PythonEngine Initialize can be interrupted by CTRL+C. 
    /// </summary>
    public class TestConsoleInterrupt
    {
        static readonly string program = @"
        using System;
        using Python.Runtime;
        using System.Text;
        using System.Diagnostics;
        class Program
        {
            static void Main(string[] args)
            {
                PythonEngine.Initialize(); // Comment this out to support CTRL+C again.
                Console.WriteLine(""Waiting 5s.. Try CTRL+C."");
                System.Threading.Thread.Sleep(5000);
                Console.WriteLine(""Timeout"");
            }
        }
    ";

        static string BuildAssembly(string code)
        {
            var provider = new CSharpCodeProvider();
            var compilerparams = new CompilerParameters();
            compilerparams.GenerateExecutable = true;
            compilerparams.GenerateInMemory = false;
            if (IntPtr.Size == 8)
            {
                compilerparams.CompilerOptions = "/platform:x64";
            }
            else
            {
                compilerparams.CompilerOptions = "/platform:x86";
            }

            var dir = TestContext.CurrentContext.TestDirectory;

            compilerparams.OutputAssembly = System.IO.Path.Combine(dir, "testctrl.exe");
            compilerparams.ReferencedAssemblies.Add(typeof(PythonEngine).Assembly.Location);
            CompilerResults results = provider.CompileAssemblyFromSource(compilerparams, code);
            if (results.Errors.HasErrors)
            {
                StringBuilder errors = new StringBuilder("Compiler Errors :\r\n");
                foreach (CompilerError error in results.Errors)
                {
                    errors.AppendFormat("Line {0},{1}\t: {2}\n", error.Line, error.Column, error.ErrorText);
                }
                throw new Exception(errors.ToString());
            }
            else
            {
                return results.PathToAssembly;
            }
        }

#if MONO_LINUX || MONO_OSX
        [DllImport("libc")]
        static extern bool kill(int pid, int signal);

        static void invokeSigInt(Process proc)
        {
            int SIGINT = 2;
            kill(proc.Id, SIGINT);
        }

#else

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GenerateConsoleCtrlEvent(int sigevent, int dwProcessGroupId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        static extern bool SetConsoleCtrlHandler(int handler, bool add);
        
        static void invokeSigInt(Process proc)
        {
            FreeConsole();
            AttachConsole((uint)proc.Id);
            SetConsoleCtrlHandler(0, true);
            System.Threading.Thread.Sleep(50);
            GenerateConsoleCtrlEvent(0, 0);
            System.Threading.Thread.Sleep(50);
            FreeConsole();
            SetConsoleCtrlHandler(0, false);
        }

#endif        

        [Test]
        public static void ConsoleInterrupt()
        {
            var path = BuildAssembly(program);
            using (var process = Process.Start(new ProcessStartInfo(path, "") { UseShellExecute = false, RedirectStandardOutput = true }))
            {
                
                System.Threading.Thread.Sleep(100);
                invokeSigInt(process);
                if (!process.WaitForExit(500))
                {
                    process.Kill();
                    Assert.Fail();
                }
                var str = process.StandardOutput.ReadToEnd();
                Assert.IsTrue(str.Contains("Waiting")); // Program starts with "Waiting".
                Assert.IsTrue(str.Contains("Timeout") == false); // program ends with "Timeout" but we should never get there.
            }
        }
    }
}
