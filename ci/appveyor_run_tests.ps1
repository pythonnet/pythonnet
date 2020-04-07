# Script to simplify AppVeyor configuration and resolve path to tools

$stopwatch = [Diagnostics.Stopwatch]::StartNew()
[array]$timings = @()

# Test Runner framework being used for embedded tests
$CS_RUNNER = "nunit3-console"

$XPLAT = $env:BUILD_OPTS -eq "--xplat"

# Needed for ARCH specific runners(NUnit2/XUnit3). Skip for NUnit3
if ($FALSE -and $env:PLATFORM -eq "x86"){
    $CS_RUNNER = $CS_RUNNER + "-x86"
}

# Executable paths for OpenCover
# Note if OpenCover fails, it won't affect the exit codes.
$OPENCOVER = Resolve-Path .\packages\OpenCover.*\tools\OpenCover.Console.exe
if ($XPLAT){
    $CS_RUNNER = Resolve-Path $env:USERPROFILE\.nuget\packages\nunit.consolerunner\*\tools\"$CS_RUNNER".exe
}
else{
    $CS_RUNNER = Resolve-Path .\packages\NUnit.*\tools\"$CS_RUNNER".exe
}
$PY = Get-Command python

# Can't use ".\build\*\Python.EmbeddingTest.dll". Missing framework files.
$CS_TESTS = ".\src\embed_tests\bin\Python.EmbeddingTest.dll"
$RUNTIME_DIR = ".\src\runtime\bin\"

function ReportTime {
    param([string] $action)

    $timeSpent = $stopwatch.Elapsed
    $timings += [pscustomobject]@{action=$action; timeSpent=$timeSpent}
    Write-Host $action " in " $timeSpent -ForegroundColor "Green"
    $stopwatch.Restart()
}

ReportTime "Preparation done"

# Run python tests with C# coverage
Write-Host ("Starting Python tests") -ForegroundColor "Green"
.$OPENCOVER -register:user -searchdirs:"$RUNTIME_DIR" -output:py.coverage `
            -target:"$PY" -targetargs:"-m pytest" `
            -returntargetcode
$PYTHON_STATUS = $LastExitCode
if ($PYTHON_STATUS -ne 0) {
    Write-Host "Python tests failed, continuing to embedded tests" -ForegroundColor "Red"
    ReportTime ""
} else {
    ReportTime "Python tests completed"
}

# Run Embedded tests with C# coverage
Write-Host ("Starting embedded tests") -ForegroundColor "Green"
.$OPENCOVER -register:user -searchdirs:"$RUNTIME_DIR" -output:cs.coverage `
            -target:"$CS_RUNNER" -targetargs:"$CS_TESTS --labels=All" `
            -filter:"+[*]Python.Runtime*" `
            -returntargetcode
$CS_STATUS = $LastExitCode
if ($CS_STATUS -ne 0) {
    Write-Host "Embedded tests failed" -ForegroundColor "Red"
    ReportTime ""
} else {
    ReportTime "Embedded tests completed"

    # NuGet for pythonnet-2.3 only has 64-bit binary for Python 3.5
    # the test is only built using modern stack
    if (($env:PLATFORM -eq "x64") -and ($XPLAT) -and ($env:PYTHON_VERSION -eq "3.5")) {
        # Run C# Performance tests
        Write-Host ("Starting performance tests") -ForegroundColor "Green"
        if ($XPLAT) {
            $CS_PERF_TESTS = ".\src\perf_tests\bin\net461\Python.PerformanceTests.dll"
        }
        else {
            $CS_PERF_TESTS = ".\src\perf_tests\bin\Python.PerformanceTests.dll"
        }
        &"$CS_RUNNER" "$CS_PERF_TESTS"
        $CS_PERF_STATUS = $LastExitCode
        if ($CS_PERF_STATUS -ne 0) {
            Write-Host "Performance tests (C#) failed" -ForegroundColor "Red"
            ReportTime ""
        } else {
            ReportTime "Performance tests (C#) completed"
        }
    } else {
        Write-Host ("Skipping performance tests for ", $env:PYTHON_VERSION) -ForegroundColor "Yellow"
        Write-Host ("on platform ", $env:PLATFORM, " xplat: ", $XPLAT) -ForegroundColor "Yellow"
        $CS_PERF_STATUS = 0
    }
}

if ($XPLAT){
    if ($env:PLATFORM -eq "x64") {
         $DOTNET_CMD = "dotnet"
    }
    else{
         $DOTNET_CMD = "c:\Program Files (x86)\dotnet\dotnet"
    }

    # Run Embedded tests for netcoreapp2.0 (OpenCover currently does not supports dotnet core)
    Write-Host ("Starting embedded tests for netcoreapp2.0") -ForegroundColor "Green"
    &$DOTNET_CMD ".\src\embed_tests\bin\netcoreapp2.0_publish\Python.EmbeddingTest.dll"
    $CS_STATUS = $LastExitCode
    if ($CS_STATUS -ne 0) {
        Write-Host "Embedded tests for netcoreapp2.0 failed" -ForegroundColor "Red"
        ReportTime ""
    } else {
        ReportTime ".NET Core 2.0 tests completed"
    }
}

Write-Host "Timings:" ($timings | Format-Table | Out-String) -ForegroundColor "Green"

# Set exit code to fail if either Python or Embedded tests failed
if ($PYTHON_STATUS -ne 0 -or $CS_STATUS -ne 0 -or $CS_PERF_STATUS -ne 0) {
    Write-Host "Tests failed" -ForegroundColor "Red"
    $host.SetShouldExit(1)
}
