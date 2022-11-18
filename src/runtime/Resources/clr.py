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

    # TODO: I am not sure this add_attribute is actually necessary.
    def add_attribute(self, *args, **kwargs):
        """Adds an attribute to this class.
        If the first argument is a tuple we assume it is a tuple containing everything to initialize the attribute.
        Otherwise, the first arg should be a .net type implementing Attribute."""
        lst = []
        if len(args) > 0:
            if isinstance(args[0], tuple):
                lst = args
            else:
                lst = [(args[0], args[1:], kwargs)]
        self._clr_attributes_.extend(lst)
        return self

class property(object):
    """
    property constructor for creating properties with implicit get/set.

    It can be used as such:
    e.g.::

        class X(object):
            A = clr.property(Double, 3.14)\
               .add_attribute(Browsable(False))

    """
    def __init__(self, type, default = None):
        import weakref
        self._clr_property_type_ = type
        self.default = default
        self.values = weakref.WeakKeyDictionary()
        self._clr_attributes_ = []
        self.fget = 1
        self.fset = 1
    def __get__(self, instance, owner):
        if self.fget != 1:
            return self.fget(instance)
        v = self.values.get(instance, self.default)
        return v
    def __set__(self, instance, value):
        if self.fset != 1:
            self.fset(instance,value)
            return
        self.values[instance] = value

    def add_attribute(self, *args, **kwargs):
        """Adds an attribute to this class.
        If the first argument is a tuple we assume it is a tuple containing everything to initialize the attribute.
        Otherwise, the first arg should be a .net type implementing Attribute."""
        lst = []
        if len(args) > 0:
            if isinstance(args[0], tuple):
                lst = args
            else:
                lst = [(args[0], args[1:], kwargs)]
        self._clr_attributes_.extend(lst)
        return self

    def __call__(self, func):
        self2 = self.__class__(self._clr_property_type_, None)
        self2.fget = func
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

    def add_attribute(self, *args, **kwargs):
        """Adds an attribute to this class.
        If the first argument is a tuple we assume it is a tuple containing everything to initialize the attribute.
        Otherwise, the first arg should be a .net type implementing Attribute."""
        lst = []
        if len(args) > 0:
            if isinstance(args[0], tuple):
                lst = args
            else:
                lst = [(args[0], args[1:], kwargs)]
        self._clr_attributes_.extend(lst)
        return self

class attribute(object):
    """
    Class decorator for adding attributes to .net python classes.

    e.g.::
        @attribute(DisplayName("X Class"))
        class X(object):
            pass
    """
    def __init__(self, *args, **kwargs):
        lst = []
        if len(args) > 0:
            if isinstance(args[0], tuple):
                lst = args
            else:
                lst = [(args[0], args[1:], kwargs)]
        import Python.Runtime
        #todo: ensure that attributes only are pushed when @ is used.
        self.attr = lst
        for item in lst:
            Python.Runtime.PythonDerivedType.PushAttribute(item)

    def __call__(self, x):
        import Python.Runtime
        for item in self.attr:
            if Python.Runtime.PythonDerivedType.AssocAttribute(item, x):
                pass
        return x
