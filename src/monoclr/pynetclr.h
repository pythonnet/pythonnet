#ifndef PYNET_CLR_H
#define PYNET_CLR_H

#include <Python.h>
#include <mono/jit/jit.h>
#include <mono/metadata/environment.h>
#include <mono/metadata/mono-config.h>
#include <mono/metadata/debug-helpers.h>
#include <mono/metadata/assembly.h>
#include <glib.h>

#define MONO_VERSION "v4.0.30319.1"
#define MONO_DOMAIN "Python.Runtime"
#define PR_ASSEMBLY "Python.Runtime.dll"

typedef struct
{
    MonoDomain *domain;
    MonoAssembly *pr_assm;
    MonoMethod *shutdown;
    char *pr_file;
    char *error;
    char *init_name;
    char *shutdown_name;
    PyObject *module;
} PyNet_Args;

PyNet_Args *PyNet_Init(int);
void PyNet_Finalize(PyNet_Args *);
void main_thread_handler(gpointer user_data);
char *PyNet_ExceptionToString(MonoObject *);

#endif // PYNET_CLR_H
