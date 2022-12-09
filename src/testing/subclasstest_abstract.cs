using System;

namespace Python.Test
{
    public class AbstractSubClassTestEventArgs
    {
        public int Value { get; }
        public AbstractSubClassTestEventArgs(int value) => Value = value;
    }

    public abstract class AbstractSubClassTest
    {
        public int BaseMethod(int value) => value;
        public abstract void PublicMethod(int value);
        public abstract int PublicProperty { get; set; }
        protected abstract void ProtectedMethod();
        public abstract event EventHandler<AbstractSubClassTestEventArgs> PublicEvent;
    }

    public static class AbstractSubClassTestConsumer
    {
        public static void TestPublicProperty(AbstractSubClassTest o, int value) => o.PublicProperty = value;
        public static void TestPublicMethod(AbstractSubClassTest o, int value) => o.PublicMethod(value);
    }
}