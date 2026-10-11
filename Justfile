default:
    @just --choose

default_platform := if arch() == "x86_64" { "x64" } else if arch() == "aarch64" { "arm64" } else { arch() }

setup:
    dotnet restore
    uv sync

docs:
    doxygen doc/Doxyfile
    uv run --group doc sphinx-build doc/source/ ./doc/build/html/

build-wheels:
    uv build
    uv build --wheel -C="--global-option=--net46-support"

# Run default tests (.NET Core Python and embedding tests)
test: test-python-coreclr test-embed-core

# Run Python tests (default or pass options like --runtime coreclr)
test-python *args:
    uv run pytest {{ args }}

# Run Python tests with .NET Core
test-python-coreclr *args:
    uv run pytest --runtime coreclr {{ args }}

# Run Python tests with Mono
test-python-mono *args:
    uv run pytest --runtime mono {{ args }}

# Run Python tests with .NET Framework
test-python-netfx *args:
    uv run pytest --runtime netfx {{ args }}

# Run Python tests executed via .NET test runner
test-python-from-dotnet platform=default_platform *args:
    dotnet test --runtime any-{{ platform }} src/python_tests_runner/ {{ args }}

# Run embedding tests
test-embed runtime=("any-" + default_platform) *args:
    dotnet test --runtime {{ runtime }} src/embed_tests/ {{ args }}

# Run embedding tests with .NET Core
test-embed-core platform=default_platform *args:
    dotnet test --runtime any-{{ platform }} --framework net10.0 --logger "console;verbosity=detailed" src/embed_tests/ {{ args }}

# Run embedding tests with Mono / .NET Framework
[env("MONO_THREADS_SUSPEND", "preemptive")]
test-embed-mono platform=default_platform *args:
    dotnet test --runtime any-{{ platform }} --framework net472 --logger "console;verbosity=detailed" src/embed_tests/ {{ args }}
