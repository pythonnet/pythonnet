namespace Python.Test
{
    /// <summary>
    /// Supports CLR class unit tests.
    /// </summary>
    public delegate void PublicDelegate();

    internal delegate void InternalDelegate();

    public delegate DelegateTest ObjectDelegate();

    public delegate string StringDelegate();

    public delegate bool BoolDelegate();

    public delegate void OutStringDelegate(out string value);
    public delegate void RefStringDelegate(ref string value);
    public delegate void OutIntDelegate(out int value);
    public delegate void RefIntDelegate(ref int value);
    public delegate void RefIntRefStringDelegate(ref int intValue, ref string stringValue);
    public delegate int IntRefIntRefStringDelegate(ref int intValue, ref string stringValue);

    public class DelegateTest
    {
        public delegate void PublicDelegate();

        protected delegate void ProtectedDelegate();

        internal delegate void InternalDelegate();

        private delegate void PrivateDelegate();

        public StringDelegate stringDelegate;
        public ObjectDelegate objectDelegate;
        public BoolDelegate boolDelegate;
        public OutStringDelegate outStringDelegate;
        public RefStringDelegate refStringDelegate;

        public DelegateTest()
        {
        }

        public string SayHello()
        {
            return "hello";
        }

        public static string StaticSayHello()
        {
            return "hello";
        }

        public void OutHello(out string value)
        {
            value = "hello";
        }

        public string CallStringDelegate(StringDelegate d)
        {
            return d();
        }

        public DelegateTest CallObjectDelegate(ObjectDelegate d)
        {
            return d();
        }

        public bool CallBoolDelegate(BoolDelegate d)
        {
            return d();
        }

        public void CallOutIntDelegate(OutIntDelegate d, out int value)
        {
            d(out value);
        }

        public void CallRefIntDelegate(RefIntDelegate d, ref int value)
        {
            d(ref value);
        }

        public void CallOutStringDelegate(OutStringDelegate d, out string value)
        {
            d(out value);
        }

        public void CallRefStringDelegate(RefStringDelegate d, ref string value)
        {
            d(ref value);
        }

        public void CallRefIntRefStringDelegate(RefIntRefStringDelegate d, ref int intValue, ref string stringValue)
        {
            d(ref intValue, ref stringValue);
        }

        public int CallIntRefIntRefStringDelegate(IntRefIntRefStringDelegate d, ref int intValue, ref string stringValue)
        {
            return d(ref intValue, ref stringValue);
        }
    }
}
