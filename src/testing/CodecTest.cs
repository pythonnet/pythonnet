using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Python.Test
{
    public class ListMember
    {
        public ListMember(int value, string name)
        {
            Value = value;
            Name = name;
        }

        public int Value { get; set; }
        public string Name { get; set; }
    }

    public class ListConversionTester
    {
        public int GetLength(IEnumerable<object> o)
        {
            return o.Count();
        }
        public int GetLength(ICollection<object> o)
        {
            return o.Count;
        }
        public int GetLength(IList<object> o)
        {
            return o.Count;
        }
        public int GetLength2(IEnumerable<ListMember> o)
        {
            return o.Count();
        }
        public int GetLength2(ICollection<ListMember> o)
        {
            return o.Count;
        }
        public int GetLength2(IList<ListMember> o)
        {
            return o.Count;
        }
    }
}
