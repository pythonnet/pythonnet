"""
Code in this module gets loaded into the main clr module.
"""


class clrproperty(object):
    """
    Property decorator for exposing python properties to .NET.
    The property type must be specified as the only argument to clrproperty.

    e.g.::

        class X(object):
            @clrproperty(string)
            def test(self):
                return "x"

    Properties decorated this way can be called from .NET, e.g.::

        dynamic x = getX(); // get an instance of X declared in Python
        string z = x.test; // calls into python and returns "x"
    """

    def __init__(self, type_, fget=None, fset=None, attributes = []):
        self.__name__ = getattr(fget, "__name__", None)
        self._clr_property_type_ = type_
        self.fget = fget
        self.fset = fset
        self._clr_attributes_ = attributes
    def __call__(self, fget):
        self.__class__(self._clr_property_type_,
                              fget=fget,
                              fset=self.fset,
                              attributes = self._clr_attributes_)


    def setter(self, fset):
        self.fset = fset
        return self

    def getter(self, fget):
        self.fget = fget
        return self

    def __get__(self, instance, owner):
        return self.fget.__get__(instance, owner)()

    def __set__(self, instance, value):
        if not self.fset:
            raise AttributeError("%s is read-only" % self.__name__)
        return self.fset.__get__(instance, None)(value)
    def add_attribute(self, attribute):
        self._clr_attributes_.append(attribute)
        return self

class property(object):

    def __init__(self, type, default):
        import weakref
        self._clr_property_type_ = type
        self.default = default
        self.values = weakref.WeakKeyDictionary()
        self._clr_attributes_ = []
        self.fget = 1
        self.fset = 1
    def __get__(self, instance, owner):
        v = self.values.get(instance, self.default)
        return v
    def __set__(self, instance, value):
        self.values[instance] = value
    def add_attribute(self, attribute):
        self._clr_attributes_.append(attribute)
        return self
    def __call__(self, type, default):
        self2 = self.__class__(self._clr_property_type_, type, default)
        self2._clr_attributes_ = self._clr_attributes_
        return self2
class clrmethod(object):
    """
    Method decorator for exposing python methods to .NET.
    The argument and return types must be specified as arguments to clrmethod.

    e.g.::

        class X(object):
            @clrmethod(int, [str])
            def test(self, x):
                return len(x)

    Methods decorated this way can be called from .NET, e.g.::

        dynamic x = getX(); // get an instance of X declared in Python
        int z = x.test("hello"); // calls into python and returns len("hello")
    """

    def __init__(self, return_type = None, arg_types = [], clrname=None, func=None, **kwargs):
        if return_type == None:
            import System
            return_type = System.Void
        self.__name__ = getattr(func, "__name__", None)
        self._clr_return_type_ = return_type
        self._clr_arg_types_ = arg_types
        self._clr_method_name_ = clrname or self.__name__
        self.__func = func
        if 'attributes' in kwargs:
            self._clr_attributes_ = kwargs["attributes"]
        else:
            self._clr_attributes_ = []

    def __call__(self, func):
        self2 = self.__class__(self._clr_return_type_,
                              self._clr_arg_types_,
                              clrname=self._clr_method_name_,
                              func=func)
        self2._clr_attributes_ = self._clr_attributes_
        return self2

    def __get__(self, instance, owner):
        return self.__func.__get__(instance, owner)

    def clr_attribute(self, attribute):
        self._clr_attributes_.append(attribute)
        return self

class attribute(object):

    def __init__(self, attr, *args, **kwargs):
        self.attr = attr
        import Python.Runtime
        #todo: ensure that attributes only are pushed when @ is used.
        #import inspect
        #Python.Runtime.PythonDerivedType.Test(inspect.stack()[1].code_context)

        Python.Runtime.PythonDerivedType.PushAttribute(attr)
    def __call__(self, x):
        import Python.Runtime
        if Python.Runtime.PythonDerivedType.AssocAttribute(self.attr, x):
            pass
        return x
