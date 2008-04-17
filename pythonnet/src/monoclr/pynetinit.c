// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.1 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================
//
// Author: Christian Heimes <christian(at)cheimes(dot)de>

#include "pynetclr.h"
#include "dirent.h"

// initialize Mono and PythonNet
PyNet_Args* PyNet_Init(int ext) {
    PyNet_Args *pn_args;
    pn_args = (PyNet_Args *)malloc(sizeof(PyNet_Args));
    pn_args->pr_file = PR_ASSEMBLY;
    pn_args->error = NULL;
    pn_args->shutdown = NULL;

    if (ext == 0) {
        pn_args->init_name = "Python.Runtime:Initialize()";
    } else {
        pn_args->init_name = "Python.Runtime:InitExt()";
    }
    pn_args->shutdown_name = "Python.Runtime:Shutdown()";

    pn_args->domain = mono_jit_init_version(MONO_DOMAIN, MONO_VERSION);

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

    if (pn_args->error != NULL) {
        PyErr_SetString(PyExc_ImportError, pn_args->error);
    }
    return pn_args;
} 

// Shuts down PythonNet and cleans up Mono
void PyNet_Finalize(PyNet_Args *pn_args) {
    MonoObject *exception = NULL;

    if (pn_args->shutdown) {
        mono_runtime_invoke(pn_args->shutdown, NULL, NULL, &exception);
        if (exception) {
            pn_args->error = PyNet_ExceptionToString(exception);
        }
        pn_args->shutdown = NULL;
    }
    
    if (pn_args->domain) {
        mono_jit_cleanup(pn_args->domain);
        pn_args->domain = NULL;
    }
    free(pn_args);
}

MonoMethod *getMethodFromClass(MonoClass *cls, char *name) {
    MonoMethodDesc *mdesc;
    MonoMethod *method;

    mdesc = mono_method_desc_new(name, 1);
    method = mono_method_desc_search_in_class(mdesc, cls);
    mono_method_desc_free(mdesc);

    return method;
}

void main_thread_handler (gpointer user_data) {
    PyNet_Args *pn_args=(PyNet_Args *)user_data;
    MonoMethod *init;
    MonoImage *pr_image;
    MonoClass *pythonengine;
    MonoObject *exception = NULL;

    //get python path system variable
    PyObject* syspath = PySys_GetObject("path");
    char* runtime_full_path = (char*) malloc(1024);
    const char* slash = "/";
    int found = 0;

    int ii = 0;
    for (ii = 0; ii < PyList_Size(syspath); ++ii) {
        const char* pydir = PyString_AsString(PyList_GetItem(syspath, ii));
        char* curdir = (char*) malloc(1024);
        if (strlen(pydir) == 0) pydir = ".";
    	
        strcpy(curdir, pydir);
        strcat(curdir, slash);

        //look in this directory for the pn_args->pr_file
        DIR* dirp = opendir(curdir);
        if (dirp != NULL) {

            struct dirent *dp;
            while ((dp = readdir(dirp)) != NULL) {
                if (strcmp(dp->d_name, pn_args->pr_file) == 0) {
                    strcpy(runtime_full_path, curdir);
                    strcat(runtime_full_path, pn_args->pr_file);
                    found = 1;
                    break;
                }
            }
            closedir(dirp);
        }
        free(curdir);

        if (found) {
            pn_args->pr_file = runtime_full_path;
            break;
        }
    }

    if (!found) {
        fprintf(stderr, "Could not find assembly %s. \n", pn_args->pr_file);
        return;
    }

    pn_args->pr_assm = mono_domain_assembly_open(pn_args->domain, pn_args->pr_file);
    if (!pn_args->pr_assm) {
        pn_args->error = "Unable to load assembly";
        return;
    }
    free(runtime_full_path);

    pr_image = mono_assembly_get_image(pn_args->pr_assm);
    if (!pr_image) {
        pn_args->error = "Unable to get image";
        return;
    }

    pythonengine = mono_class_from_name(pr_image, "Python.Runtime", "PythonEngine");
    if (!pythonengine) {
        pn_args->error = "Unable to load class PythonEngine from Python.Runtime";
        return;
    }

    init = getMethodFromClass(pythonengine, pn_args->init_name);
    if (!init) {
        pn_args->error = "Unable to fetch Init method from PythonEngine";
        return;
    }

    pn_args->shutdown = getMethodFromClass(pythonengine, pn_args->shutdown_name);
    if (!pn_args->shutdown) {
        pn_args->error = "Unable to fetch shutdown method from PythonEngine";
        return;
    }

    mono_runtime_invoke(init, NULL, NULL, &exception);
    if (exception) {
        pn_args->error = PyNet_ExceptionToString(exception);
        return;
    }
}

// Get string from a Mono exception 
char* PyNet_ExceptionToString(MonoObject *e) {
    MonoMethodDesc* mdesc = mono_method_desc_new(":ToString()", FALSE);
    MonoMethod* mmethod = mono_method_desc_search_in_class(mdesc, 
        mono_get_object_class());
    mono_method_desc_free(mdesc);
    mmethod = mono_object_get_virtual_method(e, mmethod);
    MonoString* monoString = (MonoString*) mono_runtime_invoke(mmethod, 
        e, NULL, NULL);
    return mono_string_to_utf8(monoString);
}

