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
