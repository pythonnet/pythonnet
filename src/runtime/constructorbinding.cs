using System;
using System.Reflection;

namespace Python.Runtime
{
    /// <summary>
    /// Implements a Python type that wraps a CLR ctor call. Constructor objects
    /// support a .Overloads[] syntax to allow explicit ctor overload selection.
    /// </summary>
    /// <remarks>
    /// ClassManager stores a ConstructorBinding instance in the class's __dict__['Overloads']
    /// SomeType.Overloads[Type, ...] works like this:
    /// 1) Python retrieves the Overloads attribute from this ClassObject's dictionary normally
    /// and finds a non-null tp_descr_get slot which is called by the interpreter
    /// and returns an IncRef()ed pyHandle to itself.
    /// 2) The ConstructorBinding object handles the [] syntax in its mp_subscript by matching
    /// the Type object parameters to a constructor overload using Type.GetConstructor()
    /// [NOTE: I don't know why method overloads are not searched the same way.]
    /// and creating the BoundContructor object which contains ContructorInfo object.
    /// 3) In tp_call, if ctorInfo is not null, ctorBinder.InvokeRaw() is called.
    /// </remarks>
    [Serializable]
    internal class ConstructorBinding : ExtensionType
    {
        private MaybeType type; // The managed Type being wrapped in a ClassObject
        private PyType typeToCreate; // The python type tells GetInstHandle which Type to create.
        private ConstructorBinder ctorBinder;

        [NonSerialized]
        private PyObject? repr;

        public ConstructorBinding(Type type, PyType typeToCreate, ConstructorBinder ctorBinder)
        {
            this.type = type;
            this.typeToCreate = typeToCreate;
            this.ctorBinder = ctorBinder;
        }

        /// <summary>
        /// Descriptor __get__ implementation.
        /// Implements a Python type that wraps a CLR ctor call that requires the use
        /// of a .Overloads[pyTypeOrType...] syntax to allow explicit ctor overload
        /// selection.
        /// </summary>
        /// <param name="op"> PyObject* to a Constructors wrapper </param>
        /// <param name="instance">
        /// the instance that the attribute was accessed through,
        /// or None when the attribute is accessed through the owner
        /// </param>
        /// <param name="owner"> always the owner class </param>
        /// <returns>
        /// a CtorMapper (that borrows a reference to this python type and the
        /// ClassObject's ConstructorBinder) wrapper.
        /// </returns>
        /// <remarks>
        /// Python 2.6.5 docs:
        /// object.__get__(self, instance, owner)
        /// Called to get the attribute of the owner class (class attribute access)
        /// or of an instance of that class (instance attribute access).
        /// owner is always the owner class, while instance is the instance that
        /// the attribute was accessed through, or None when the attribute is accessed through the owner.
        /// This method should return the (computed) attribute value or raise an AttributeError exception.
        /// </remarks>
        public static NewReference tp_descr_get(BorrowedReference op, BorrowedReference instance, BorrowedReference owner)
        {
            var self = (ConstructorBinding?)GetManagedObject(op);
            if (self == null)
            {
                Exceptions.SetError(Exceptions.AssertionError, "attempting to access destroyed object");
                return default;
            }

            // It doesn't seem to matter if it's accessed through an instance (rather than via the type).
            /*if (instance != IntPtr.Zero) {
            // This is ugly! PyObject_IsInstance() returns 1 for true, 0 for false, -1 for error...
                if (Runtime.PyObject_IsInstance(instance, owner) < 1) {
                    return Exceptions.RaiseTypeError("How in the world could that happen!");
                }
            }*/
            return new NewReference(op);
        }

        /// <summary>
        /// Implement explicit overload selection using subscript syntax ([]).
        /// </summary>
        /// <remarks>
        /// ConstructorBinding.GetItem(PyObject *o, PyObject *key)
        /// Return element of o corresponding to the object key or NULL on failure.
        /// This is the equivalent of the Python expression o[key].
        /// </remarks>
        public static NewReference mp_subscript(BorrowedReference op, BorrowedReference key)
        {
            var self = (ConstructorBinding)GetManagedObject(op)!;
            if (!self.type.Valid)
            {
                return Exceptions.RaiseTypeError(self.type.DeletedMessage);
            }
            Type tp = self.type.Value;

            Type[]? types = Runtime.PythonArgsToTypeArray(key);
            if (types == null)
            {
                return Exceptions.RaiseTypeError("type(s) expected");
            }
            //MethodBase[] methBaseArray = self.ctorBinder.GetMethods();
            //MethodBase ci = MatchSignature(methBaseArray, types);
            ConstructorInfo ci = tp.GetConstructor(types);
            if (ci == null)
            {
                return Exceptions.RaiseTypeError("No match found for constructor signature");
            }
            var boundCtor = new BoundContructor(tp, self.typeToCreate, self.ctorBinder, ci);
            return boundCtor.Alloc();
        }

        /// <summary>
        /// ConstructorBinding  __repr__ implementation [borrowed from MethodObject].
        /// </summary>
        public static NewReference tp_repr(BorrowedReference ob)
        {
            var self = (ConstructorBinding)GetManagedObject(ob)!;
            if (self.repr is not null)
            {
                return new NewReference(self.repr);
            }
            MethodBase[] methods = self.ctorBinder.GetMethods();

            if (!self.type.Valid)
            {
                return Exceptions.RaiseTypeError(self.type.DeletedMessage);
            }
            string name = self.type.Value.FullName;
            var doc = "";
            foreach (MethodBase t in methods)
            {
                if (doc.Length > 0)
                {
                    doc += "\n";
                }
                string str = t.ToString();
                int idx = str.IndexOf("(");
                doc += string.Format("{0}{1}", name, str.Substring(idx));
            }
            using var docStr = Runtime.PyString_FromString(doc);
            if (docStr.IsNull()) return default;
            self.repr = docStr.MoveToPyObject();
            return new NewReference(self.repr);
        }

        public static int tp_traverse(BorrowedReference ob, IntPtr visit, IntPtr arg)
        {
            var self = (ConstructorBinding?)GetManagedObject(ob);
            if (self is null) return 0;

            int res = PyVisit(self.typeToCreate, visit, arg);
            return res;
        }
    }

    /// <summary>
    /// Implements a Python type that constructs the given Type given a particular ContructorInfo.
    /// </summary>
    /// <remarks>
    /// Here mostly because I wanted a new __repr__ function for the selected constructor.
    /// An earlier implementation hung the __call__ on the ContructorBinding class and
    /// returned an Incref()ed self.pyHandle from the __get__ function.
    /// </remarks>
    [Serializable]
    internal class BoundContructor : ExtensionType
    {
        private Type type; // The managed Type being wrapped in a ClassObject
        private PyType typeToCreate; // The python type tells GetInstHandle which Type to create.
        private ConstructorBinder ctorBinder;
        private ConstructorInfo ctorInfo;
        private PyObject? repr;

        public BoundContructor(Type type, PyType typeToCreate, ConstructorBinder ctorBinder, ConstructorInfo ci)
        {
            this.type = type;
            this.typeToCreate = typeToCreate;
            this.ctorBinder = ctorBinder;
            ctorInfo = ci;
        }

        /// <summary>
        /// BoundContructor.__call__(PyObject *callable_object, PyObject *args, PyObject *kw)
        /// </summary>
        /// <param name="op"> PyObject *callable_object </param>
        /// <param name="args"> PyObject *args </param>
        /// <param name="kw"> PyObject *kw </param>
        /// <returns> A reference to a new instance of the class by invoking the selected ctor(). </returns>
        public static NewReference tp_call(BorrowedReference op, BorrowedReference args, BorrowedReference kw)
        {
            var self = (BoundContructor)GetManagedObject(op)!;
            // Even though a call with null ctorInfo just produces the old behavior
            /*if (self.ctorInfo == null) {
                string msg = "Usage: Class.Overloads[CLR_or_python_Type, ...]";
                return Exceptions.RaiseTypeError(msg);
            }*/
            // Bind using ConstructorBinder.Bind and invoke the ctor providing a null instancePtr
            // which will fire self.ctorInfo using ConstructorInfo.Invoke().
            object? obj = self.ctorBinder.InvokeRaw(null, args, kw, self.ctorInfo);
            if (obj == null)
            {
                // XXX set an error
                return default;
            }
            // Instantiate the python object that wraps the result of the method call
            // and return the PyObject* to it.
            return CLRObject.GetReference(obj, self.typeToCreate);
        }

        /// <summary>
        /// BoundContructor  __repr__ implementation [borrowed from MethodObject].
        /// </summary>
        public static NewReference tp_repr(BorrowedReference ob)
        {
            var self = (BoundContructor)GetManagedObject(ob)!;
            if (self.repr is not null)
            {
                return new NewReference(self.repr);
            }
            string name = self.type.FullName;
            string str = self.ctorInfo.ToString();
            int idx = str.IndexOf("(");
            str = string.Format("returns a new {0}{1}", name, str.Substring(idx));
            using var docStr = Runtime.PyString_FromString(str);
            if (docStr.IsNull()) return default;
            self.repr = docStr.MoveToPyObject();
            return new NewReference(self.repr);
        }

        public static int tp_traverse(BorrowedReference ob, IntPtr visit, IntPtr arg)
        {
            var self = (BoundContructor?)GetManagedObject(ob);
            if (self is null) return 0;

            int res = PyVisit(self.typeToCreate, visit, arg);
            return res;
        }
    }
}
