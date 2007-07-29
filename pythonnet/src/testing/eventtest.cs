// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using System;
using System.Windows.Forms;

namespace Python.Test {

    //========================================================================
    // Supports CLR event unit tests.
    //========================================================================

    public delegate void TestEventHandler(object sender, TestEventArgs e);


    public class EventTest {


        public void WinFormTest() {
            EventTest e = new EventTest();
            EventHandler h = new EventHandler(e.ClickHandler);

            Form f = new Form();
            f.Click += h;
            //f.Click(null, new EventArgs());
            f.Click -= h;
        }

        public void ClickHandler(object sender, EventArgs e) {
            Console.WriteLine("click");
        }


        public static event TestEventHandler PublicStaticEvent;

        protected static event TestEventHandler ProtectedStaticEvent;

        internal static event TestEventHandler InternalStaticEvent;

        private static event TestEventHandler PrivateStaticEvent;

        public event TestEventHandler PublicEvent;

        protected event TestEventHandler ProtectedEvent;

        internal event TestEventHandler InternalEvent;

        private event TestEventHandler PrivateEvent;


        public static int s_value;
        public int value;

        public EventTest () {
            this.value = 0;
        }

        static EventTest () {
            s_value = 0;
        }


        public void OnPublicEvent(TestEventArgs e) {
            if (PublicEvent != null) {
                PublicEvent(this, e);
            }
        }


        public void OnProtectedEvent(TestEventArgs e) {
            if (ProtectedEvent != null) {
                ProtectedEvent(this, e);
            }
        }


        public static void OnPublicStaticEvent(TestEventArgs e) {
            if (PublicStaticEvent != null) {
                PublicStaticEvent(null, e);
            }
        }


        protected static void OnProtectedStaticEvent(TestEventArgs e) {
            if (ProtectedStaticEvent != null) {
                ProtectedStaticEvent(null, e);
            }
        }


        public void GenericHandler(object sender, TestEventArgs e) {
            this.value = e.value;
        }

        public static void StaticHandler(object sender, TestEventArgs e) {
            s_value = e.value;
        }

        public static void ShutUpCompiler() {
            // Quiet compiler warnings.
            EventTest e = new EventTest();
            TestEventHandler f = new TestEventHandler(e.GenericHandler);
            ProtectedStaticEvent += f;
            InternalStaticEvent += f;
            PrivateStaticEvent += f;
            e.ProtectedEvent += f;
            e.InternalEvent += f;
            e.PrivateEvent += f;
        }

    }


    public class TestEventArgs : EventArgs {
        public int value;

        public TestEventArgs(int v) {
            this.value = v;
        }

    }


}
