# Build `conda.recipe` only if this is a Pull_Request. Saves time for CI.

if ($env:APPVEYOR_PULL_REQUEST_NUMBER) {
    # Update PATH, and keep a copy to restore at end of this PowerShell script
    $old_path = $env:path
    $env:path = "$env:CONDA_BLD;$env:CONDA_BLD\Scripts;" + $env:path

    Write-Host "Starting conda install" -ForegroundColor "Green"
    conda config --set always_yes True
    conda config --set changeps1 False
    conda install conda-build jinja2 anaconda-client --quiet

    Write-Host "Starting conda build recipe" -ForegroundColor "Green"
    conda build conda.recipe --dirty --quiet

    $CONDA_PKG=(conda build conda.recipe --output)
    Copy-Item $CONDA_PKG .\dist\
    Write-Host "Completed conda build recipe" -ForegroundColor "Green"

    # Restore PATH back to original
    $env:path = $old_path
} else {
    Write-Host "Skipping conda build recipe" -ForegroundColor "Green"
}
