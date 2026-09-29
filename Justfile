default:
    @just --choose

setup:
    dotnet restore
    uv sync

docs:
    doxygen doc/Doxyfile
    uv run --group doc sphinx-build doc/source/ ./doc/build/html/

build-wheels:
    uv build
    uv build --wheel -C="--global-option=--net46-support"