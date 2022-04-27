namespace Python.Test
{
    /// <summary>
    /// Supports CLR generics unit tests.
    /// </summary>
    public class GenericWrapper<T>
    {
        public T value;

        public GenericWrapper(T value)
        {
            this.value = value;
        }
    }

    public class GenericTypeDefinition<T, U>
    {
        public T value1;
        public U value2;

        public GenericTypeDefinition(T arg1, U arg2)
        {
            value1 = arg1;
            value2 = arg2;
        }
    }

    public class DerivedFromOpenGeneric<V, W> : GenericTypeDefinition<int, V>
    {
        public W value3;

        public DerivedFromOpenGeneric(int arg1, V arg2, W arg3) : base(arg1, arg2)
        {
            value3 = arg3;
        }
    }

    public class GenericTypeWithConstraint<T>
        where T: struct
    { }

    public class GenericNameTest1
    {
        public static int value = 0;
    }

    public class GenericNameTest1<T>
    {
        public static int value = 1;
    }

    public class GenericNameTest1<T, U>
    {
        public static int value = 2;
    }

    public class GenericNameTest2<T>
    {
        public static int value = 1;
    }

    public class GenericNameTest2<T, U>
    {
        public static int value = 2;
    }


    public class GenericMethodTest<T>
    {
        public GenericMethodTest()
        {
        }

        public int Overloaded()
        {
            return 1;
        }

        public T Overloaded(T arg)
        {
            return arg;
        }

        public Q Overloaded<Q>(Q arg)
        {
            return arg;
        }

        public U Overloaded<Q, U>(Q arg1, U arg2)
        {
            return arg2;
        }

        public string Overloaded<Q>(int arg1, int arg2, string arg3)
        {
            return arg3;
        }
    }

    public class GenericStaticMethodTest<T>
    {
        public GenericStaticMethodTest()
        {
        }

        public static int Overloaded()
        {
            return 1;
        }

        public static T Overloaded(T arg)
        {
            return arg;
        }

        public static Q Overloaded<Q>(Q arg)
        {
            return arg;
        }

        public static U Overloaded<Q, U>(Q arg1, U arg2)
        {
            return arg2;
        }

        public static string Overloaded<Q>(int arg1, int arg2, string arg3)
        {
            return arg3;
        }
    }

    public class GenericArrayConversionTest
    {
        public static T[] EchoRange<T>(T[] items)
        {
            return items;
        }
    }

    public abstract class GenericVirtualMethodTest
    {
        public virtual Q VirtMethod<Q>(Q arg1)
        {
            return arg1;
        }
    }
}
