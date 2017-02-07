namespace Python.Test
{
    /// <summary>
    /// Supports units tests for property access.
    /// </summary>
    public class PropertyTest
    {
        public PropertyTest()
        {
        }

        private int _public_property = 0;

        public int PublicProperty
        {
            get { return _public_property; }
            set { _public_property = value; }
        }

        private static int _public_static_property = 0;

        public static int PublicStaticProperty
        {
            get { return _public_static_property; }
            set { _public_static_property = value; }
        }

        private int _protected_property = 0;

        protected int ProtectedProperty
        {
            get { return _protected_property; }
            set { _protected_property = value; }
        }

        private static int _protected_static_property = 0;

        protected static int ProtectedStaticProperty
        {
            get { return _protected_static_property; }
            set { _protected_static_property = value; }
        }

        private int _internal_property = 0;

        internal int InternalProperty
        {
            get { return _internal_property; }
            set { _internal_property = value; }
        }

        private static int _internal_static_property = 0;

        internal static int InternalStaticProperty
        {
            get { return _internal_static_property; }
            set { _internal_static_property = value; }
        }

        private int _private_property = 0;

        private int PrivateProperty
        {
            get { return _private_property; }
            set { _private_property = value; }
        }

        private static int _private_static_property = 0;

        private static int PrivateStaticProperty
        {
            get { return _private_static_property; }
            set { _private_static_property = value; }
        }

        private ShortEnum _enum_property = ShortEnum.Zero;

        public ShortEnum EnumProperty
        {
            get { return _enum_property; }
            set { _enum_property = value; }
        }
    }
}
