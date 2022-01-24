using Python.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Python.Test
{
    /// <summary>
    /// Supports CLR Exception unit tests.
    /// </summary>
    public class ExceptionTest
    {
        public int ThrowProperty
        {
            get { throw new OverflowException("error"); }
            set { throw new OverflowException("error"); }
        }

        public static Exception GetBaseException()
        {
            return new Exception("error");
        }

        public static OverflowException GetExplicitException()
        {
            return new OverflowException("error");
        }

        public static Exception GetWidenedException()
        {
            return new OverflowException("error");
        }

        public static ExtendedException GetExtendedException()
        {
            return new ExtendedException("error");
        }


        public static bool SetBaseException(Exception e)
        {
            return typeof(Exception).IsInstanceOfType(e);
        }

        public static bool SetExplicitException(OverflowException e)
        {
            return typeof(OverflowException).IsInstanceOfType(e);
        }

        public static bool SetWidenedException(Exception e)
        {
            return typeof(Exception).IsInstanceOfType(e);
        }

        public static bool ThrowException()
        {
            throw new OverflowException("error");
        }

        public static IEnumerable<int> ThrowExceptionInIterator(Exception e)
        {
            yield return 1;
            yield return 2;
            throw e;
        }

        public static void ThrowChainedExceptions()
        {
            try
            {
                try
                {
                    throw new Exception("Innermost exception");
                }
                catch (Exception exc)
                {
                    throw new Exception("Inner exception", exc);
                }
            }
            catch (Exception exc2)
            {
                throw new Exception("Outer exception", exc2);
            }
        }
        
        public static IntPtr DoThrowSimple()
        {
            using (Py.GIL())
            {
                dynamic builtins = Py.Import("builtins");
                var typeErrorType = new PyType(builtins.TypeError);
                var pyerr = new PythonException(typeErrorType, value:null, traceback:null, "Type error, the first", innerException:null);
                throw new ArgumentException("Bogus bad parameter", pyerr);

            }
        }

        public static void DoThrowWithInner()
        {
            using(Py.GIL())
            {
                // create a TypeError
                dynamic builtins = Py.Import("builtins");
                var pyerrFirst = new PythonException(new PyType(builtins.TypeError), value:null, traceback:null, "Type error, the first", innerException:null);

                // Create an ArgumentException, but as a python exception, with the previous type error as the inner exception
                var argExc = new ArgumentException("Bogus bad parameter", pyerrFirst);
                var argExcPyObj = argExc.ToPython();
                var pyArgExc = new PythonException(argExcPyObj.GetPythonType(), value:null, traceback:null, argExc.Message, innerException:argExc.InnerException);
                // This object must be disposed explicitly or else we get a false-positive leak.
                argExcPyObj.Dispose();
                
                // Then throw a TypeError with the ArgumentException-as-python-error exception as inner.
                var pyerrSecond = new PythonException(new PyType(builtins.TypeError), value:null, traceback:null, "Type error, the second", innerException:pyArgExc);
                throw pyerrSecond;

            }
        }
    }


    public class ExtendedException : OverflowException
    {
        public ExtendedException()
        {
        }

        public ExtendedException(string m) : base(m)
        {
        }

        public string extra = "extra";

        public string ExtraProperty
        {
            get { return extra; }
            set { extra = value; }
        }

        public string GetExtraInfo()
        {
            return extra;
        }
    }
}
