#ifndef PYNET_CLR_H
#define PYNET_CLR_H

#include <Python.h>

#define CLASS_NAME "Python.Runtime.PythonEngine"
#define ASSEMBLY_NAME "Python.Runtime"
#define PR_ASSEMBLY "Python.Runtime.dll"

typedef void* (*py_init)(void);
typedef void (*py_finalize)(void);

typedef struct
{
    char *pr_file;
    char *error;
    char *assembly_path;
    char *assembly_name;
    char *class_name;
    char *init_method_name;
    char *shutdown_method_name;
    char *entry_path;
    char *clr_path;
    void* core_clr_lib;
    void* host_handle;
    unsigned int domain_id;
    PyObject *module;
    py_init init;
    py_finalize shutdown;
} PyNet_Args;

PyNet_Args *PyNet_Init(int);
void PyNet_Finalize(PyNet_Args *);

void init(PyNet_Args *);
int createDelegates(PyNet_Args *);

#endif // PYNET_CLR_H
