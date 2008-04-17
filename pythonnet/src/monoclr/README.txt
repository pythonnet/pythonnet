This has been updated to work with MonoFramework 1.9.

1. On MacOS X, create the following directory structure:

	/PythonNET/src/monoclr

2. Copy the Makefile and setup.py to /PythonNET

3. Copy the C/C++ code and header files to /PythonNET/src/monoclr

4. In a terminal window, run:

	$ python setup.py build

5. This creates the clr.so

6. Copy the clr.so, the Python.Runtime.dll, and the Python.Runtime.dll.config to the site-packages folder.