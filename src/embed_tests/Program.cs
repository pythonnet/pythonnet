using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Common;

using NUnitLite;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class Program
    {
        public static int Main(string[] args)
        {
            if (args.Contains("--loop"))
            {
                args = args.Where(x => x != "--loop").ToArray();
                int result;
                int runNumber = 0;
                string pathEnv = Environment.GetEnvironmentVariable("PATH");
                do
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Error.WriteLine($"----- Run = {++runNumber}, MemUsage = {Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024} Mb -----");
                    Console.ForegroundColor = ConsoleColor.Gray;

                    result = new AutoRun(typeof(Program).Assembly).Execute(
                        args,
                        new ExtendedTextWrapper(Console.Out),
                        Console.In);

                    // Python does not see Environment.SetEnvironmentVariable changes.
                    // So we needs restore PATH variable in a pythonic way.
                    using (new PythonEngine())
                    {
                        dynamic os = PythonEngine.ImportModule("os");
                        os.environ["PATH"] = new PyString(pathEnv);
                    }
                } while (true);

                return result;
            }
            else
            {
                return new AutoRun(typeof(Program).Assembly).Execute(
                    args,
                    new ExtendedTextWrapper(Console.Out),
                    Console.In);
            }
        }
    }
}
