# Author: Christian Heimes

PYTHON = python2.5
BASENAME = $(shell $(PYTHON) -c "import sys; print 'python%i.%i' % sys.version_info[:2]")
GCC = gcc

PY_LIBS = $(shell $(PYTHON) -c "from distutils.sysconfig import get_config_vars; \
    print get_config_vars('BLDLIBRARY')[0]") -lpython
PY_CFLAGS = -I$(shell $(PYTHON) -c "from distutils.sysconfig import get_config_vars; \
    print get_config_vars('CFLAGS')[0] + ' -I' + get_config_vars('CONFINCLUDEPY')[0]")

MONO_LIBS = $(shell pkg-config --libs mono)
MONO_CFLAGS = $(shell pkg-config --cflags mono)

LIBS = $(MONO_LIBS) $(PY_LIBS)
CFLAGS = $(MONO_CFLAGS) $(PY_CFLAGS)

all: clr$(BASENAME) $(BASENAME) clr.so

clrmod.o: pynetclr.h clrmod.c
	$(GCC) $(CFLAGS) -c clrmod.c -o clrmod.o	

clrpython.o: pynetclr.h clrpython.c
	$(GCC) $(CFLAGS) -c clrpython.c -o clrpython.o 

pynetinit.o: pynetclr.h pynetinit.c
	$(GCC) $(CFLAGS) -c pynetinit.c -o pynetinit.o

$(BASENAME): python.c 
	$(GCC) $(PY_CFLAGS) $(PY_LIBS) python.c -o $(BASENAME)

clr.so: clrmod.o pynetinit.o
	$(GCC) $(LIBS) -dynamiclib pynetinit.o clrmod.o -o clr.so

clr$(BASENAME): clrpython.o pynetinit.o
	$(GCC) $(LIBS) clrpython.o pynetinit.o -o clr$(BASENAME) 

clean:
	rm -f *.o
	rm -f *.so
	rm -f clrpython2.?
	rm -f python2.? 
	rm -rf build/

