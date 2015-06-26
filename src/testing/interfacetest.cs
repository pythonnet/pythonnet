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
    // Supports CLR class unit tests.
    //========================================================================

    public interface IPublicInterface {}

    internal interface IInternalInterface {}



    public interface ISayHello1 {
        string SayHello();
    }

    public interface ISayHello2 {
        string SayHello();
    }

    public class InterfaceTest : ISayHello1, ISayHello2{

        public InterfaceTest() {}

        public string HelloProperty {
            get { return "hello"; }
        }

        string ISayHello1.SayHello() {
            return "hello 1";
        }

        string ISayHello2.SayHello() {
            return "hello 2";
        }

        public interface IPublic {}

        protected interface IProtected {}

        internal interface IInternal {}

        private interface IPrivate {}

    }


}
