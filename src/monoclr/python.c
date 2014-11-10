// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.1 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================
//
// python.c provides a python executable with is dynamically linked agaist
// libpython2.x.so. For example Ubuntu's python executables aren't linked
// against libpython :(
//
// Author: Christian Heimes <christian(at)cheimes(dot)de>
//

#include <Python.h>

#if (PY_MAJOR_VERSION > 2)
#include <wchar.h>
#endif

int main(int argc, char **argv) {
#if (PY_MAJOR_VERSION > 2)
    int i, result;
    size_t len;
    wchar_t **wargv = (wchar_t**)malloc(sizeof(wchar_t*)*argc);
    for (i=0; i<argc; ++ i) {
        len = strlen(argv[i]);
        wargv[i] = (wchar_t*)malloc(sizeof(wchar_t)*(len+1));
	if (len == mbsrtowcs(wargv[i], (const char**)&argv[i], len, NULL))
            wargv[i][len] = 0;
    }
    result = Py_Main(argc, wargv);
    for (i=0; i<argc; ++i) {
        free((void*)wargv[i]);
    }
    free((void*)wargv);
    return result;
#else
    return Py_Main(argc, argv);
#endif
}

