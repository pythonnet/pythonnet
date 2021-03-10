import importlib.abc
import sys

class DotNetLoader(importlib.abc.Loader):

    def __init__(self):
        super(DotNetLoader, self).__init__()

    @classmethod
    def exec_module(klass, mod):
        # This method needs to exist.
        pass

    @classmethod
    def create_module(klass, spec):
        import clr
        return clr._LoadClrModule(spec)

class DotNetFinder(importlib.abc.MetaPathFinder):
    
    def __init__(self):
        super(DotNetFinder, self).__init__()
    
    @classmethod
    def find_spec(klass, fullname, paths=None, target=None):
        import clr
        if (hasattr(clr, '_availableNamespaces') and fullname in clr._availableNamespaces):
            return importlib.machinery.ModuleSpec(fullname, DotNetLoader(), is_package=True)
        return None


def init_import_hook():
    sys.meta_path.append(DotNetFinder())