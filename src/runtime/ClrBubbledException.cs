using System;
using System.Text;

namespace Python.Runtime
{
    /// <summary>
    /// Provides an abstraction to represent a .Net exception that is bubbled to Python and back to .Net
    /// and includes the Python traceback.
    /// </summary>
    public class ClrBubbledException : Exception
    {
        /// <summary>
        /// The Python traceback
        /// </summary>
        public string PythonTraceback { get; }

        /// <summary>
        /// Creates a new instance of <see cref="ClrBubbledException"/>
        /// </summary>
        /// <param name="sourceException">The original exception that was thrown in .Net</param>
        /// <param name="pythonTraceback">The Python traceback</param>
        public ClrBubbledException(Exception sourceException, string pythonTraceback)
            : base(sourceException.Message, sourceException)
        {
            PythonTraceback = pythonTraceback;
        }

        /// <summary>
        /// StackTrace Property
        /// </summary>
        /// <remarks>
        /// A string representing the exception stack trace.
        /// </remarks>
        public override string StackTrace
        {
            get
            {
                return PythonTraceback + "Underlying exception stack trace:" + Environment.NewLine + InnerException.StackTrace;
            }
        }

        public override string ToString()
        {
            StringBuilder description = new StringBuilder();
            description.AppendFormat("{0}: {1}{2}", InnerException.GetType().Name, Message, Environment.NewLine);
            description.AppendFormat(" --> {0}", PythonTraceback);
            description.AppendFormat("   --- End of Python traceback ---{0}", Environment.NewLine);

            if (InnerException.InnerException != null)
            {
                description.AppendFormat("   ---> {0}", InnerException.InnerException);
                description.AppendFormat("{0}     --- End of inner exception stack trace ---{0}", Environment.NewLine);
            }

            description.Append(InnerException.StackTrace);
            description.AppendFormat("{0}   --- End of underlying exception ---", Environment.NewLine);

            var str = description.ToString();
            return str;
        }
    }
}
