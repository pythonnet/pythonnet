import os
import clr
path_to_dll = os.path.join(os.path.dirname(__file__), "TestDependencyAssembly.dll")
clr.AddReference(path_to_dll)
from TestDependencyAssembly import TestDependency