using System;
using System.Reflection;
using NUnit.Common;
using NUnitLite;

namespace Python.EmbeddingTest
{
    public class Program
    {
        public static int Main(string[] args)
        {
            ////var example = new TestExample();
            ////example.SetUp();
            ////example.TestReadme();
            ////example.Dispose();
            ////return 0;
            return new AutoRun(typeof(Program).GetTypeInfo().Assembly)
                .Execute(args, new ExtendedTextWrapper(Console.Out), Console.In);
        }
    }
}
