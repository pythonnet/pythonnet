Visual Studio 2005
==================

pythonnet contains a new solution file for Visual Studio 2005: pythonnet.sln
It should make development under Windows much easier since you don't have to
install MSys or Cygwin to run the makefile.

The solution file should work with the free VS .NET Express Edition.

Available configurations
------------------------

Every configuration copies the dll, pdf and exe files to the root directory
of the project.

 * Release
   Builds Python.Runtime, Python.Tests, clr.pyd and python.exe. The console
   project starts a Python console
   
 * Debug
   Same as Release but creates a build with debug symbols
   
 * UnitTest
   Builds a Debug build. The console project invokes runtests.py instead of
   opening a Python shell.
   
 * EmbeddingTest
   Builds Python.EmbeddingTests and its dependencies. The configuration
   requires the NUunit framework.
   
Python version
--------------

You can switch the destination version by defining either PYTHON24 or PYTHON25
inside the Python.Runtime project. 

 ** Don't forget to force a rebuild after you have altered the setting! **

MS VS doesn't take changes to define into account.

Thanks to Virgil Duprasfor his original VS howto!

Christian 'Tiran' Heimes
