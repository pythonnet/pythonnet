using System.Collections;

namespace Python.Test
{
    /// <summary>
    /// Supports units tests for indexer access.
    /// </summary>
    public class IndexerBase
    {
        protected Hashtable t;

        protected IndexerBase()
        {
            t = new Hashtable();
        }

        protected string GetValue(object index)
        {
            if (index == null)
            {
                return null;
            }
            object value = t[index];
            if (value != null)
            {
                return (string)value;
            }
            return null;
        }
    }


    public class PublicIndexerTest : IndexerBase
    {
        public PublicIndexerTest() : base()
        {
        }

        public string this[int index]
        {
            get { return GetValue(index); }
            set { t[index] = value; }
        }
    }


    public class ProtectedIndexerTest : IndexerBase
    {
        public ProtectedIndexerTest() : base()
        {
        }

        protected string this[int index]
        {
            get { return GetValue(index); }
            set { t[index] = value; }
        }
    }


    public class InternalIndexerTest : IndexerBase
    {
        public InternalIndexerTest() : base()
        {
        }

        internal string this[int index]
        {
            get { return GetValue(index); }
            set { t[index] = value; }
        }
    }


    public class PrivateIndexerTest : IndexerBase
    {
        public PrivateIndexerTest() : base()
        {
        }

        private string this[int index]
        {
            get { return GetValue(index); }
            set { t[index] = value; }
        }
    }


    public class BooleanIndexerTest : IndexerBase
    {
        public BooleanIndexerTest() : base()
        {
        }

        public string this[bool index]
        {
            get { return GetValue(index); }
            set { t[index] = value; }
        }
    }


    public class ByteIndexerTest : IndexerBase
    {
        public ByteIndexerTest() : base()
        {
        }

        public string this[byte index]
        {
            get { return GetValue(index); }
            set { t[index] = value; }
        }
    }


    public class SByteIndexerTest : IndexerBase
    {
        public SByteIndexerTest() : base()
        {
        }

        public string this[sbyte index]
        {
            get { return GetValue(index); }
            set { t[index] = value; }
        }
    }


    public class CharIndexerTest : IndexerBase
    {
        public CharIndexerTest() : base()
        {
        }

        public string this[char index]
        {
            get { return GetValue(index); }
            set { t[index] = value; }
        }
    }


    public class Int16IndexerTest : IndexerBase
    {
        public Int16IndexerTest() : base()
        {
        }

        public string this[short index]
        {
            get { return GetValue(index); }
            set { t[index] = value; }
        }
    }


    public class Int32IndexerTest : IndexerBase
    {
        public Int32IndexerTest() : base()
        {
        }

        public string this[int index]
        {
            get { return GetValue(index); }
            set { t[index] = value; }
        }
    }


    public class Int64IndexerTest : IndexerBase
    {
        public Int64IndexerTest() : base()
        {
        }

        public string this[long index]
        {
            get { return GetValue(index); }
            set { t[index] = value; }
        }
    }


    public class UInt16IndexerTest : IndexerBase
    {
        public UInt16IndexerTest() : base()
        {
        }

        public string this[ushort index]
        {
            get { return GetValue(index); }
            set { t[index] = value; }
        }
    }


    public class UInt32IndexerTest : IndexerBase
    {
        public UInt32IndexerTest() : base()
        {
        }

        public string this[uint index]
        {
            get { return GetValue(index); }
            set { t[index] = value; }
        }
    }


    public class UInt64IndexerTest : IndexerBase
    {
        public UInt64IndexerTest() : base()
        {
        }

        public string this[ulong index]
        {
            get { return GetValue(index); }
            set { t[index] = value; }
        }
    }


    public class SingleIndexerTest : IndexerBase
    {
        public SingleIndexerTest() : base()
        {
        }

        public string this[float index]
        {
            get { return GetValue(index); }
            set { t[index] = value; }
        }
    }


    public class DoubleIndexerTest : IndexerBase
    {
        public DoubleIndexerTest() : base()
        {
        }

        public string this[double index]
        {
            get { return GetValue(index); }
            set { t[index] = value; }
        }
    }


    public class DecimalIndexerTest : IndexerBase
    {
        public DecimalIndexerTest() : base()
        {
        }

        public string this[decimal index]
        {
            get { return GetValue(index); }
            set { t[index] = value; }
        }
    }


    public class StringIndexerTest : IndexerBase
    {
        public StringIndexerTest() : base()
        {
        }

        public string this[string index]
        {
            get { return GetValue(index); }
            set { t[index] = value; }
        }
    }


    public class EnumIndexerTest : IndexerBase
    {
        public EnumIndexerTest() : base()
        {
        }

        public string this[ShortEnum index]
        {
            get { return GetValue(index); }
            set { t[index] = value; }
        }
    }


    public class ObjectIndexerTest : IndexerBase
    {
        public ObjectIndexerTest() : base()
        {
        }

        public string this[object index]
        {
            get { return GetValue(index); }
            set { t[index] = value; }
        }
    }


    public class InterfaceIndexerTest : IndexerBase
    {
        public InterfaceIndexerTest() : base()
        {
        }

        public string this[ISpam index]
        {
            get { return GetValue(index); }
            set { t[index] = value; }
        }
    }


    public class TypedIndexerTest : IndexerBase
    {
        public TypedIndexerTest() : base()
        {
        }

        public string this[Spam index]
        {
            get { return GetValue(index); }
            set { t[index] = value; }
        }
    }


    public class MultiArgIndexerTest : IndexerBase
    {
        public MultiArgIndexerTest() : base()
        {
        }

        public string this[int index1, int index2]
        {
            get
            {
                string key = index1.ToString() + index2.ToString();
                object value = t[key];
                if (value != null)
                {
                    return (string)value;
                }
                return null;
            }
            set
            {
                string key = index1.ToString() + index2.ToString();
                t[key] = value;
            }
        }
    }


    public class MultiTypeIndexerTest : IndexerBase
    {
        public MultiTypeIndexerTest() : base()
        {
        }

        public string this[int i1, string i2, ISpam i3]
        {
            get
            {
                string key = i1.ToString() + i2.ToString() + i3.GetHashCode().ToString();
                object value = t[key];
                if (value != null)
                {
                    return (string)value;
                }
                return null;
            }
            set
            {
                string key = i1.ToString() + i2.ToString() + i3.GetHashCode().ToString();
                t[key] = value;
            }
        }
    }

    public class MultiDefaultKeyIndexerTest : IndexerBase
    {
        public MultiDefaultKeyIndexerTest() : base()
        {
        }

        public string this[int i1, int i2 = 2]
        {
            get
            {
                string key = i1.ToString() + i2.ToString();
                return (string)t[key];
            }
            set
            {
                string key = i1.ToString() + i2.ToString();
                t[key] = value;
            }
        }
    }
}
