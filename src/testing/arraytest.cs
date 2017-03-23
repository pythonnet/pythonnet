namespace Python.Test
{
    /// <summary>
    /// Supports units tests for indexer access.
    /// </summary>
    public class PublicArrayTest
    {
        public int[] items;

        public PublicArrayTest()
        {
            items = new int[5] { 0, 1, 2, 3, 4 };
        }
    }


    public class ProtectedArrayTest
    {
        protected int[] items;

        public ProtectedArrayTest()
        {
            items = new int[5] { 0, 1, 2, 3, 4 };
        }
    }


    public class InternalArrayTest
    {
        internal int[] items;

        public InternalArrayTest()
        {
            items = new int[5] { 0, 1, 2, 3, 4 };
        }
    }


    public class PrivateArrayTest
    {
        private int[] items;

        public PrivateArrayTest()
        {
            items = new int[5] { 0, 1, 2, 3, 4 };
        }
    }


    public class BooleanArrayTest
    {
        public bool[] items;

        public BooleanArrayTest()
        {
            items = new bool[5] { true, false, true, false, true };
        }
    }


    public class ByteArrayTest
    {
        public byte[] items;

        public ByteArrayTest()
        {
            items = new byte[5] { 0, 1, 2, 3, 4 };
        }
    }


    public class SByteArrayTest
    {
        public sbyte[] items;

        public SByteArrayTest()
        {
            items = new sbyte[5] { 0, 1, 2, 3, 4 };
        }
    }


    public class CharArrayTest
    {
        public char[] items;

        public CharArrayTest()
        {
            items = new char[5] { 'a', 'b', 'c', 'd', 'e' };
        }
    }


    public class Int16ArrayTest
    {
        public short[] items;

        public Int16ArrayTest()
        {
            items = new short[5] { 0, 1, 2, 3, 4 };
        }
    }


    public class Int32ArrayTest
    {
        public int[] items;

        public Int32ArrayTest()
        {
            items = new int[5] { 0, 1, 2, 3, 4 };
        }
    }


    public class Int64ArrayTest
    {
        public long[] items;

        public Int64ArrayTest()
        {
            items = new long[5] { 0, 1, 2, 3, 4 };
        }
    }


    public class UInt16ArrayTest
    {
        public ushort[] items;

        public UInt16ArrayTest()
        {
            items = new ushort[5] { 0, 1, 2, 3, 4 };
        }
    }


    public class UInt32ArrayTest
    {
        public uint[] items;

        public UInt32ArrayTest()
        {
            items = new uint[5] { 0, 1, 2, 3, 4 };
        }
    }


    public class UInt64ArrayTest
    {
        public ulong[] items;

        public UInt64ArrayTest()
        {
            items = new ulong[5] { 0, 1, 2, 3, 4 };
        }
    }


    public class SingleArrayTest
    {
        public float[] items;

        public SingleArrayTest()
        {
            items = new float[5] { 0.0F, 1.0F, 2.0F, 3.0F, 4.0F };
        }
    }


    public class DoubleArrayTest
    {
        public double[] items;

        public DoubleArrayTest()
        {
            items = new double[5] { 0.0, 1.0, 2.0, 3.0, 4.0 };
        }
    }


    public class DecimalArrayTest
    {
        public decimal[] items;

        public DecimalArrayTest()
        {
            items = new decimal[5] { 0, 1, 2, 3, 4 };
        }
    }


    public class StringArrayTest
    {
        public string[] items;

        public StringArrayTest()
        {
            items = new string[5] { "0", "1", "2", "3", "4" };
        }
    }

    public class EnumArrayTest
    {
        public ShortEnum[] items;

        public EnumArrayTest()
        {
            items = new ShortEnum[5]
            {
                ShortEnum.Zero,
                ShortEnum.One,
                ShortEnum.Two,
                ShortEnum.Three,
                ShortEnum.Four
            };
        }
    }


    public class NullArrayTest
    {
        public object[] items;
        public object[] empty;

        public NullArrayTest()
        {
            items = new object[5] { null, null, null, null, null };
            empty = new object[0] { };
        }
    }


    public class ObjectArrayTest
    {
        public object[] items;

        public ObjectArrayTest()
        {
            items = new object[5];
            items[0] = new Spam("0");
            items[1] = new Spam("1");
            items[2] = new Spam("2");
            items[3] = new Spam("3");
            items[4] = new Spam("4");
        }
    }


    public class InterfaceArrayTest
    {
        public ISpam[] items;

        public InterfaceArrayTest()
        {
            items = new ISpam[5];
            items[0] = new Spam("0");
            items[1] = new Spam("1");
            items[2] = new Spam("2");
            items[3] = new Spam("3");
            items[4] = new Spam("4");
        }
    }


    public class TypedArrayTest
    {
        public Spam[] items;

        public TypedArrayTest()
        {
            items = new Spam[5];
            items[0] = new Spam("0");
            items[1] = new Spam("1");
            items[2] = new Spam("2");
            items[3] = new Spam("3");
            items[4] = new Spam("4");
        }
    }


    public class MultiDimensionalArrayTest
    {
        public int[,] items;

        public MultiDimensionalArrayTest()
        {
            items = new int[5, 5]
            {
                { 0, 1, 2, 3, 4 },
                { 5, 6, 7, 8, 9 },
                { 10, 11, 12, 13, 14 },
                { 15, 16, 17, 18, 19 },
                { 20, 21, 22, 23, 24 }
            };
        }
    }


    public class ArrayConversionTest
    {
        public static Spam[] EchoRange(Spam[] items)
        {
            return items;
        }

        public static Spam[,] EchoRangeMD(Spam[,] items)
        {
            return items;
        }

        public static Spam[][] EchoRangeAA(Spam[][] items)
        {
            return items;
        }
    }
}
