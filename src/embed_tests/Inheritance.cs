using System;
using System.Collections.Generic;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class Inheritance
    {
        ExtraBaseTypeProvider ExtraBaseTypeProvider;
        NoEffectBaseTypeProvider NoEffectBaseTypeProvider;
        

        [OneTimeSetUp]
        public void SetUp()
        {
            using var locals = new PyDict();
            PythonEngine.Exec(InheritanceTestBaseClassWrapper.ClassSourceCode, locals: locals);

            NoEffectBaseTypeProvider = new NoEffectBaseTypeProvider();
            ExtraBaseTypeProvider = new ExtraBaseTypeProvider(new PyType(locals[InheritanceTestBaseClassWrapper.ClassName]));

            var baseTypeProviders = PythonEngine.InteropConfiguration.PythonBaseTypeProviders;
            baseTypeProviders.Add(ExtraBaseTypeProvider);
            baseTypeProviders.Add(NoEffectBaseTypeProvider);
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            var baseTypeProviders = PythonEngine.InteropConfiguration.PythonBaseTypeProviders;
            baseTypeProviders.Remove(NoEffectBaseTypeProvider);
            baseTypeProviders.Remove(ExtraBaseTypeProvider);
            ExtraBaseTypeProvider.Dispose();
        }

        [Test]
        public void ExtraBase_PassesInstanceCheck()
        {
            var inherited = new Inherited();
            bool properlyInherited = PyIsInstance(inherited, ExtraBaseTypeProvider.ExtraBase);
            Assert.That(properlyInherited, Is.True);
        }

        static dynamic PyIsInstance => PythonEngine.Eval("isinstance");

        [Test]
        public void InheritingWithExtraBase_CreatesNewClass()
        {
            PyObject a = ExtraBaseTypeProvider.ExtraBase;
            var inherited = new Inherited();
            PyObject inheritedClass = inherited.ToPython().GetAttr("__class__");
            Assert.That(PythonReferenceComparer.Instance.Equals(a, inheritedClass), Is.False);
        }

        [Test]
        public void InheritedFromInheritedClassIsSelf()
        {
            using var scope = Py.CreateScope();
            scope.Exec($"from {typeof(Inherited).Namespace} import {nameof(Inherited)}");
            scope.Exec($"class B({nameof(Inherited)}): pass");
            PyObject b = scope.Eval("B");
            PyObject bInstance = b.Invoke();
            PyObject bInstanceClass = bInstance.GetAttr("__class__");
            Assert.That(PythonReferenceComparer.Instance.Equals(b, bInstanceClass), Is.True);
        }

        // https://github.com/pythonnet/pythonnet/issues/1420
        [Test]
        public void CallBaseMethodFromContainerInNestedClass()
        {
            using var nested = new ContainerClass.InnerClass().ToPython();
            nested.InvokeMethod(nameof(ContainerClass.BaseMethod));
        }

        [Test]
        public void Grandchild_PassesExtraBaseInstanceCheck()
        {
            using var scope = Py.CreateScope();
            scope.Exec($"from {typeof(Inherited).Namespace} import {nameof(Inherited)}");
            scope.Exec($"class B({nameof(Inherited)}): pass");
            PyObject b = scope.Eval("B");
            PyObject bInst = b.Invoke();
            bool properlyInherited = PyIsInstance(bInst, ExtraBaseTypeProvider.ExtraBase);
            Assert.That(properlyInherited, Is.True);
        }

        [Test]
        public void CallInheritedClrMethod_WithExtraPythonBase()
        {
            var instance = new Inherited().ToPython();
            string result = instance.InvokeMethod(nameof(PythonWrapperBase.WrapperBaseMethod)).As<string>();
            Assert.That(nameof(PythonWrapperBase.WrapperBaseMethod), Is.EqualTo(result));
        }

        [Test]
        public void CallExtraBaseMethod()
        {
            var instance = new Inherited();
            using var scope = Py.CreateScope();
            scope.Set(nameof(instance), instance);
            int actual = instance.ToPython().InvokeMethod("callVirt").As<int>();
            Assert.That(actual, Is.EqualTo(Inherited.OverridenVirtValue));
        }

        [Test]
        public void SetAdHocAttributes_WhenExtraBasePresent()
        {
            var instance = new Inherited();
            using var scope = Py.CreateScope();
            scope.Set(nameof(instance), instance);
            scope.Exec($"super({nameof(instance)}.__class__, {nameof(instance)}).set_x_to_42()");
            int actual = scope.Eval<int>($"{nameof(instance)}.{nameof(Inherited.XProp)}");
            Assert.That(actual, Is.EqualTo(Inherited.X));
        }

        // https://github.com/pythonnet/pythonnet/issues/1476
        [Test]
        public void BaseClearIsCalled()
        {
            using var scope = Py.CreateScope();
            scope.Set("exn", new Exception("42"));
            var msg = scope.Eval("exn.args[0]");
            Assert.That(msg.Refcount, Is.EqualTo(2));
            scope.Set("exn", null);
            Assert.That(msg.Refcount, Is.EqualTo(1));
        }

        // https://github.com/pythonnet/pythonnet/issues/1455
        [Test]
        public void PropertyAccessorOverridden()
        {
            using var derived = new PropertyAccessorDerived().ToPython();
            derived.SetAttr(nameof(PropertyAccessorDerived.VirtualProp), "hi".ToPython());
            Assert.That(derived.GetAttr(nameof(PropertyAccessorDerived.VirtualProp)).As<string>(), Is.EqualTo("HI"));
        }
    }

    class ExtraBaseTypeProvider(PyType ExtraBase) : IPythonBaseTypeProvider, IDisposable
    {
        public PyType ExtraBase { get; } = ExtraBase;

        public void Dispose()
        {
            ExtraBase.Dispose();
        }

        public IEnumerable<PyType> GetBaseTypes(Type type, IList<PyType> existingBases)
        {
            if (type == typeof(InheritanceTestBaseClassWrapper))
            {
                return [PyType.Get(type.BaseType), ExtraBase];
            }
            return existingBases;
        }
    }

    class NoEffectBaseTypeProvider : IPythonBaseTypeProvider
    {
        public IEnumerable<PyType> GetBaseTypes(Type type, IList<PyType> existingBases)
            => existingBases;
    }

    public class PythonWrapperBase
    {
        public string WrapperBaseMethod() => nameof(WrapperBaseMethod);
    }

    public class InheritanceTestBaseClassWrapper : PythonWrapperBase
    {
        public const string ClassName = "InheritanceTestBaseClass";
        public const string ClassSourceCode = "class " + ClassName +
@":
  def virt(self):
    return 42
  def set_x_to_42(self):
    self.XProp = 42
  def callVirt(self):
    return self.virt()
  def __getattr__(self, name):
    return '__getattr__:' + name
  def __setattr__(self, name, value):
    value[name] = name
" + ClassName + " = " + ClassName + "\n";
    }

    public class Inherited : InheritanceTestBaseClassWrapper
    {
        public const int OverridenVirtValue = -42;
        public const int X = 42;
        readonly Dictionary<string, object> extras = new Dictionary<string, object>();
        public int virt() => OverridenVirtValue;
        public int XProp
        {
            get
            {
                using (var scope = Py.CreateScope())
                {
                    scope.Set("this", this);
                    try
                    {
                        return scope.Eval<int>($"super(this.__class__, this).{nameof(XProp)}");
                    }
                    catch (PythonException ex) when (PythonReferenceComparer.Instance.Equals(ex.Type, Exceptions.AttributeError))
                    {
                        if (this.extras.TryGetValue(nameof(this.XProp), out object value))
                            return (int)value;
                        throw;
                    }
                }
            }
            set => this.extras[nameof(this.XProp)] = value;
        }
    }

    public class PropertyAccessorBase
    {
        public virtual string VirtualProp { get; set; }
    }

    public class PropertyAccessorIntermediate: PropertyAccessorBase { }

    public class PropertyAccessorDerived: PropertyAccessorIntermediate
    {
        public override string VirtualProp { set => base.VirtualProp = value.ToUpperInvariant(); }
    }

    public class ContainerClass
    {
        public void BaseMethod() { }

        public class InnerClass: ContainerClass
        {

        }
    }
}
