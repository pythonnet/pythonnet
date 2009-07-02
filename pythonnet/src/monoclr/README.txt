This has been updated to work with MonoFramework 1.9.

1. On MacOS X, create the following directory structure:

	/PythonNET/src/monoclr

2. Copy the setup.py to /PythonNET

3. Copy the C/C++ code and header files to /PythonNET/src/monoclr

4. Copy the Makefile to /PythonNET/src/monoclr

5. In a terminal window, from the monoclr folder, run:

	$ make

6. In a terminal window, from the PythonNET folder, run:

	$ python setup.py build

7. This creates the clr.so

8. Copy the clr.so, the Python.Runtime.dll, and the Python.Runtime.dll.config to the site-packages folder.