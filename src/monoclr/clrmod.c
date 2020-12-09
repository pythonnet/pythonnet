// #define Py_LIMITED_API 0x03050000
#include <Python.h>

#include "stdlib.h"

#define MONO_VERSION "v4.0.30319.1"
#define MONO_DOMAIN "Python"

#include <mono/jit/jit.h>
#include <mono/metadata/environment.h>
#include <mono/metadata/mono-config.h>
#include <mono/metadata/debug-helpers.h>
#include <mono/metadata/assembly.h>

#ifndef _WIN32
#include "dirent.h"
#include "dlfcn.h"
#include "libgen.h"
#include "alloca.h"
#endif

typedef struct
{
    MonoDomain *domain;
    MonoAssembly *pr_assm;
    MonoMethod *shutdown;
    const char *pr_file;
    char *error;
    char *init_name;
    char *shutdown_name;
    PyObject *module;
} PyNet_Args;

PyNet_Args *PyNet_Init(void);
static PyNet_Args *pn_args;

PyMODINIT_FUNC
PyInit_clr(void)
{
    pn_args = PyNet_Init();
    if (pn_args->error)
    {
        return NULL;
    }

    return pn_args->module;
}

void PyNet_Finalize(PyNet_Args *);
void main_thread_handler(PyNet_Args *user_data);

// initialize Mono and PythonNet
PyNet_Args *PyNet_Init()
{
    PyObject *pn_module;
    PyObject *pn_path;
    PyNet_Args *pn_args;
    pn_args = (PyNet_Args *)malloc(sizeof(PyNet_Args));

    pn_module = PyImport_ImportModule("pythonnet");
    if (pn_module == NULL)
    {
        pn_args->error = "Failed to import pythonnet";
        return pn_args;
    }

    pn_path = PyObject_CallMethod(pn_module, "get_assembly_path", NULL);
    if (pn_path == NULL)
    {
        Py_DecRef(pn_module);
        pn_args->error = "Failed to get assembly path";
        return pn_args;
    }

    pn_args->pr_file = PyUnicode_AsUTF8(pn_path);
    pn_args->error = NULL;
    pn_args->shutdown = NULL;
    pn_args->module = NULL;

#ifdef __linux__
    // Force preload libmono-2.0 as global. Without this, on some systems
    // symbols from libmono are not found by libmononative (which implements
    // some of the System.* namespaces). Since the only happened on Linux so
    // far, we hardcode the library name, load the symbols into the global
    // namespace and leak the handle.
    dlopen("libmono-2.0.so", RTLD_LAZY | RTLD_GLOBAL);
#endif

    pn_args->init_name = "Python.Runtime:InitExt()";
    pn_args->shutdown_name = "Python.Runtime:Shutdown()";

    pn_args->domain = mono_jit_init_version(MONO_DOMAIN, MONO_VERSION);

    // XXX: Skip setting config for now, should be derived from pr_file
    // mono_domain_set_config(pn_args->domain, ".", "Python.Runtime.dll.config");

    /*
     * Load the default Mono configuration file, this is needed
     * if you are planning on using the dllmaps defined on the
     * system configuration
     */
    mono_config_parse(NULL);

    /* I can't use this call to run the main_thread_handler. The function
     * runs it in another thread but *this* thread holds the Python
     * import lock -> DEAD LOCK.
     *
     * mono_runtime_exec_managed_code(pn_args->domain, main_thread_handler,
     *                                pn_args);
     */

    main_thread_handler(pn_args);

    if (pn_args->error != NULL)
    {
        PyErr_SetString(PyExc_ImportError, pn_args->error);
    }
    return pn_args;
}

char *PyNet_ExceptionToString(MonoObject *e);

// Shuts down PythonNet and cleans up Mono
void PyNet_Finalize(PyNet_Args *pn_args)
{
    MonoObject *exception = NULL;

    if (pn_args->shutdown)
    {
        mono_runtime_invoke(pn_args->shutdown, NULL, NULL, &exception);
        if (exception)
        {
            pn_args->error = PyNet_ExceptionToString(exception);
        }
        pn_args->shutdown = NULL;
    }

    if (pn_args->domain)
    {
        mono_jit_cleanup(pn_args->domain);
        pn_args->domain = NULL;
    }
    free(pn_args);
}

MonoMethod *getMethodFromClass(MonoClass *cls, char *name)
{
    MonoMethodDesc *mdesc;
    MonoMethod *method;

    mdesc = mono_method_desc_new(name, 1);
    method = mono_method_desc_search_in_class(mdesc, cls);
    mono_method_desc_free(mdesc);

    return method;
}

void main_thread_handler(PyNet_Args *user_data)
{
    PyNet_Args *pn_args = user_data;
    MonoMethod *init;
    MonoImage *pr_image;
    MonoClass *pythonengine;
    MonoObject *exception = NULL;
    MonoObject *init_result;

    pn_args->pr_assm = mono_domain_assembly_open(pn_args->domain, pn_args->pr_file);
    if (!pn_args->pr_assm)
    {
        pn_args->error = "Unable to load assembly";
        return;
    }

    pr_image = mono_assembly_get_image(pn_args->pr_assm);
    if (!pr_image)
    {
        pn_args->error = "Unable to get image";
        return;
    }

    pythonengine = mono_class_from_name(pr_image, "Python.Runtime", "PythonEngine");
    if (!pythonengine)
    {
        pn_args->error = "Unable to load class PythonEngine from Python.Runtime";
        return;
    }

    init = getMethodFromClass(pythonengine, pn_args->init_name);
    if (!init)
    {
        pn_args->error = "Unable to fetch Init method from PythonEngine";
        return;
    }

    pn_args->shutdown = getMethodFromClass(pythonengine, pn_args->shutdown_name);
    if (!pn_args->shutdown)
    {
        pn_args->error = "Unable to fetch shutdown method from PythonEngine";
        return;
    }

    init_result = mono_runtime_invoke(init, NULL, NULL, &exception);
    if (exception)
    {
        pn_args->error = PyNet_ExceptionToString(exception);
        return;
    }

    pn_args->module = *(PyObject**)mono_object_unbox(init_result);
}

// Get string from a Mono exception
char *PyNet_ExceptionToString(MonoObject *e)
{
    MonoMethodDesc *mdesc = mono_method_desc_new(":ToString()", 0 /*FALSE*/);
    MonoMethod *mmethod = mono_method_desc_search_in_class(mdesc, mono_get_object_class());
    mono_method_desc_free(mdesc);

    mmethod = mono_object_get_virtual_method(e, mmethod);
    MonoString *monoString = (MonoString*) mono_runtime_invoke(mmethod, e, NULL, NULL);
    mono_runtime_invoke(mmethod, e, NULL, NULL);
    return mono_string_to_utf8(monoString);
}
