// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using System;


namespace Python.Test {

    //========================================================================
    // Supports CLR Exception unit tests.
    //========================================================================

    public class ExceptionTest {

        public int ThrowProperty {
            get {
                throw new OverflowException("error");
            }
            set { 
                throw new OverflowException("error");
            }
        }

        public static Exception GetBaseException() {
            return new Exception("error");
        }

        public static OverflowException GetExplicitException() {
            return new OverflowException("error");
        }

        public static Exception GetWidenedException() {
            return new OverflowException("error");
        }

        public static ExtendedException GetExtendedException() {
            return new ExtendedException("error");
        }


        public static bool SetBaseException(Exception e) {
            return typeof(Exception).IsInstanceOfType(e);
        }

        public static bool SetExplicitException(OverflowException e) {
            return typeof(OverflowException).IsInstanceOfType(e);
        }

        public static bool SetWidenedException(Exception e) {
            return typeof(Exception).IsInstanceOfType(e);
        }

        public static bool ThrowException() {
            throw new OverflowException("error");
        }
    }


    public class ExtendedException : OverflowException {

        public ExtendedException() : base() {}
        public ExtendedException(string m) : base(m) {}

        public string extra = "extra";

        public string ExtraProperty {
            get { 
                return extra;
            }
            set { 
                extra = value;
            }
        }

        public string GetExtraInfo() {
            return extra;
        }


    }



}




