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

int main(int argc, char **argv) {
    return Py_Main(argc, argv);
}

