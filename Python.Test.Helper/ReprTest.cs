using System;
using System.Text;

namespace Python.Test
{
    /// <summary>
    /// Supports repr unit tests.
    /// </summary>
    public class ReprTest
    {
        public class Point
        {
            public Point(double x, double y)
            {
                X = x;
                Y = y;
            }

            public double X { get; set; }
            public double Y { get; set; }

            public override string ToString()
            {
                return base.ToString() + ": X=" + X.ToString() + ", Y=" + Y.ToString();
            }

            public string __repr__()
            {
                return "Point(" + X.ToString() + "," + Y.ToString() + ")";
            }
        }

        public class Foo
        {
            public string __repr__()
            {
                return "I implement __repr__() but not ToString()!";
            }
        }

        public class Bar
        {
            public override string ToString()
            {
                return "I implement ToString() but not __repr__()!";
            }
        }

        public class BazBase
        {
            public override string ToString()
            {
                return "Base class implementing ToString()!";
            }
        }

        public class BazMiddle : BazBase
        {
            public override string ToString()
            {
                return "Middle class implementing ToString()!";
            }
        }

        //implements ToString via BazMiddle
        public class Baz : BazMiddle
        {

        }

        public class Quux
        {
            public string ToString(string format)
            {
                return "I implement ToString() with an argument!";
            }
        }

        public class QuuzBase
        {
            protected string __repr__()
            {
                return "I implement __repr__ but it isn't public!";
            }
        }

        public class Quuz : QuuzBase
        {

        }

        public class Corge
        {
            public string __repr__(int i)
            {
                return "__repr__ implemention with input parameter!";
            }
        }

        public class Grault
        {
            public int __repr__()
            {
                return "__repr__ implemention with wrong return type!".Length;
            }
        }
    }
}
