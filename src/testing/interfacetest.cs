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
}
