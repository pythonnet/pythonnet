#include "pynetclr.h"

#include "coreclrhost.h"
#include "coreutils.h"
#include "stdlib.h"

#ifndef _WIN32
#include "dirent.h"
#include "dlfcn.h"
#include "libgen.h"
#include "alloca.h"
#endif

#ifndef SUCCEEDED
#define SUCCEEDED(Status) ((Status) >= 0)
#endif // !SUCCEEDED

// Name of the environment variable controlling server GC.
// If set to 1, server GC is enabled on startup. If 0, server GC is
// disabled. Server GC is off by default.
static const char* serverGcVar = "CORECLR_SERVER_GC";

// initialize Core CLR and PythonNet
PyNet_Args *PyNet_Init(int ext)
{
    PyNet_Args *pn_args = (PyNet_Args *)malloc(sizeof(PyNet_Args));

    pn_args->pr_file = PR_ASSEMBLY;
    pn_args->assembly_name = ASSEMBLY_NAME;
    pn_args->class_name = CLASS_NAME;

    pn_args->error = NULL;
    pn_args->shutdown = NULL;
    pn_args->module = NULL;

    if (ext == 0)
    {
        pn_args->init_method_name = "Initialize";
    }
    else
    {
        pn_args->init_method_name = "InitExt";
    }

    pn_args->shutdown_method_name = "Shutdown";

    init(pn_args);

    if (pn_args->error != NULL)
    {
        PyErr_SetString(PyExc_ImportError, pn_args->error);
    }

    return pn_args;
}

// Shuts down PythonNet and cleans up Core CLR
void PyNet_Finalize(PyNet_Args *pn_args)
{
    // Indicates failure
    int exitCode = -1;

    // Call Python.Runtime.PythonEngine.Shutdown()
    if (pn_args->shutdown != NULL)
    {
        pn_args->shutdown();
    }

    // Shutdown Core CLR
    if (pn_args->core_clr_lib)
    {
        coreclr_shutdown_2_ptr shutdownCoreCLR = (coreclr_shutdown_2_ptr)dlsym(pn_args->core_clr_lib, "coreclr_shutdown_2");

        if (shutdownCoreCLR == NULL)
        {
            fprintf(stderr, "Function coreclr_shutdown_2 not found in the libcoreclr.so\n");
        }
        else if (pn_args->host_handle && pn_args->domain_id)
        {
            int latchedExitCode = 0;
            int st = shutdownCoreCLR(pn_args->host_handle, pn_args->domain_id, &latchedExitCode);
            if (!SUCCEEDED(st))
            {
                fprintf(stderr, "coreclr_shutdown failed - status: 0x%08x\n", st);
                exitCode = -1;
            }

            if (exitCode != -1)
            {
                exitCode = latchedExitCode;
            }
        }

        if (dlclose(pn_args->core_clr_lib) != 0)
        {
            fprintf(stderr, "Warning - dlclose failed\n");
        }
    }

   free(pn_args);
}

void init(PyNet_Args* pn_args)
{
#ifndef _WIN32
    //get python path system variable
    PyObject *syspath = PySys_GetObject("path");
    pn_args->assembly_path = malloc(PATH_MAX);
    const char *slash = "/";
    int found = 0;

    int ii;
    for (ii = 0; ii < PyList_Size(syspath); ++ii)
    {
#if PY_MAJOR_VERSION >= 3
        Py_ssize_t wlen;
        wchar_t *wstr = PyUnicode_AsWideCharString(PyList_GetItem(syspath, ii), &wlen);
        char *pydir = (char*)malloc(wlen + 1);
        size_t mblen = wcstombs(pydir, wstr, wlen + 1);
        if (mblen > wlen)
            pydir[wlen] = '\0';
        PyMem_Free(wstr);
#else
        const char *pydir = PyString_AsString(PyList_GetItem(syspath, ii));
#endif
        char *curdir = (char*) malloc(1024);
        strncpy(curdir, strlen(pydir) > 0 ? pydir : ".", 1024);
        strncat(curdir, slash, 1024);

#if PY_MAJOR_VERSION >= 3
        free(pydir);
#endif

        //look in this directory for the pn_args->pr_file
        DIR *dirp = opendir(curdir);
        if (dirp != NULL)
        {
            struct dirent *dp;
            while ((dp = readdir(dirp)) != NULL)
            {
                if (strcmp(dp->d_name, pn_args->pr_file) == 0)
                {
                    strcpy(pn_args->assembly_path, curdir);
                    found = 1;
                    break;
                }
            }
            closedir(dirp);
        }
        free(curdir);

        if (found)
        {
            break;
        }
    }

    if (!found)
    {
        fprintf(stderr, "Could not find assembly %s. \n", pn_args->pr_file);
        return;
    }
#endif

    if (!GetEntrypointExecutableAbsolutePath(&pn_args->entry_path))
    {
        pn_args->error = "Unable to find entry point";
        return;
    }

    if (!GetClrFilesAbsolutePath(pn_args->entry_path, "/usr/share/dotnet/shared/Microsoft.NETCore.App/2.0.0", &pn_args->clr_path))
    //if (!GetClrFilesAbsolutePath(pn_args->entry_path, NULL, &pn_args->clr_path)))
    {
        pn_args->error = "Unable to find clr path";
        return;
    }

    int st = createDelegates(pn_args);

    if (SUCCEEDED(st))
    {
        pn_args->module = pn_args->init();
    }

#ifndef _WIN32
    free(pn_args->assembly_path);
    free(pn_args->entry_path);
#endif
}

int createDelegates(PyNet_Args * pn_args)
{
    // Indicates failure
    int exitCode = -1;

#ifdef _ARM_
    // libunwind library is used to unwind stack frame, but libunwind for ARM
    // does not support ARM vfpv3/NEON registers in DWARF format correctly.
    // Therefore let's disable stack unwinding using DWARF information
    // See https://github.com/dotnet/coreclr/issues/6698
    //
    // libunwind use following methods to unwind stack frame.
    // UNW_ARM_METHOD_ALL          0xFF
    // UNW_ARM_METHOD_DWARF        0x01
    // UNW_ARM_METHOD_FRAME        0x02
    // UNW_ARM_METHOD_EXIDX        0x04
    putenv((char *)("UNW_ARM_UNWIND_METHOD=6"));
#endif // _ARM_

    char coreClrDllPath[strlen(pn_args->clr_path) + strlen(coreClrDll) + 2];
    strcpy(coreClrDllPath, pn_args->clr_path);
    strcat(coreClrDllPath, "/");
    strcat(coreClrDllPath, coreClrDll);

    if (strlen(coreClrDllPath) >= PATH_MAX)
    {
        fprintf(stderr, "Absolute path to libcoreclr.so too long\n");
        return -1;
    }

    // Get just the path component of the managed assembly path
    char* appPath = NULL;
    if (!GetDirectory(pn_args->assembly_path, &appPath))
    {
        return -1;
    }

    char* tpaList = NULL;
    if (pn_args->assembly_path != NULL)
    {
        // Target assembly should be added to the tpa list. Otherwise corerun.exe
        // may find wrong assembly to execute.
        // Details can be found at https://github.com/dotnet/coreclr/issues/5631
        tpaList = malloc(strlen(appPath) + strlen(pn_args->pr_file) + 3);

        if(tpaList == NULL)
        {
            perror("Could not allocate buffer");
            free(appPath);
            return -1;
        }

        strcpy(tpaList, appPath);
        strcat(tpaList, "/");
        strcat(tpaList, pn_args->pr_file);
        strcat(tpaList, ":");
    }

    // Construct native search directory paths
    char* nativeDllSearchDirs = malloc(strlen(appPath) + strlen(pn_args->clr_path) + 2);

    if (nativeDllSearchDirs == NULL)
    {
        perror("Could not allocate buffer");
        free(appPath);
        if (tpaList != NULL)
        {
            free(tpaList);
        }
        return -1;
    }

    strcpy(nativeDllSearchDirs, appPath);
    strcat(nativeDllSearchDirs, ":");
    strcat(nativeDllSearchDirs, pn_args->clr_path);

    const char *coreLibraries = getenv("CORE_LIBRARIES");
    if (coreLibraries)
    {
        nativeDllSearchDirs = realloc(nativeDllSearchDirs, strlen(coreLibraries) + 2);

        if (nativeDllSearchDirs == NULL)
        {
            perror("Could not reallocate buffer");
            free(appPath);
            if (tpaList != NULL)
            {
                free(tpaList);
            }
            return -1;
        }

        strcat(nativeDllSearchDirs, ":");
        strcat(nativeDllSearchDirs, coreLibraries);
        if (strcmp(coreLibraries, pn_args->clr_path) != 0)
        {
            AddFilesFromDirectoryToTpaList(coreLibraries, &tpaList);
        }
    }

    AddFilesFromDirectoryToTpaList(pn_args->clr_path, &tpaList);

    pn_args->core_clr_lib = dlopen(coreClrDllPath, RTLD_NOW | RTLD_LOCAL);

    if (pn_args->core_clr_lib != NULL)
    {
        coreclr_initialize_ptr initializeCoreCLR = (coreclr_initialize_ptr)dlsym(pn_args->core_clr_lib, "coreclr_initialize");
        coreclr_create_delegate_ptr createDelegate = (coreclr_create_delegate_ptr)dlsym(pn_args->core_clr_lib, "coreclr_create_delegate");

        if (initializeCoreCLR == NULL)
        {
            fprintf(stderr, "Function coreclr_initialize not found in the libcoreclr.so\n");
        }
        else if (createDelegate == NULL)
        {
            fprintf(stderr, "Function coreclr_create_delegate not found in the libcoreclr.so\n");
        }
        else
        {
            // Check whether we are enabling server GC (off by default)
            const char* useServerGc = GetEnvValueBoolean(serverGcVar);

            // Allowed property names:
            // APPBASE
            // - The base path of the application from which the exe and other assemblies will be loaded
            //
            // TRUSTED_PLATFORM_ASSEMBLIES
            // - The list of complete paths to each of the fully trusted assemblies
            //
            // APP_PATHS
            // - The list of paths which will be probed by the assembly loader
            //
            // APP_NI_PATHS
            // - The list of additional paths that the assembly loader will probe for ngen images
            //
            // NATIVE_DLL_SEARCH_DIRECTORIES
            // - The list of paths that will be probed for native DLLs called by PInvoke
            //
            const char *propertyKeys[] = {
                "TRUSTED_PLATFORM_ASSEMBLIES",
                "APP_PATHS",
                "APP_NI_PATHS",
                "NATIVE_DLL_SEARCH_DIRECTORIES",
                "System.GC.Server",
                "AppDomainCompatSwitch",
            };
            const char *propertyValues[] = {
                // TRUSTED_PLATFORM_ASSEMBLIES
                tpaList,
                // APP_PATHS
                appPath,
                // APP_NI_PATHS
                appPath,
                // NATIVE_DLL_SEARCH_DIRECTORIES
                nativeDllSearchDirs,
                // System.GC.Server
                useServerGc,
                // AppDomainCompatSwitch
                "UseLatestBehaviorWhenTFMNotSpecified"
            };

            pn_args->host_handle = NULL;
            pn_args->domain_id = 0;

            int st = initializeCoreCLR(
                        pn_args->entry_path,
                        "pythonnet",
                        sizeof(propertyKeys) / sizeof(propertyKeys[0]),
                        propertyKeys,
                        propertyValues,
                        &pn_args->host_handle,
                        &pn_args->domain_id);

            if (!SUCCEEDED(st))
            {
                fprintf(stderr, "coreclr_initialize failed - status: 0x%08x\n", st);
                exitCode = -1;
            }
            else
            {
                // Create init delegate
                st = createDelegate(
                        pn_args->host_handle,
                        pn_args->domain_id,
                        pn_args->assembly_name,
                        pn_args->class_name,
                        pn_args->init_method_name,
                        (void**)&pn_args->init);

                if (!SUCCEEDED(st))
                {
                    fprintf(stderr, "coreclr_create_delegate failed - status: 0x%08x\n", st);
                    exitCode = -1;
                }

                // Create shutdown delegate
                st = createDelegate(
                        pn_args->host_handle,
                        pn_args->domain_id,
                        pn_args->assembly_name,
                        pn_args->class_name,
                        pn_args->shutdown_method_name,
                        (void**)&pn_args->shutdown);

                if (!SUCCEEDED(st))
                {
                    fprintf(stderr, "coreclr_create_delegate failed - status: 0x%08x\n", st);
                    exitCode = -1;
                }
				else
				{
					exitCode = 0;
				}
            }
        }
    }
    else
    {
        const char* error = dlerror();
        fprintf(stderr, "dlopen failed to open the libcoreclr.so with error %s\n", error);
    }

    free(appPath);
    free(tpaList);
    free(nativeDllSearchDirs);

    return exitCode;
}
