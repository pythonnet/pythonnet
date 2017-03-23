#include "pynetclr.h"

/* List of functions defined in the module */
static PyMethodDef clr_methods[] = {
    {NULL, NULL, 0, NULL} /* Sentinel */
};

PyDoc_STRVAR(clr_module_doc,
             "clr facade module to initialize the CLR. It's later "
             "replaced by the real clr module. This module has a facade "
             "attribute to make it distinguishable from the real clr module."
);

static PyNet_Args *pn_args;
char **environ = NULL;

#if PY_MAJOR_VERSION >= 3
static struct PyModuleDef clrdef = {
    PyModuleDef_HEAD_INIT,
    "clr",               /* m_name */
    clr_module_doc,      /* m_doc */
    -1,                  /* m_size */
    clr_methods,         /* m_methods */
    NULL,                /* m_reload */
    NULL,                /* m_traverse */
    NULL,                /* m_clear */
    NULL,                /* m_free */
};
#endif

static PyObject *_initclr()
{
    PyObject *m;

    /* Create the module and add the functions */
#if PY_MAJOR_VERSION >= 3
    m = PyModule_Create(&clrdef);
#else
    m = Py_InitModule3("clr", clr_methods, clr_module_doc);
#endif
    if (m == NULL)
        return NULL;
    PyModule_AddObject(m, "facade", Py_True);
    Py_INCREF(Py_True);

    pn_args = PyNet_Init(1);
    if (pn_args->error)
    {
        return NULL;
    }

    if (NULL != pn_args->module)
        return pn_args->module;

    return m;
}

#if PY_MAJOR_VERSION >= 3
PyMODINIT_FUNC
PyInit_clr(void)
{
    return _initclr();
}
#else
PyMODINIT_FUNC
initclr(void)
{
    _initclr();
}
#endif
