if ($env:APPVEYOR_PULL_REQUEST_NUMBER) {
    # Update PATH, and keep a copy to restore at end of this PowerShell script
    $old_path = $env:path
    $env:path = "$env:CONDA_BLD;$env:CONDA_BLD\Scripts;" + $env:path

    Write-Host "Starting Conda Update/Install" -ForegroundColor "Green"
    conda update conda -q -y
    conda install conda-build jinja2 anaconda-client -q -y

    Write-Host "Starting Conda Recipe build" -ForegroundColor "Green"
    conda build conda.recipe -q --dirty

    $CONDA_PKG=(conda build conda.recipe -q --output)
    Copy-Item $CONDA_PKG "$env:APPVEYOR_BUILD_FOLDER\dist\"
    Write-Host "Completed Conda Recipe build" -ForegroundColor "Green"

    # Restore PATH back to original
    $env:path = $old_path
} else {
    Write-Host "Skipping Conda Recipe build" -ForegroundColor "Green"
}
