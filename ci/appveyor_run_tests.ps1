# Script to simplify AppVeyor configuration and resolve path to tools

# Test Runner framework being used for embedded tests
$CS_RUNNER = "nunit3-console"

# Needed for ARCH specific runners(NUnit2/XUnit3). Skip for NUnit3
if ($FALSE -and $env:PLATFORM -eq "x86"){
    $CS_RUNNER = $CS_RUNNER + "-x86"
}

# Executable paths for OpenCover
# Note if OpenCover fails, it won't affect the exit codes.
$OPENCOVER = Resolve-Path .\packages\OpenCover.*\tools\OpenCover.Console.exe
if ($env:BUILD_OPTS -eq "--xplat"){
    $CS_RUNNER = Resolve-Path $env:USERPROFILE\.nuget\packages\nunit.consolerunner\*\tools\"$CS_RUNNER".exe
}
else{
    $CS_RUNNER = Resolve-Path .\packages\NUnit.*\tools\"$CS_RUNNER".exe
}
$PY = Get-Command python

# Can't use ".\build\*\Python.EmbeddingTest.dll". Missing framework files.
$CS_TESTS = ".\src\embed_tests\bin\Python.EmbeddingTest.dll"
$RUNTIME_DIR = ".\src\runtime\bin\"

# Run python tests with C# coverage
Write-Host ("Starting Python tests") -ForegroundColor "Green"
.$OPENCOVER -register:user -searchdirs:"$RUNTIME_DIR" -output:py.coverage `
            -target:"$PY" -targetargs:"-m pytest" `
            -returntargetcode
$PYTHON_STATUS = $LastExitCode
if ($PYTHON_STATUS -ne 0) {
    Write-Host "Python tests failed, continuing to embedded tests" -ForegroundColor "Red"
}

# Run Embedded tests with C# coverage
Write-Host ("Starting embedded tests") -ForegroundColor "Green"
.$OPENCOVER -register:user -searchdirs:"$RUNTIME_DIR" -output:cs.coverage `
            -target:"$CS_RUNNER" -targetargs:"$CS_TESTS" `
            -filter:"+[*]Python.Runtime*" `
            -returntargetcode
$CS_STATUS = $LastExitCode
if ($CS_STATUS -ne 0) {
    Write-Host "Embedded tests failed" -ForegroundColor "Red"
}

if ($env:BUILD_OPTS -eq "--xplat"){
    if ($env:PLATFORM -eq "x64") {
         $DOTNET_CMD = "dotnet"
    }
    else{
         $DOTNET_CMD = "c:\Program Files (x86)\dotnet\dotnet"
    }

    # Run Embedded tests for netcoreapp2.0 (OpenCover currently does not supports dotnet core)
    Write-Host ("Starting embedded tests for netcoreapp2.0") -ForegroundColor "Green"
    &$DOTNET_CMD .\src\embed_tests\bin\netcoreapp2.0_publish\Python.EmbeddingTest.dll
    $CS_STATUS = $LastExitCode
    if ($CS_STATUS -ne 0) {
        Write-Host "Embedded tests for netcoreapp2.0 failed" -ForegroundColor "Red"
    }
}

# Set exit code to fail if either Python or Embedded tests failed
if ($PYTHON_STATUS -ne 0 -or $CS_STATUS -ne 0) {
    Write-Host "Tests failed" -ForegroundColor "Red"
    $host.SetShouldExit(1)
}
