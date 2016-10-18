:: build it

:: set path to modern MSBuild
set PATH=C:\Program Files (x86)\MSBuild\14.0\Bin;%PATH%

%PYTHON% setup.py install

:: copy our compiled library
set SRC=%RECIPE_DIR%\..
set DEST=%SP_DIR%

:: Install step
copy %SRC%\Python.Runtime.dll.config %DEST%
