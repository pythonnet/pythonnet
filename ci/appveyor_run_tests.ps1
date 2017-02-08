# Script to simplify appveyor configuration and resolve path to tools

# Executable paths for OpenCover
# Note if OpenCover fails, it won't affect the exit codes.
$OPENCOVER = Resolve-Path .\packages\OpenCover.*\tools\OpenCover.Console.exe
$NUNIT = Resolve-Path .\packages\NUnit.Runners*\tools\"$env:NUNIT".exe
$PY = Get-Command python

# Can't use ".\build\*\Python.EmbeddingTest.dll". Missing framework files.
$CS_TESTS = ".\src\embed_tests\bin\Python.EmbeddingTest.dll"
$RUNTIME_DIR = ".\src\runtime\bin\"

# Run python tests with C# coverage
# why `2>&1 | %{ "$_" }`? see: http://stackoverflow.com/a/20950421/5208670
.$OPENCOVER -register:user -searchdirs:"$RUNTIME_DIR" -output:py.coverage -target:"$PY" -targetargs:src\tests\runtests.py -returntargetcode 2>&1 | %{ "$_" }
$PYTHON_STATUS = $LastExitCode
if ($PYTHON_STATUS -ne 0) {
    Write-Host "Python tests failed, continuing to embedded tests" -ForegroundColor "Red"
}

# Run Embedded tests with C# coverage
.$OPENCOVER -register:user -searchdirs:"$RUNTIME_DIR" -output:cs.coverage -target:"$NUNIT" -targetargs:"$CS_TESTS" -returntargetcode
$NUNIT_STATUS = $LastExitCode
if ($NUNIT_STATUS -ne 0) {
    Write-Host "Embedded tests failed" -ForegroundColor "Red"
}

# Embedded tests failing due to open issues, pass/fail only on Python exit code
if ($PYTHON_STATUS -ne 0 -or $NUNIT_STATUS -ne 0) {
    Write-Host "Tests failed" -ForegroundColor "Red"
    $host.SetShouldExit(1)
}
