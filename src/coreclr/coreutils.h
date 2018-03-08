#ifndef CORE_RUN_COMMON_H
#define CORE_RUN_COMMON_H

#include <stdbool.h>

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// https://github.com/dotnet/coreclr/blob/master/src/coreclr/hosts/unixcoreruncommon/coreruncommon.h

// Get the path to entrypoint executable
bool GetEntrypointExecutableAbsolutePath(char** entrypointExecutable);

// Get absolute path from the specified path.
// Return true in case of a success, false otherwise.
bool GetAbsolutePath(const char* path, char** absolutePath);

// Get directory of the specified path.
// Return true in case of a success, false otherwise.
bool GetDirectory(const char* absolutePath, char** directory);

//
// Get the absolute path to use to locate libcoreclr.so and the CLR assemblies are stored. If clrFilesPath is provided,
// this function will return the absolute path to it. Otherwise, the directory of the current executable is used.
//
// Return true in case of a success, false otherwise.
//
bool GetClrFilesAbsolutePath(const char* currentExePath, const char* clrFilesPath, char** clrFilesAbsolutePath);

// Check if the provided assembly is already included in the list of assemblies.
// Return true if present, false otherwise.
bool AssemblyAlreadyPresent(const char* addedAssemblies, const char* filenameWithoutExt);

// Add all *.dll, *.ni.dll, *.exe, and *.ni.exe files from the specified directory to the tpaList string.
void AddFilesFromDirectoryToTpaList(const char* directory, char** tpaList);

const char* GetEnvValueBoolean(const char* envVariable);

#if defined(__APPLE__)
#include <mach-o/dyld.h>
static const char * const coreClrDll = "libcoreclr.dylib";
#else
static const char * const coreClrDll = "libcoreclr.so";
#endif

#endif // CORE_RUN_COMMON_H
