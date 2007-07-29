# Makefile for PythonNET
# usage:
#     make PYTHON=/path/to/python
#     make PYTHON=C:/Python25/python.exe
#     make PYTHON=/path/to/python DEFINE=additional,defines CSCARGS=additional_args
#     make clean

RELEASE = pythonnet-2.0-alpha3

PYTHON ?= python
PYTHONVER ?= $(shell $(PYTHON) -c "import sys; print 'PYTHON%i%i' % sys.version_info[:2]")
UCS ?= $(shell $(PYTHON) -c "from distutils.sysconfig import get_config_var; \
                          print 'UCS%i' % (get_config_var('Py_UNICODE_SIZE') or 2)")

ifeq ($(origin WINDIR), undefined)
    RUNNER = mono
    ILDASM = monodis
    ILASM = ilasm
    CSC = gmcs
    RUNTIME_REF = /reference:Mono.Posix.dll
    ALL = clr.so monoclr
else
    RUNNER = 
    ILDASM = ildasm.exe 
    ILASM = ilasm.exe
    CSC = csc.exe
    RUNTIME_REF = 
    ALL = clr.pyd
endif 

ifeq ($(origin DEFINE), undefined)
    _DEFINE = $(PYTHONVER),$(UCS)
else
    _DEFINE = $(DEFINE),$(PYTHONVER),$(UCS)
endif

CSC += /define:$(_DEFINE) /nologo $(CSCARGS)

BASEDIR = $(shell pwd)

PYTHON_CS = $(wildcard $(BASEDIR)/src/console/*.cs)
RUNTIME_CS = $(wildcard $(BASEDIR)/src/runtime/*.cs)
TESTING_CS = $(wildcard $(BASEDIR)/src/testing/*.cs)
EMBED_CS = $(wildcard $(BASEDIR)/src/embed_tests/*.cs)

all: Python.Runtime.dll python.exe Python.Test.dll $(ALL)

cleanall: clean all

python.exe: Python.Runtime.dll $(PYTHON_CS)
	cd "$(BASEDIR)/src/console"; \
	$(CSC) /target:exe /out:../../python.exe \
	    /reference:../../Python.Runtime.dll /recurse:*.cs

Python.Runtime.dll: $(RUNTIME_CS)
	cd "$(BASEDIR)/src/runtime"; \
	$(CSC) /unsafe /target:library \
	    $(RUNTIME_REF) /out:../../Python.Runtime.dll /recurse:*.cs


clr.pyd: Python.Runtime.dll src/runtime/clrmodule.il
	$(ILASM) /nologo /dll /quiet /output=clr.pyd \
	    src/runtime/clrmodule.il


clr.so: Python.Runtime.dll src/monoclr/clrmod.c src/monoclr/pynetclr.h \
    src/monoclr/pynetinit.c
	$(PYTHON) setup.py build_ext -i


Python.Test.dll: Python.Runtime.dll
	cd "$(BASEDIR)/src/testing"; \
	$(CSC) /target:library /out:../../Python.Test.dll \
	    /reference:../../Python.Runtime.dll,System.Windows.Forms.dll \
	    /recurse:*.cs

.PHONY=clean
clean:
	find . \( -name \*.o -o -name \*.so -o -name \*.py[co] -o -name \
	    \*.dll -o -name \*.exe -o -name \*.pdb -o -name \*.mdb \
	    -o -name \*.pyd -o -name \*~ \) -exec rm -f {} \;
	rm -f Python*.il Python*.il2 Python*.res
	rm -rf build/
	cd src/console; rm -rf bin; rm -rf obj; cd ../..;
	cd src/runtime; rm -rf bin; rm -rf obj; cd ../..;
	cd src/testing; rm -rf bin; rm -rf obj; cd ../..;
	cd src/embed_tests; rm -rf bin; rm -rf obj; rm -f TestResult.xml; cd ../..;
	cd src/monoclr; make clean; cd ../..

.PHONY=test
test: all
	rm -f ./src/tests/*.pyc
	$(RUNNER) ./python.exe ./src/tests/runtests.py

.PHONY=dist
dist: clean all
	rm -rf ./$(RELEASE)
	mkdir ./$(RELEASE)
	mkdir -p ./release
	cp ./makefile ./$(RELEASE)/
	cp ./*.sln ./$(RELEASE)/
	cp ./*.txt ./$(RELEASE)/
	svn export ./demo ./$(RELEASE)/demo/
	svn export ./doc ./$(RELEASE)/doc/
	svn export ./src ./$(RELEASE)/src/
	cp ./python.exe ./$(RELEASE)/
	cp ./*.dll ./$(RELEASE)/
	cp ./*.pyd ./$(RELEASE)/
	tar czf $(RELEASE).tgz ./$(RELEASE)/
	mv $(RELEASE).tgz ./release/
	rm -rf ./$(RELEASE)/

dis:
	$(ILDASM) Python.Runtime.dll /out=Python.Runtime.il

asm:
	$(ILASM) /dll /quiet  \
	    /resource=Python.Runtime.res /output=Python.Runtime.dll \
	    Python.Runtime.il

monoclr:
	make -C $(BASEDIR)/src/monoclr PYTHON=$(PYTHON)

run: python.exe
	$(RUNNER) python.exe

