namespace Python.Test
{
    /// <summary>
    /// Supports units tests for field access.
    /// </summary>
    public class FieldTest
    {
        public FieldTest()
        {
            EnumField = ShortEnum.Zero;
            SpamField = new Spam("spam");
            StringField = "spam";
        }

        public void Shutup()
        {
            int i = PrivateStaticField;
            int j = PrivateField;
        }

        public static readonly int ReadOnlyStaticField = 0;
        protected static int ProtectedStaticField = 0;
        internal static int InternalStaticField = 0;
        private static int PrivateStaticField = 0;
        public static int PublicStaticField = 0;

        public const int ConstField = 0;
        public readonly int ReadOnlyField = 0;
        internal int InternalField = 0;
        protected int ProtectedField = 0;
        private int PrivateField = 0;
        public int PublicField = 0;

        public bool BooleanField = false;
        public byte ByteField = 0;
        public sbyte SByteField = 0;
        public char CharField = 'A';
        public short Int16Field = 0;
        public int Int32Field = 0;
        public long Int64Field = 0;
        public ushort UInt16Field = 0;
        public uint UInt32Field = 0;
        public ulong UInt64Field = 0;
        public float SingleField = 0.0F;
        public double DoubleField = 0.0;
        public decimal DecimalField = 0;
        public string StringField;
        public ShortEnum EnumField;
        public FlagsEnum FlagsField;
        public object ObjectField;
        public ISpam SpamField;
    }
}
