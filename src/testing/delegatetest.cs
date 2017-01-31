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


    public class DelegateTest
    {
        public delegate void PublicDelegate();

        protected delegate void ProtectedDelegate();

        internal delegate void InternalDelegate();

        private delegate void PrivateDelegate();

        public StringDelegate stringDelegate;
        public ObjectDelegate objectDelegate;
        public BoolDelegate boolDelegate;

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
    }
}
