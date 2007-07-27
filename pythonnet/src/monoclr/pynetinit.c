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

    /*
     * Load the default Mono configuration file, this is needed
     * if you are planning on using the dllmaps defined on the
     * system configuration
     */
    mono_config_parse(NULL);

    pn_args->domain = mono_jit_init_version(MONO_DOMAIN, MONO_VERSION);

    /* I can't use this call to run the main_thread_handler. The function
     * runs it in another thread but *this* thread holds the Python
     * import lock -> DEAD LOCK.
     *
     * mono_runtime_exec_managed_code(pn_args->domain, main_thread_handler,
     *                                pn_args);
     */
				   
    main_thread_handler(pn_args);

    if (pn_args->error != NULL) {
        fprintf(stderr, "CRITICAL ERROR\n");
        fprintf(stderr, pn_args->error);
        fprintf(stderr, "\n\n");
    }
    return pn_args;
} 

void PyNet_Finalize(PyNet_Args *pn_args) {
    MonoObject *exception = NULL;

    if (pn_args->shutdown) {
        mono_runtime_invoke(pn_args->shutdown, NULL, NULL, &exception);
        if (exception) {
            pn_args->error = "An exception was raised during shutdown";
            fprintf(stderr, pn_args->error);
	    fprintf(stderr, "\n");
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
    MonoMethodDesc *method_desc;
    MonoMethod *method;

    method_desc = mono_method_desc_new(name, 1);
    method = mono_method_desc_search_in_class(method_desc, cls);
    mono_method_desc_free(method_desc);

    return method;
}

void main_thread_handler (gpointer user_data) {
    PyNet_Args *pn_args=(PyNet_Args *)user_data;
    MonoMethod *init;
    MonoImage *pr_image;
    MonoClass *pythonengine;
    MonoObject *exception = NULL;

    pn_args->pr_assm = mono_domain_assembly_open(pn_args->domain, pn_args->pr_file);
    if (!pn_args->pr_assm) {
        pn_args->error = "Unable to load assembly";
        return;
    }

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
        pn_args->error = "An exception was raised";
	return;
    }
}
