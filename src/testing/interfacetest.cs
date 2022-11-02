namespace Python.Test
{
    /// <summary>
    /// Supports CLR class unit tests.
    /// </summary>
    public interface IPublicInterface
    {
    }

    internal interface IInternalInterface
    {
    }

    public interface ISayHello1
    {
        string SayHello();
    }

    public interface ISayHello2
    {
        string SayHello();
    }

    public class InterfaceTest : ISayHello1, ISayHello2
    {
        public InterfaceTest()
        {
        }

        public string HelloProperty
        {
            get { return "hello"; }
        }

        string ISayHello1.SayHello()
        {
            return "hello 1";
        }

        string ISayHello2.SayHello()
        {
            return "hello 2";
        }

        public ISayHello1 GetISayHello1()
        {
            return this;
        }

        public void GetISayHello2(out ISayHello2 hello2)
        {
            hello2 = this;
        }

        public ISayHello1 GetNoSayHello(out ISayHello2 hello2)
        {
            hello2 = null;
            return null;
        }

        public ISayHello1 [] GetISayHello1Array()
        {
            return new[] { this };
        }

        public interface IPublic
        {
        }

        protected interface IProtected
        {
        }

        internal interface IInternal
        {
        }

        private interface IPrivate
        {
        }
    }

    public interface IOutArg
    {
        string MyMethod_Out(string name, out int index);
    }

    public class OutArgCaller
    {
        public static int CallMyMethod_Out(IOutArg myInterface)
        {
            myInterface.MyMethod_Out("myclient", out int index);
            return index;
        }
    }

    public interface IGenericInterface<T>
    {
        public T Get(T x);
    }

    public class SpecificInterfaceUser
    {
        public SpecificInterfaceUser(IGenericInterface<int> some, int x)
        {
            some.Get(x);
        }
    }

    public class GenericInterfaceUser<T>
    {
        public GenericInterfaceUser(IGenericInterface<T> some, T x)
        {
            some.Get(x);
        }
    }
}
