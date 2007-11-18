# Makefile for PythonNET
# usage:
#     make PYTHON=/path/to/python
#     make PYTHON=C:/Python25/python.exe
#     make PYTHON=/path/to/python DEFINE=additional,defines CSCARGS=additional_args
#     make clean

RELEASE = pythonnet-2.0-alpha2
KEYFILE = pythonnet.key

PYTHON ?= python
PYTHONVER ?= $(shell $(PYTHON) -c "import sys; \
    print 'PYTHON%i%i' % sys.version_info[:2]") 
UCS ?= $(shell $(PYTHON) -c "import sys; \
    print 'UCS%i' % ((sys.maxunicode > 65536) and 4 or 2)")
SITEPACKAGES = $(shell $(PYTHON) -c "from distutils.sysconfig import get_python_lib; \
    print get_python_lib(plat_specific=1, standard_lib=0)")
INSTALL=/usr/bin/install -m644

ifeq ($(origin WINDIR), undefined)
    RUNNER = mono
    ILDASM = monodis
    ILASM = ilasm
    CSC = gmcs
    RUNTIME_REF = 
    ALL = clr.so monoclr
    GACUTIL = gacutil
    SN = sn
else
    RUNNER = 
    ILDASM = $${PROGRAMFILES}/Microsoft.NET/SDK/v2.0/Bin/ildasm.exe 
    ILASM = $${WINDIR}/Microsoft.NET/Framework/v2.0.50727/ilasm.exe
    CSC = $${WINDIR}/Microsoft.NET/Framework/v2.0.50727/csc.exe
    RUNTIME_REF = 
    ALL = 
    GACUTIL = $${PROGRAMFILES}/Microsoft.NET/SDK/v2.0/Bin/gacutil.exe /nologo
    SN = $${PROGRAMFILES}/Microsoft.NET/SDK/v2.0/Bin/sn.exe /q
endif 

ifeq ($(origin DEFINE), undefined)
    _DEFINE = 
else
    _DEFINE = /define:$(DEFINE)
endif

ifeq ($(UCS), UCS4)
    RUNTIME_REF = /reference:Mono.Posix.dll
endif 

CSC += /define:$(PYTHONVER) /define:$(UCS) $(_DEFINE) /nologo $(CSCARGS)

BASEDIR = $(shell pwd)

PYTHON_CS = $(wildcard $(BASEDIR)/src/console/*.cs)
RUNTIME_CS = $(wildcard $(BASEDIR)/src/runtime/*.cs)
TESTING_CS = $(wildcard $(BASEDIR)/src/testing/*.cs)
EMBED_CS = $(wildcard $(BASEDIR)/src/embed_tests/*.cs)

all: Python.Runtime.dll python.exe Python.Test.dll clr.pyd $(ALL)

cleanall: realclean all

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

Python.EmbeddingTest.dll: Python.Runtime.dll $(EMBED_CS)
	cd $(BASEDIR)/src/embed_tests; \
	$(CSC) /target:library /out:../../Python.EmbeddingTest.dll \
	    /reference:../../Python.Runtime.dll,System.Windows.Forms.dll,nunit.framework \
	    /recurse:*.cs

.PHONY=clean
clean:
	rm -f *.exe *.dll *.so *.pyd
	make -C src/monoclr clean

.PHONY=realclean
realclean: clean
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
dist: realclean
	if ! [ -f $(KEYFILE) ]; then \
	    echo "Could not find $(KEYFILE) to sign assemblies"; \
	    exit 1; \
	fi 
	rm -rf ./$(RELEASE)
	mkdir -p ./release/
	mkdir ./$(RELEASE)
	cp ./Makefile ./$(RELEASE)/
	cp ./*.sln ./$(RELEASE)/
	cp ./*.mds ./$(RELEASE)/
	cp ./*.txt ./$(RELEASE)/
	cp ./*.py ./$(RELEASE)/
	svn export ./demo ./$(RELEASE)/demo/
	svn export ./doc ./$(RELEASE)/doc/
	svn export ./src ./$(RELEASE)/src/
	for PY in python2.4 python2.5; do \
	    for PYUCS in UCS2 UCS4; do \
	        make clean; \
		make PYTHON=$$PY UCS=$$PYUCS CSCARGS="/keyfile:$(BASEDIR)/$(KEYFILE) /optimize"; \
		mkdir ./$(RELEASE)/$$PY-$$PYUCS; \
		cp *.dll *.exe *.pyd *.so ./$(RELEASE)/$$PY-$$PYUCS/; \
	    done; \
	done;
	tar czf $(RELEASE).tar.gz ./$(RELEASE)/
	zip -r -6 $(RELEASE).zip ./$(RELEASE)
	mv $(RELEASE).* ./release/
	rm -rf ./$(RELEASE)/

sign:
	rm -f release/$(RELEASE)*.sig
	cd release; md5sum $(RELEASE).tar.gz $(RELEASE).zip > \
	    $(RELEASE).md5
	cd release; sha256sum $(RELEASE).tar.gz $(RELEASE).zip > \
	    $(RELEASE).sha
	cd release; gpg -sab $(RELEASE).zip
	cd release; gpg -sab $(RELEASE).tar.gz 

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

install: all
	$(PYTHON) setup.py install

mkkey:
	$(SN) -k $(KEYFILE)

