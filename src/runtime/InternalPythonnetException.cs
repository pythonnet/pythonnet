using System;

namespace Python.Runtime;

public class InternalPythonnetException : Exception
{
    public InternalPythonnetException(string message, Exception innerException)
        : base(message, innerException) { }
}
