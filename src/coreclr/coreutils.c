// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// Code that is used by both the Unix corerun and coreconsole.
//

// https://github.com/dotnet/coreclr/blob/master/src/coreclr/hosts/unixcoreruncommon/coreruncommon.cpp

#include <assert.h>
#include <ctype.h>
#include <dirent.h>
#include <dlfcn.h>
#include <limits.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>
#if defined(__FreeBSD__)
#include <sys/types.h>
#include <sys/param.h>
#endif
#if defined(HAVE_SYS_SYSCTL_H) || defined(__FreeBSD__)
#include <sys/sysctl.h>
#endif
#include <unistd.h>
#include "coreutils.h"
#ifndef SUCCEEDED
#define SUCCEEDED(Status) ((Status) >= 0)
#endif // !SUCCEEDED

#if defined(__linux__)
#define symlinkEntrypointExecutable "/proc/self/exe"
#elif !defined(__APPLE__)
#define symlinkEntrypointExecutable "/proc/curproc/exe"
#endif

bool GetEntrypointExecutableAbsolutePath(char** entrypointExecutable)
{
    bool result = false;

    // Get path to the executable for the current process using
    // platform specific means.
#if defined(__APPLE__)

    char path[PATH_MAX];
    // On Mac, we ask the OS for the absolute path to the entrypoint executable
    size_t lenActualPath = sizeof(path);
    if (_NSGetExecutablePath(path, &lenActualPath) == 0)
    {
        char* buf = strdup(path);
        if (buf == NULL)
        {
            perror("Could not allocate buffer for path");
            return false;
        }
        result = true;
    }
    else
    {
        fprintf(stderr, "Path too long\n");
        return result;
    }
#elif defined (__FreeBSD__)
    static const int name[] = {
        CTL_KERN, KERN_PROC, KERN_PROC_PATHNAME, -1
    };
    char path[PATH_MAX];
    size_t len;

    len = sizeof(path);
    if (sysctl(name, 4, path, &len, nullptr, 0) == 0)
    {
        char* buf = strdup(path);
        if (buf == NULL)
        {
            perror("Could not allocate buffer for path");
            return false;
        }
        result = true;
    }
    else
    {
        // ENOMEM
        result = false;
    }
#elif defined(__NetBSD__) && defined(KERN_PROC_PATHNAME)
    static const int name[] = {
        CTL_KERN, KERN_PROC_ARGS, -1, KERN_PROC_PATHNAME,
    };
    char path[MAXPATHLEN];
    size_t len;

    len = sizeof(path);
    if (sysctl(name, __arraycount(name), path, &len, NULL, 0) != -1)
    {
        char* buf = strdup(path);
        if (buf == NULL)
        {
            perror("Could not allocate buffer for path");
            return false;
        }
        result = true;
    }
    else
    {
        result = false;
    }
#else
    // On other OSs, return the symlink that will be resolved by GetAbsolutePath
    // to fetch the entrypoint EXE absolute path, inclusive of filename.
    result = GetAbsolutePath(symlinkEntrypointExecutable, entrypointExecutable);
#endif

    return result;
}

bool GetAbsolutePath(const char* path, char** absolutePath)
{
    bool result = false;

    char realPath[PATH_MAX];
    if (realpath(path, realPath) != NULL && realPath[0] != '\0')
    {
        // realpath should return canonicalized path without the trailing slash
        assert((realPath)[strlen(realPath)-1] != '/');

        *absolutePath = strdup(realPath);
        if (*absolutePath == NULL)
        {
            perror("Could not allocate buffer for path");
            return false;
        }

        result = true;
    }

    return result;
}

bool GetDirectory(const char* absolutePath, char** directory)
{
    *directory = strdup(absolutePath);
    if (*directory == NULL)
    {
        perror("Could not allocate buffer for path");
        return false;
    }

    size_t len = strlen(*directory);
    if((*directory)[len-1] == '/')
    {
        (*directory)[len-1] = '\0';
        return true;
    }

    return false;
}

bool GetClrFilesAbsolutePath(const char* currentExePath, const char* clrFilesPath, char** clrFilesAbsolutePath)
{
    char* clrFilesRelativePath = NULL;
    const char* clrFilesPathLocal = clrFilesPath;
    if (clrFilesPathLocal == NULL)
    {
        // There was no CLR files path specified, use the folder of the current exe
        if (!GetDirectory(currentExePath, &clrFilesRelativePath))
        {
            fprintf(stderr, "Failed to get directory\n");
            return false;
        }

        clrFilesPathLocal = clrFilesRelativePath;

        // TODO: consider using an env variable (if defined) as a fall-back.
        // The windows version of the corerun uses core_root env variable
    }

    if (!GetAbsolutePath(clrFilesPathLocal, clrFilesAbsolutePath))
    {
        fprintf(stderr, "Failed to convert CLR files path to absolute path\n");
        return false;
    }

    return true;
}

bool AssemblyAlreadyPresent(const char* addedAssemblies, const char* filenameWithoutExt)
{
    // Copy buffer as strtok munges input
    char buf[strlen(addedAssemblies) + 1];
    strcpy(buf, addedAssemblies);
    const char* token = strtok(buf, ":");

    while (token != NULL)
    {
        if (strcmp(token, filenameWithoutExt) == 0)
        {
            return true;
        }
        token = strtok(NULL, ":");
    }

    return false;
}

void AddFilesFromDirectoryToTpaList(const char* directory, char** tpaList)
{
    const char * const tpaExtensions[] = {
                ".ni.dll",      // Probe for .ni.dll first so that it's preferred if ni and il coexist in the same dir
                ".dll",
                ".ni.exe",
                ".exe",
                };

    DIR* dir = opendir(directory);
    if (dir == NULL)
    {
        return;
    }

    // Initially empty string
    char* addedAssemblies = malloc(1);
    if (addedAssemblies == NULL)
    {
        perror("Could not allocate buffer");
        closedir(dir);
        return;
    }

    addedAssemblies[0] = '\0';

    // Walk the directory for each extension separately so that we first get files with .ni.dll extension,
    // then files with .dll extension, etc.
    size_t extIndex;
    for (extIndex = 0; extIndex < sizeof(tpaExtensions) / sizeof(tpaExtensions[0]); extIndex++)
    {
        const char* ext = tpaExtensions[extIndex];
        size_t extLength = strlen(ext);

        struct dirent* entry;

        // For all entries in the directory
        while ((entry = readdir(dir)) != NULL)
        {
            // We are interested in files only
            switch (entry->d_type)
            {
            case DT_REG:
                break;

            // Handle symlinks and file systems that do not support d_type
            case DT_LNK:
            case DT_UNKNOWN:
                {
                    char fullFilename[strlen(directory) + strlen(entry->d_name) + 2];
                    strcpy(fullFilename, directory);
                    strcat(fullFilename, "/");
                    strcat(fullFilename, entry->d_name);

                    struct stat sb;
                    if (stat(fullFilename, &sb) == -1)
                    {
                        continue;
                    }

                    if (!S_ISREG(sb.st_mode))
                    {
                        continue;
                    }
                }
                break;

            default:
                continue;
            }

            const char* filename = entry->d_name;

            // Check if the extension matches the one we are looking for
            int extPos = strlen(filename) - extLength;
            const char* extLoc = filename + extPos;
            if ((extPos <= 0) || (strncmp(extLoc, ext, extLength) != 0))
            {
                continue;
            }

            char filenameWithoutExt[strlen(filename) - extLength + 1];
            strncpy(filenameWithoutExt, filename, extPos);
            filenameWithoutExt[extPos] = '\0';

            // Make sure if we have an assembly with multiple extensions present,
            // we insert only one version of it.
            if (!AssemblyAlreadyPresent(addedAssemblies, filenameWithoutExt))
            {
                char* buf = realloc(
                    addedAssemblies,
                    strlen(addedAssemblies) + strlen(filenameWithoutExt) + 2);
                if (buf == NULL)
                {
                    perror("Could not reallocate buffer");
                    closedir(dir);
                    return;
                }
                addedAssemblies = buf;

                strcat(addedAssemblies, filenameWithoutExt);
                strcat(addedAssemblies, ":");

                buf = realloc(
                    *tpaList,
                    strlen(*tpaList) + strlen(directory) + strlen(filename) + 3);
                if (buf == NULL)
                {
                    perror("Could not reallocate buffer");
                    free(addedAssemblies);
                    closedir(dir);
                    return;
                }
                *tpaList = buf;

                strcat(*tpaList, directory);
                strcat(*tpaList, "/");
                strcat(*tpaList, filename);
                strcat(*tpaList, ":");
            }
        }

        // Rewind the directory stream to be able to iterate over it for the next extension
        rewinddir(dir);
    }

    free(addedAssemblies);

    closedir(dir);
}


const char* GetEnvValueBoolean(const char* envVariable)
{
    const char* envValue = getenv(envVariable);
    if (envValue == NULL)
    {
        envValue = "0";
    }

    // CoreCLR expects strings "true" and "false" instead of "1" and "0".
    if (strcmp(envValue, "1") == 0)
    {
        return "true";
    }
    else
    {
        // Try again with lowercase
        char* value = strdup(envValue);
        if (value == NULL)
        {
            perror("Could not allocate buffer");
            return "false";
        }

        for (; *value; ++value) *value = tolower(*value);

        if (strcmp(value, "true") == 0)
        {
            return "true";
        }
    }

    return "false";
}
