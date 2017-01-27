if ($env:APPVEYOR_PULL_REQUEST_NUMBER) {
    Invoke-Expression .\ci\install_miniconda.ps1
    &"$env:CONDA_BLD\Scripts\conda" build conda.recipe --dirty -q
    $CONDA_PKG=(&"$env:CONDA_BLD\Scripts\conda" build conda.recipe --output -q)
    Copy-Item $CONDA_PKG "$env:APPVEYOR_BUILD_FOLDER\dist\"
}
