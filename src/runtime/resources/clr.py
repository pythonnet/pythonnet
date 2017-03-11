"""
Code in this module gets loaded into the main clr module.
"""

__version__ = "2.3.0"


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

    def __init__(self, type_, fget=None, fset=None):
        self.__name__ = getattr(fget, "__name__", None)
        self._clr_property_type_ = type_
        self.fget = fget
        self.fset = fset

    def __call__(self, fget):
        return self.__class__(self._clr_property_type_,
                              fget=fget,
                              fset=self.fset)

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

    def __init__(self, return_type, arg_types, clrname=None, func=None):
        self.__name__ = getattr(func, "__name__", None)
        self._clr_return_type_ = return_type
        self._clr_arg_types_ = arg_types
        self._clr_method_name_ = clrname or self.__name__
        self.__func = func

    def __call__(self, func):
        return self.__class__(self._clr_return_type_,
                              self._clr_arg_types_,
                              clrname=self._clr_method_name_,
                              func=func)

    def __get__(self, instance, owner):
        return self.__func.__get__(instance, owner)
