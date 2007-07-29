# Author: Christian Heimes

PYTHON = python2.5
BASENAME = $(shell basename $(PYTHON))
GCC = gcc

PY_LIBS = $(shell $(PYTHON) -c "from distutils.sysconfig import get_config_vars; \
    print get_config_vars('BLDLIBRARY')[0]") 
PY_CFLAGS = -I$(shell $(PYTHON) -c "from distutils.sysconfig import get_config_vars; \
    print get_config_vars('CFLAGS')[0] + ' -I' + get_config_vars('CONFINCLUDEPY')[0]")

MONO_LIBS = $(shell pkg-config --libs mono)
MONO_CFLAGS = $(shell pkg-config --cflags mono)

LIBS = $(MONO_LIBS) $(PY_LIBS)
CFLAGS = $(MONO_CFLAGS) $(PY_CFLAGS)

all: clrpython python

clrpython: clrpython.o pynetinit.o
	$(GCC) $(LIBS) clrpython.o pynetinit.o -o clr$(BASENAME) 

clrpython.o: pynetclr.h clrpython.c
	$(GCC) $(CFLAGS) -c clrpython.c -o clrpython.o 

pynetinit.o: pynetclr.h pynetinit.c
	$(GCC) $(CFLAGS) -c pynetinit.c -o pynetinit.o

python: python.c 
	$(GCC) $(PY_CFLAGS) $(PY_LIBS) python.c -o $(BASENAME)

clean:
	rm -f *.o
	rm -f *.so
	rm -f clr$(PYTHON)
	rm -f $(PYTHON)
	rm -rf build/
