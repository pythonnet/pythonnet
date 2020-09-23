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

    public interface IInterfaceResolutionTest
    {
        string TestMethod1();
        string TestMethod2();

        string TestProperty1 { get; }
        string TestProperty2 { get; }
    }

    public class InterfaceTest : ISayHello1, ISayHello2, IInterfaceResolutionTest
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

        public string TestMethod1()
        {
            return "TestMethod1";
        }

        string IInterfaceResolutionTest.TestMethod2()
        {
            return "TestMethod2";
        }

        public string TestProperty1 => "TestProperty1";

        string IInterfaceResolutionTest.TestProperty2 => "TestProperty2";

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
}
