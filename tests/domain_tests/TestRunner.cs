// We can't refer to or use Python.Runtime here.
// We want it to be loaded only inside the subdomains
using System;
using Microsoft.CSharp;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;

namespace Python.DomainReloadTests
{
    /// <summary>
    /// This class provides an executable that can run domain reload tests.
    /// The setup is a bit complicated:
    /// 1. pytest runs test_*.py in this directory.
    /// 2. test_classname runs Python.DomainReloadTests.exe (this class) with an argument
    /// 3. This class at runtime creates a directory that has both C# and
    ///    python code, and compiles the C#.
    /// 4. This class then runs the C# code.
    /// 
    /// But there's a bit more indirection. This class compiles a DLL that
    /// contains code that will change.
    /// Then, the test case:
    /// * Compiles some code, loads it into a domain, runs python that refers to it.
    /// * Unload the domain, re-runs the domain to make sure domain reload happens correctly.
    /// * Compile a new piece of code, load it into a new domain, run a new piece of 
    ///   Python code to test the objects after they've been deleted or modified in C#.
    /// * Unload the domain. Reload the domain, run the same python again.
    /// 
    /// This class gets built into an executable which takes one argument:
    /// which test case to run. That's because pytest assumes we'll run
    /// everything in one process, but we really want a clean process on each
    /// test case to test the init/reload/teardown parts of the domain reload.
    /// 
    /// ### Debugging tips: ###
    /// * Running pytest with the `-s` argument prevents stdout capture by pytest
    /// * Add a sleep into the python test case before the crash/failure, then while 
    ///   sleeping, attach the debugger to the Python.TestDomainReload.exe process.
    /// </summary>
    /// 
    class TestRunner
    {
        const string TestAssemblyName = "DomainTests";

        class TestCase
        {
            /// <summary>
            /// The key to pass as an argument to choose this test.
            /// </summary>
            public string Name;

            public override string ToString() => Name;

            /// <summary>
            /// The C# code to run in the first domain.
            /// </summary>
            public string DotNetBefore;

            /// <summary>
            /// The C# code to run in the second domain.
            /// </summary>
            public string DotNetAfter;

            /// <summary>
            /// The Python code to run as a module that imports the C#.
            /// It should have two functions: before_reload() and after_reload(). 
            /// Before will be called twice when DotNetBefore is loaded; 
            /// after will also be called twice when DotNetAfter is loaded.
            /// To make the test fail, have those functions raise exceptions.
            ///
            /// Make sure there's no leading spaces since Python cares.
            /// </summary>
            public string PythonCode;
        }

        static TestCase[] Cases = new TestCase[]
        {
            new TestCase
            {
                Name = "class_rename",
                DotNetBefore = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Before { }
                    }",
                DotNetAfter = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class After { }
                    }",
                PythonCode = @"
import clr
import sys
clr.AddReference('DomainTests')
import TestNamespace

def before_reload():
    sys.my_cls = TestNamespace.Before


def after_reload():
    assert sys.my_cls is not None
    try:
        foo = TestNamespace.Before
    except AttributeError:
        print('Caught expected exception')
    else:
        raise AssertionError('Failed to throw exception')
                    ",
            },

            new TestCase 
            {
                Name = "static_member_rename",
                DotNetBefore = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls { public static int Before() { return 5; } }
                    }",
                DotNetAfter = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls { public static int After() { return 10; } }
                    }",
                PythonCode = @"
import clr
import sys
clr.AddReference('DomainTests')
import TestNamespace

def before_reload():
    if not hasattr(sys, 'my_cls'):
        sys.my_cls = TestNamespace.Cls
        sys.my_fn = TestNamespace.Cls.Before
    assert 5 == sys.my_fn()
    assert 5 == TestNamespace.Cls.Before()

def after_reload():

    # We should have reloaded the class so we can access the new function.
    assert 10 == sys.my_cls.After()
    assert True is True

    try:
        # We should have reloaded the class. The old function still exists, but is now invalid.
        sys.my_cls.Before()
    except AttributeError:
        print('Caught expected TypeError')
    else:
        raise AssertionError('Failed to throw exception: expected TypeError calling class member that no longer exists')

    assert sys.my_fn is not None

    try:
        # Unbound functions still exist. They will error out when called though.
        sys.my_fn()
    except TypeError:
        print('Caught expected TypeError')
    else:
        raise AssertionError('Failed to throw exception: expected TypeError calling unbound .NET function that no longer exists')
                    ",
            },


            new TestCase 
            {
                Name = "member_rename",
                DotNetBefore = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls { public int Before() { return 5; } }
                    }",
                DotNetAfter = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls { public int After() { return 10; } }
                    }",
                PythonCode = @"
import clr
import sys
clr.AddReference('DomainTests')
import TestNamespace

def before_reload():
    sys.my_cls = TestNamespace.Cls()
    sys.my_fn = TestNamespace.Cls().Before
    sys.my_fn()
    TestNamespace.Cls().Before()

def after_reload():

    # We should have reloaded the class so we can access the new function.
    assert 10 == sys.my_cls.After()
    assert True is True

    try:
        # We should have reloaded the class. The old function still exists, but is now invalid.
        sys.my_cls.Before()
    except AttributeError:
        print('Caught expected TypeError')
    else:
        raise AssertionError('Failed to throw exception: expected TypeError calling class member that no longer exists')
    
    assert sys.my_fn is not None

    try:
        # Unbound functions still exist. They will error out when called though.
        sys.my_fn()
    except TypeError:
        print('Caught expected TypeError')
    else:
        raise AssertionError('Failed to throw exception: expected TypeError calling unbound .NET function that no longer exists')
                    ",
            },

            new TestCase
            {
                Name = "field_rename",
                DotNetBefore = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls 
                        {
                            static public int Before = 2;
                        }
                    }",
                DotNetAfter = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls
                        {
                            static public int After = 4;
                        }
                    }",
                PythonCode = @"
import clr
import sys
clr.AddReference('DomainTests')
from TestNamespace import Cls

def before_reload():
    sys.my_int = Cls.Before

def after_reload():
    print(sys.my_int)
    try:
        assert 2 == Cls.Before
    except AttributeError:
        print('Caught expected exception')
    else:
        raise AssertionError('Failed to throw exception')
",
            },
            new TestCase
            {
                Name = "property_rename",
                DotNetBefore = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls 
                        {
                            static public int Before { get { return 2; } }
                        }
                    }",
                DotNetAfter = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls
                        {
                            static public int After { get { return 4; } }
                        }
                    }",
                PythonCode = @"
import clr
import sys
clr.AddReference('DomainTests')
from TestNamespace import Cls

def before_reload():
    sys.my_int = Cls.Before

def after_reload():
    print(sys.my_int)
    try:
        assert 2 == Cls.Before
    except AttributeError:
        print('Caught expected exception')
    else:
        raise AssertionError('Failed to throw exception')
",
            },

            new TestCase
            {
                Name = "event_rename",
                DotNetBefore = @"
                    using System;
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls 
                        {
                            public static event Action Before;
                            public static void Call()
                            {
                                if (Before != null) Before();
                            }
                        }
                    }",
                DotNetAfter = @"
                    using System;
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls
                        {
                            public static event Action After;
                            public static void Call()
                            {
                                if (After != null) After();
                            }
                        }
                    }",
                PythonCode = @"
import clr
import sys
clr.AddReference('DomainTests')
from TestNamespace import Cls

called = False
before_reload_called = False
after_reload_called = False

def callback_function():
    global called
    called = True

def before_reload():
    global called, before_reload_called
    called = False
    Cls.Before += callback_function
    Cls.Call()
    assert called is True
    before_reload_called = True

def after_reload():
    global called, after_reload_called, before_reload_called

    assert before_reload_called is True
    if not after_reload_called:
        assert called is True
    after_reload_called = True

    called = False
    Cls.Call()
    assert called is False
",
            },

            new TestCase
            {
                Name = "namespace_rename",
                DotNetBefore = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls 
                        {
                            public int Foo;
                            public Cls(int i)
                            {
                                Foo = i;
                            }
                        }
                    }",
                DotNetAfter = @"
                    namespace NewTestNamespace
                    {
                        [System.Serializable]
                        public class Cls 
                        {
                            public int Foo;
                            public Cls(int i)
                            {
                                Foo = i;
                            }
                        }
                    }",
                PythonCode = @"
import clr
import sys
clr.AddReference('DomainTests')
import TestNamespace

def before_reload():
    sys.my_cls = TestNamespace.Cls
    sys.my_inst = TestNamespace.Cls(1)

def after_reload():
     try:
        TestNamespace.Cls(2)
     except AttributeError:
         print('Caught expected exception')
     else:
         raise AssertionError('Failed to throw exception')
                    ",
            },

            new TestCase
            {
                Name = "field_visibility_change",
                DotNetBefore = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls
                        {
                            public static int Foo = 1;
                            public static int Field = 2;
                        }
                    }",
                DotNetAfter = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls
                        {
                            public static int Foo = 1;
                            private static int Field = 2;
                        }
                    }",
                PythonCode = @"
import clr
import sys
clr.AddReference('DomainTests')
from TestNamespace import Cls

def before_reload():
    assert 2 == Cls.Field
    assert 1 == Cls.Foo

def after_reload():
    assert 1 == Cls.Foo
    try:
        assert 1 == Cls.Field
    except AttributeError:
        print('Caught expected exception')
    else:
        raise AssertionError('Failed to throw exception')
                    ",
            },

            new TestCase
            {
                Name = "method_visibility_change",
                DotNetBefore = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls
                        {
                            public static int Foo() { return 1; }
                            public static int Function() { return 2; }
                        }
                    }",
                DotNetAfter = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls
                        {
                            public static int Foo() { return 1; }
                            private static int Function() { return 2; }
                        }
                    }",
                PythonCode = @"
import clr
import sys
clr.AddReference('DomainTests')
from TestNamespace import Cls

def before_reload():
    sys.my_func = Cls.Function
    assert 1 == Cls.Foo()
    assert 2 == Cls.Function()

def after_reload():
    assert 1 == Cls.Foo()
    try:
        assert 2 == Cls.Function()
    except AttributeError:
        print('Caught expected exception')
    else:
        raise AssertionError('Failed to throw exception')

    try:
        assert 2 == sys.my_func()
    except TypeError:
        print('Caught expected exception')
    else:
        raise AssertionError('Failed to throw exception')
                    ",
            },

            new TestCase
            {
                Name = "property_visibility_change",
                DotNetBefore = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls
                        {
                            public static int Foo { get { return 1; } }
                            public static int Property { get { return 2; } }
                        }
                    }",
                DotNetAfter = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls
                        {
                            public static int Foo { get { return 1; } }
                            private static int Property { get { return 2; } }
                        }
                    }",
                PythonCode = @"
import clr
import sys
clr.AddReference('DomainTests')
from TestNamespace import Cls

def before_reload():
    assert 1 == Cls.Foo
    assert 2 == Cls.Property

def after_reload():
    assert 1 == Cls.Foo
    try:
        assert 2 == Cls.Property
    except AttributeError:
        print('Caught expected exception')
    else:
        raise AssertionError('Failed to throw exception')
                    ",
            },

        new TestCase
            {
                Name = "class_visibility_change",
                DotNetBefore = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class PublicClass { }
                        
                        [System.Serializable]
                        public class Cls { }
                    }",
                DotNetAfter = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        internal class Cls { }
                    }",
                PythonCode = @"
import clr
import sys
clr.AddReference('DomainTests')
import TestNamespace 

def before_reload():
    sys.my_cls = TestNamespace.Cls

def after_reload():
    sys.my_cls()
    
    try:
        TestNamespace.Cls()
    except AttributeError:
        print('Caught expected exception')
    else:
        raise AssertionError('Failed to throw exception')
                    ",
            },

            new TestCase
            {
                Name = "method_parameters_change",
                DotNetBefore = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls
                        {
                            public static void MyFunction(int a)
                            {
                                System.Console.WriteLine(string.Format(""MyFunction says: {0}"", a));
                            }
                        }
                    }",
                DotNetAfter = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls
                        {
                            public static void MyFunction(string a)
                            {
                                System.Console.WriteLine(string.Format(""MyFunction says: {0}"", a));
                            }
                        }
                    }",
                PythonCode = @"
import clr
import sys
clr.AddReference('DomainTests')
from TestNamespace import Cls

def before_reload():
    sys.my_cls = Cls
    sys.my_func = Cls.MyFunction
    sys.my_cls.MyFunction(1)
    sys.my_func(2)

def after_reload():
    try:
        sys.my_cls.MyFunction(1)
    except TypeError:
        print('Caught expected exception')
    else:
        raise AssertionError('Failed to throw exception')

    try:
        sys.my_func(2)
    except TypeError:
        print('Caught expected exception')
    else:
        raise AssertionError('Failed to throw exception')

    # Calling the function from the class passes
    sys.my_cls.MyFunction('test')
    
    try:
        # calling the callable directly fails
        sys.my_func('test')
    except TypeError:
        print('Caught expected exception')
    else:
        raise AssertionError('Failed to throw exception')
    
    Cls.MyFunction('another test')
    
                    ",
            },

            new TestCase
            {
                Name = "method_return_type_change",
                DotNetBefore = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls
                        {
                            public static int MyFunction()
                            {
                                return 2;
                            }
                        }
                    }",
                DotNetAfter = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls
                        {
                            public static string MyFunction()
                            {
                                return ""22"";
                            }
                        }
                    }",
                PythonCode = @"
import clr
import sys
clr.AddReference('DomainTests')
from TestNamespace import Cls

def before_reload():
    sys.my_cls = Cls
    sys.my_func = Cls.MyFunction
    assert 2 == sys.my_cls.MyFunction()
    assert 2 == sys.my_func()

def after_reload():
    assert '22' == sys.my_cls.MyFunction()
    assert '22' == sys.my_func()
    assert '22' == Cls.MyFunction()
                    ",
            },

            new TestCase
            {
                Name = "field_type_change",
                DotNetBefore = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls 
                        {
                            static public int Field = 2;
                        }
                    }",
                DotNetAfter = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls
                        {
                            static public string Field = ""22"";
                        }
                    }",
                PythonCode = @"
import clr
import sys
clr.AddReference('DomainTests')
from TestNamespace import Cls

def before_reload():
    sys.my_cls = Cls
    assert 2 == sys.my_cls.Field

def after_reload():
    assert '22' == Cls.Field
    assert '22' == sys.my_cls.Field
                    ",
            },

            new TestCase
            {
                Name = "construct_removed_class",
                DotNetBefore = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Before { }
                    }",
                DotNetAfter = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class After { }
                    }",
                PythonCode = @"
import clr
import sys
clr.AddReference('DomainTests')
import TestNamespace

def before_reload():
    sys.my_cls = TestNamespace.Before

def after_reload():
    try:
        bar = sys.my_cls()
    except TypeError:
        print('Caught expected exception')
    else:
        raise AssertionError('Failed to throw exception')
                    ",
            },

            new TestCase
            {
                Name = "out_to_ref_param",
                DotNetBefore = @"
                    namespace TestNamespace
                    {

                        [System.Serializable]
                        public class Data
                        {
                            public int num = -1;
                        }

                        [System.Serializable]
                        public class Cls 
                        {
                            public static void MyFn (out Data a)
                            {
                                a = new Data();
                                a.num = 9001;
                            }
                        }
                    }",
                DotNetAfter = @"
                    namespace TestNamespace
                    {

                        [System.Serializable]
                        public class Data
                        {
                            public int num = -1;
                        }

                        [System.Serializable]
                        public class Cls
                        {
                            public static void MyFn (ref Data a)
                            {
                                a.num = 7;
                            }
                        }
                    }",
                PythonCode = @"
import clr
import sys
clr.AddReference('DomainTests')
import TestNamespace
import System

def before_reload():

    foo = TestNamespace.Data()
    bar = TestNamespace.Cls.MyFn(foo)
    assert bar.num == 9001
    # foo shouldn't have changed.
    assert foo.num == -1


def after_reload():

    try:
        # Now that the function takes a ref type, we must pass a valid object.
        bar = TestNamespace.Cls.MyFn(None)
    except System.NullReferenceException as e:
        print('caught expected exception')
    else:
        raise AssertionError('failed to raise')

    foo = TestNamespace.Data()
    bar = TestNamespace.Cls.MyFn(foo)
    # foo should have changed
    assert foo.num == 7
    assert bar.num == 7
    # Pythonnet also returns a new object with `ref`-qualified parameters
    assert foo is not bar
                    ",
            },

            new TestCase
            {
                Name = "ref_to_out_param",
                DotNetBefore = @"
                    namespace TestNamespace
                    {

                        [System.Serializable]
                        public class Data
                        {
                            public int num = -1;
                        }

                        [System.Serializable]
                        public class Cls 
                        {
                            public static void MyFn (ref Data a)
                            {
                                a.num = 7;
                            }
                        }
                    }",
                DotNetAfter = @"
                    namespace TestNamespace
                    {

                        [System.Serializable]
                        public class Data
                        {
                            public int num = -1;
                        }

                        [System.Serializable]
                        public class Cls
                        {
                            public static void MyFn (out Data a)
                            {
                                a = new Data();
                                a.num = 9001;
                            }
                        }
                    }",
                PythonCode = @"
import clr
import sys
clr.AddReference('DomainTests')
import TestNamespace
import System

def before_reload():

    foo = TestNamespace.Data()
    bar = TestNamespace.Cls.MyFn(foo)
    # foo should have changed
    assert foo.num == 7
    assert bar.num == 7


def after_reload():

    foo = TestNamespace.Data()
    bar = TestNamespace.Cls.MyFn(foo)
    assert bar.num == 9001
    # foo shouldn't have changed.
    assert foo.num == -1
    # this should work too
    baz = TestNamespace.Cls.MyFn(None)
    assert baz.num == 9001
                    ",
            },
            new TestCase
            {
                Name = "ref_to_in_param",
                DotNetBefore = @"
                    namespace TestNamespace
                    {

                        [System.Serializable]
                        public class Data
                        {
                            public int num = -1;
                        }

                        [System.Serializable]
                        public class Cls 
                        {
                            public static void MyFn (ref Data a)
                            {
                                a.num = 7;
                                System.Console.Write(""Method with ref parameter: "");
                                System.Console.WriteLine(a.num);
                            }
                        }
                    }",
                DotNetAfter = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Data
                        {
                            public int num = -1;
                        }

                        [System.Serializable]
                        public class Cls
                        {
                            public static void MyFn (Data a)
                            {
                                System.Console.Write(""Method with in parameter: "");
                                System.Console.WriteLine(a.num);
                            }
                        }
                    }",
                PythonCode = @"
import clr
import sys
clr.AddReference('DomainTests')
import TestNamespace
import System

def before_reload():

    foo = TestNamespace.Data()
    bar = TestNamespace.Cls.MyFn(foo)
    # foo should have changed
    assert foo.num == 7
    assert bar.num == 7

def after_reload():

    foo = TestNamespace.Data()
    TestNamespace.Cls.MyFn(foo)
    # foo should not have changed
    assert foo.num == TestNamespace.Data().num
    
                    ",
            },
            new TestCase
            {
                Name = "in_to_ref_param",
                DotNetBefore = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Data
                        {
                            public int num = -1;
                        }

                        [System.Serializable]
                        public class Cls
                        {
                            public static void MyFn (Data a)
                            {
                                System.Console.Write(""Method with in parameter: "");
                                System.Console.WriteLine(a.num);
                            }
                        }
                    }",
                DotNetAfter = @"
                    namespace TestNamespace
                    {

                        [System.Serializable]
                        public class Data
                        {
                            public int num = -1;
                        }

                        [System.Serializable]
                        public class Cls
                        {
                            public static void MyFn (ref Data a)
                            {
                                a.num = 7;
                                System.Console.Write(""Method with ref parameter: "");
                                System.Console.WriteLine(a.num);
                            }
                        }
                    }",
                PythonCode = @"
import clr
import sys
clr.AddReference('DomainTests')
import TestNamespace
import System

def before_reload():

    foo = TestNamespace.Data()
    TestNamespace.Cls.MyFn(foo)
    # foo should not have changed
    assert foo.num == TestNamespace.Data().num

def after_reload():

    foo = TestNamespace.Data()
    bar = TestNamespace.Cls.MyFn(foo)
    # foo should have changed
    assert foo.num == 7
    assert bar.num == 7
                    ",
            },
            new TestCase
            {
                Name = "nested_type",
                DotNetBefore = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class WithNestedType
                        {
                            [System.Serializable]
                            public class Inner
                            {
                                public static int Value = -1;
                            }
                        }
                    }",
                DotNetAfter = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class WithNestedType
                        {
                            [System.Serializable]
                            public class Inner
                            {
                                public static int Value = -1;
                            }
                        }
                    }",
                PythonCode = @"
import clr
import sys
clr.AddReference('DomainTests')
import TestNamespace

def before_reload():

    sys.my_obj = TestNamespace.WithNestedType

def after_reload():

    assert sys.my_obj is not None
    foo = sys.my_obj.Inner()
    print(foo)
    
                    ",
            },
            new TestCase
            {
                // The C# code for this test doesn't matter; we're testing
                // that the import hook behaves properly after a domain reload
                Name = "import_after_reload",
                DotNetBefore = "",
                DotNetAfter = "",
                PythonCode = @"
import sys

def before_reload():
    import clr
    import System


def after_reload():
    assert 'System' in sys.modules
    assert 'clr' in sys.modules
    import clr
    import System
    
                    ",
            },
        };

        /// <summary>
        /// The runner's code. Runs the python code
        /// This is a template for string.Format
        /// Arg 0 is the no-arg python function to run, before or after.
        /// </summary>
        const string CaseRunnerTemplate = @"
using System;
using System.IO;
using Python.Runtime;
namespace CaseRunner
{{
    class CaseRunner
    {{
        public static int Main()
        {{
            try
            {{
                PythonEngine.Initialize();
                using (Py.GIL())
                {{
                    var temp = AppDomain.CurrentDomain.BaseDirectory;
                    dynamic sys = Py.Import(""sys"");
                    sys.path.append(new PyString(temp));
                    dynamic test_mod = Py.Import(""domain_test_module.mod"");
                    test_mod.{0}_reload();
                }}
                PythonEngine.Shutdown();
            }}
            catch (PythonException pe)
            {{
                throw new ArgumentException(message:pe.Message+""    ""+pe.StackTrace);
            }}
            catch (Exception e)
            {{
                Console.Error.WriteLine(e.StackTrace);
                throw;
            }}
            return 0;
        }}
    }}
}}
";
        readonly static string PythonDllLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Python.Runtime.dll");

        static string TestPath = null;

        public static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                foreach (var testCase in Cases)
                {
                    Run(testCase);
                    Console.WriteLine();
                }
            }
            else
            {
                string testName = args[0];
                Console.WriteLine($"-- Looking for domain reload test case {testName}");
                var testCase = int.TryParse(testName, out var index) ? Cases[index] : Cases.First(c => c.Name == testName);
                Run(testCase);
            }

            return 0;
        }

        static void Run(TestCase testCase)
        {
            Console.WriteLine($"-- Running domain reload test case: {testCase.Name}");

            SetupTestFolder(testCase.Name);

            CreatePythonModule(testCase);
            {
                var runnerAssembly = CreateCaseRunnerAssembly(verb:"before");
                CreateTestClassAssembly(testCase.DotNetBefore);
                {
                    var runnerDomain = CreateDomain("case runner before");
                    RunAndUnload(runnerDomain, runnerAssembly);
                }
                {
                    var runnerDomain = CreateDomain("case runner before (again)");
                    RunAndUnload(runnerDomain, runnerAssembly);
                }
            }

            {
                var runnerAssembly = CreateCaseRunnerAssembly(verb:"after");
                CreateTestClassAssembly(testCase.DotNetAfter);

                // Do it twice for good measure
                {
                    var runnerDomain = CreateDomain("case runner after");
                    RunAndUnload(runnerDomain, runnerAssembly);
                }
                {
                    var runnerDomain = CreateDomain("case runner after (again)");
                    RunAndUnload(runnerDomain, runnerAssembly);
                }
            }

            // Don't delete unconditionally. It's sometimes useful to leave the
            // folder behind to debug failing tests.
            TeardownTestFolder();

            Console.WriteLine($"-- PASSED: {testCase.Name}");
        }

        static void SetupTestFolder(string testCaseName)
        {
            var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            TestPath = Path.Combine(Path.GetTempPath(), $"Python.TestRunner.{testCaseName}-{pid}");
            if (Directory.Exists(TestPath))
            {
                Directory.Delete(TestPath, recursive: true);
            }
            Directory.CreateDirectory(TestPath);
            Console.WriteLine($"Using directory: {TestPath}");
            File.Copy(PythonDllLocation, Path.Combine(TestPath, "Python.Runtime.dll"));
        }

        static void TeardownTestFolder()
        {
            if (Directory.Exists(TestPath))
            {
                Directory.Delete(TestPath, recursive: true);
            }
        }

        static void RunAndUnload(AppDomain domain, string assemblyPath)
        {
            // Somehow the stack traces during execution sometimes have the wrong line numbers.
            // Add some info for when debugging is required.
            Console.WriteLine($"-- Running domain {domain.FriendlyName}");
            domain.ExecuteAssembly(assemblyPath);
            AppDomain.Unload(domain);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        static string CreateTestClassAssembly(string code)
        {
            return CreateAssembly(TestAssemblyName + ".dll", code, exe: false);
        }

        static string CreateCaseRunnerAssembly(string verb)
        {
            var code = string.Format(CaseRunnerTemplate, verb);
            var name = "TestCaseRunner.exe";

            return CreateAssembly(name, code, exe: true);
        }
        static string CreateAssembly(string name, string code, bool exe = false)
        {
            // Never return or hold the Assembly instance. This will cause
            // the assembly to be loaded into the current domain and this
            // interferes with the tests. The Domain can execute fine from a 
            // path, so let's return that.
            CSharpCodeProvider provider = new CSharpCodeProvider();
            CompilerParameters parameters = new CompilerParameters();
            parameters.GenerateExecutable = exe;
            var assemblyName = name;
            var assemblyFullPath = Path.Combine(TestPath, assemblyName);
            parameters.OutputAssembly = assemblyFullPath;
            parameters.ReferencedAssemblies.Add("System.dll");
            parameters.ReferencedAssemblies.Add("System.Core.dll");
            parameters.ReferencedAssemblies.Add("Microsoft.CSharp.dll");
            var netstandard = "netstandard.dll";
            if (Type.GetType("Mono.Runtime") != null)
            {
                netstandard = "Facades/" + netstandard;
            }
            parameters.ReferencedAssemblies.Add(netstandard);
            parameters.ReferencedAssemblies.Add(PythonDllLocation);
            // Write code to file so it can debugged.
            var sourcePath = Path.Combine(TestPath, name+"_source.cs");
            using(var file = new StreamWriter(sourcePath))
            {
                file.Write(code);
            }
            CompilerResults results = provider.CompileAssemblyFromFile(parameters, sourcePath);
            if (results.NativeCompilerReturnValue != 0)
            {
                var stderr = System.Console.Error;
                stderr.WriteLine($"Error in {name} compiling:\n{code}");
                foreach (var error in results.Errors)
                {
                    stderr.WriteLine(error);
                }
                throw new ArgumentException("Error compiling code");
            }

            return assemblyFullPath;
        }

        static AppDomain CreateDomain(string name)
        {
            // Create the domain. Make sure to set PrivateBinPath to a relative
            // path from the CWD (namely, 'bin').
            // See https://stackoverflow.com/questions/24760543/createinstanceandunwrap-in-another-domain
            var currentDomain = AppDomain.CurrentDomain;
            var domainsetup = new AppDomainSetup()
            {
                ApplicationBase = TestPath,
                ConfigurationFile = currentDomain.SetupInformation.ConfigurationFile,
                LoaderOptimization = LoaderOptimization.SingleDomain,
                PrivateBinPath = "."
            };
            var domain = AppDomain.CreateDomain(
                    $"My Domain {name}",
                    currentDomain.Evidence,
                domainsetup);

            return domain;
        }

        static string CreatePythonModule(TestCase testCase)
        {
            var modulePath = Path.Combine(TestPath, "domain_test_module");
            if (Directory.Exists(modulePath))
            {
                Directory.Delete(modulePath, recursive: true);
            }
            Directory.CreateDirectory(modulePath);

            File.Create(Path.Combine(modulePath, "__init__.py")).Close(); //Create and don't forget to close!
            using (var writer = File.CreateText(Path.Combine(modulePath, "mod.py")))
            {
                writer.Write(testCase.PythonCode);
            }

            return null;
        }
    }
}
