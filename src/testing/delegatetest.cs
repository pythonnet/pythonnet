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

    public delegate void PublicDelegate();
    internal delegate void InternalDelegate();

    public delegate DelegateTest ObjectDelegate();
    public delegate string StringDelegate();
    public delegate bool BoolDelegate();


    public class DelegateTest {

        public delegate void PublicDelegate();
        protected delegate void ProtectedDelegate();
        internal delegate void InternalDelegate();
        private delegate void PrivateDelegate();

        public StringDelegate stringDelegate;
        public ObjectDelegate objectDelegate;
        public BoolDelegate boolDelegate;

        public DelegateTest() {

        }

        public string SayHello() {
            return "hello";
        }

        public static string StaticSayHello() {
            return "hello";
        }

        public string CallStringDelegate(StringDelegate d) {
            return d();
        }

        public DelegateTest CallObjectDelegate(ObjectDelegate d) {
            return d();
        }

        public bool CallBoolDelegate(BoolDelegate d) {
            return d();
        }


    }


}
