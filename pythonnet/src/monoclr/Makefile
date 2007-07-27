# Author: Christian Heimes

PYTHON=python2.5

all: clrpython python

clrpython: clrpython.o pynetinit.o
	gcc `pkg-config --libs mono` -I/usr/include/$(PYTHON) -l$(PYTHON) \
	    clrpython.o pynetinit.o -o clr$(PYTHON)

clrpython.o: pynetclr.h clrpython.c
	gcc `pkg-config --cflags mono` -I/usr/include/$(PYTHON) -c clrpython.c -o clrpython.o 
pynetinit.o: pynetclr.h pynetinit.c
	gcc `pkg-config --cflags mono` -I/usr/include/$(PYTHON) -c pynetinit.c -o pynetinit.o

python: python.c 
	gcc -I/usr/include/$(PYTHON) -l$(PYTHON) python.c -o $(PYTHON)

clean:
	rm -f *.o
	rm -f *.so
	rm -f clr$(PYTHON)
	rm -f $(PYTHON)
	rm -rf build/

