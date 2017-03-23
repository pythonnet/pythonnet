using System;

namespace Python.Test
{
    /// <summary>
    /// Supports CLR event unit tests.
    /// </summary>
    public delegate void EventHandlerTest(object sender, EventArgsTest e);


    public class EventTest
    {
        public static event EventHandlerTest PublicStaticEvent;

        protected static event EventHandlerTest ProtectedStaticEvent;

        internal static event EventHandlerTest InternalStaticEvent;

        private static event EventHandlerTest PrivateStaticEvent;

        public event EventHandlerTest PublicEvent;

        protected event EventHandlerTest ProtectedEvent;

        internal event EventHandlerTest InternalEvent;

        private event EventHandlerTest PrivateEvent;


        public static int s_value;
        public int value;

        public EventTest()
        {
            value = 0;
        }

        static EventTest()
        {
            s_value = 0;
        }


        public void OnPublicEvent(EventArgsTest e)
        {
            if (PublicEvent != null)
            {
                PublicEvent(this, e);
            }
        }


        public void OnProtectedEvent(EventArgsTest e)
        {
            if (ProtectedEvent != null)
            {
                ProtectedEvent(this, e);
            }
        }


        public static void OnPublicStaticEvent(EventArgsTest e)
        {
            if (PublicStaticEvent != null)
            {
                PublicStaticEvent(null, e);
            }
        }


        protected static void OnProtectedStaticEvent(EventArgsTest e)
        {
            if (ProtectedStaticEvent != null)
            {
                ProtectedStaticEvent(null, e);
            }
        }


        public void GenericHandler(object sender, EventArgsTest e)
        {
            value = e.value;
        }

        public static void StaticHandler(object sender, EventArgsTest e)
        {
            s_value = e.value;
        }

        public static void ShutUpCompiler()
        {
            // Quiet compiler warnings.
            var e = new EventTest();
            EventHandlerTest f = e.GenericHandler;
            ProtectedStaticEvent += f;
            InternalStaticEvent += f;
            PrivateStaticEvent += f;
            e.ProtectedEvent += f;
            e.InternalEvent += f;
            e.PrivateEvent += f;
        }
    }


    public class EventArgsTest : EventArgs
    {
        public int value;

        public EventArgsTest(int v)
        {
            value = v;
        }
    }
}
