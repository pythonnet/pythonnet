using System;

using NUnit.Common;

using NUnitLite;

namespace Python.EmbeddingTest
{
    public class Program
    {
        public static int Main(string[] args)
        {
            return new AutoRun(typeof(Program).Assembly).Execute(
                args,
                new ExtendedTextWrapper(Console.Out),
                Console.In);
        }
    }
}
