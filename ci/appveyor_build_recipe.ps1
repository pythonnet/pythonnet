# Build `conda.recipe` only if this is a Pull_Request. Saves time for CI.

$stopwatch = [Diagnostics.Stopwatch]::StartNew()

$env:CONDA_PY = "$env:PY_VER"
# Use pre-installed miniconda. Note that location differs if 64bit
$env:CONDA_BLD = "C:\miniconda36"

if ($env:PLATFORM -eq "x86"){
    $env:CONDA_BLD_ARCH=32
} else {
    $env:CONDA_BLD_ARCH=64
    $env:CONDA_BLD = "$env:CONDA_BLD" + "-x64"
}

if ($env:APPVEYOR_PULL_REQUEST_NUMBER -or $env:APPVEYOR_REPO_TAG_NAME -or $env:FORCE_CONDA_BUILD -eq "True") {
    # Update PATH, and keep a copy to restore at end of this PowerShell script
    $old_path = $env:path
    $env:path = "$env:CONDA_BLD;$env:CONDA_BLD\Scripts;" + $env:path

    Write-Host "Starting conda install" -ForegroundColor "Green"
    conda config --set always_yes True
    conda config --set changeps1 False
    conda config --set auto_update_conda False
    conda install conda-build jinja2 anaconda-client --quiet
    conda info

    # why `2>&1 | %{ "$_" }`? Redirect STDERR to STDOUT
    # see: http://stackoverflow.com/a/20950421/5208670
    Write-Host "Starting conda build recipe" -ForegroundColor "Green"
    conda build conda.recipe --quiet 2>&1 | %{ "$_" }

    $CONDA_PKG=(conda build conda.recipe --output)
    Copy-Item $CONDA_PKG .\dist\

    $timeSpent = $stopwatch.Elapsed
    Write-Host "Completed conda build recipe in " $timeSpent -ForegroundColor "Green"

    # Restore PATH back to original
    $env:path = $old_path
} else {
    Write-Host "Skipping conda build recipe" -ForegroundColor "Green"
}
