# Script to simplify appveyor configuration and resolve path to tools

# Executable paths for OpenCover
# Note if OpenCover fails, it won't affect the exit codes.
$OPENCOVER = Resolve-Path .\packages\OpenCover.*\tools\OpenCover.Console.exe
$NUNIT = Resolve-Path .\packages\NUnit.*\tools\"$env:NUNIT".exe
$PY = Get-Command python

# Can't use ".\build\*\Python.EmbeddingTest.dll". Missing framework files.
$CS_TESTS = ".\src\embed_tests\bin\Python.EmbeddingTest.dll"
$RUNTIME_DIR = ".\src\runtime\bin\"

# Run python tests with C# coverage
# why `2>&1 | %{ "$_" }`? see: http://stackoverflow.com/a/20950421/5208670
Write-Host ("Starting Python tests") -ForegroundColor "Green"
.$OPENCOVER -register:user -searchdirs:"$RUNTIME_DIR" -output:py.coverage `
            -target:"$PY" -targetargs:"-m pytest" `
            -returntargetcode `
            2>&1 | %{ "$_" }
$PYTHON_STATUS = $LastExitCode
if ($PYTHON_STATUS -ne 0) {
    Write-Host "Python tests failed, continuing to embedded tests" -ForegroundColor "Red"
}

# Run Embedded tests with C# coverage
# Powershell continuation: http://stackoverflow.com/a/2608186/5208670
# Powershell options splatting: http://stackoverflow.com/a/24313253/5208670
Write-Host ("Starting embedded tests") -ForegroundColor "Green"
.$OPENCOVER -register:user -searchdirs:"$RUNTIME_DIR" -output:cs.coverage `
            -target:"$NUNIT" -targetargs:"$CS_TESTS" `
            -filter:"+[*]Python.Runtime*" `
            -returntargetcode
$NUNIT_STATUS = $LastExitCode
if ($NUNIT_STATUS -ne 0) {
    Write-Host "Embedded tests failed" -ForegroundColor "Red"
}

# Set exit code to fail if either Python or Embedded tests failed
if ($PYTHON_STATUS -ne 0 -or $NUNIT_STATUS -ne 0) {
    Write-Host "Tests failed" -ForegroundColor "Red"
    $host.SetShouldExit(1)
}
