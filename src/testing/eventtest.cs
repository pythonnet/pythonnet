using System;

namespace Python.Test
{
    /// <summary>
    /// Supports CLR event unit tests.
    /// </summary>
    public delegate void EventHandlerTest(object sender, EventArgsTest e);

    #pragma warning disable 67 // Unused events, these are only accessed from Python
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

        public event OutStringDelegate OutStringEvent;
        public event OutIntDelegate OutIntEvent;
        public event RefStringDelegate RefStringEvent;
        public event RefIntDelegate RefIntEvent;

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

        public void OnRefStringEvent(ref string data)
        {
            RefStringEvent?.Invoke(ref data);
        }

        public void OnRefIntEvent(ref int data)
        {
            RefIntEvent?.Invoke(ref data);
        }

        public void OnOutStringEvent(out string data)
        {
            data = default;
            OutStringEvent?.Invoke(out data);
        }

        public void OnOutIntEvent(out int data)
        {
            data = default;
            OutIntEvent?.Invoke(out data);
        }

        public void GenericHandler(object sender, EventArgsTest e)
        {
            value = e.value;
        }

        public static void StaticHandler(object sender, EventArgsTest e)
        {
            s_value = e.value;
        }

        public void OutStringHandler(out string data)
        {
            data = value.ToString();
        }

        public void OutIntHandler(out int data)
        {
            data = value;
        }

        public void RefStringHandler(ref string data)
        {
            data += "!";
        }

        public void RefIntHandler(ref int data)
        {
            data++;
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
    #pragma warning restore 67


    public class EventArgsTest : EventArgs
    {
        public int value;

        public EventArgsTest(int v)
        {
            value = v;
        }
    }
}
