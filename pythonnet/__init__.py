"""Python.NET runtime loading and configuration"""

import sys
from pathlib import Path
from typing import Dict, Optional, Union, Any
import clr_loader

__all__ = ["set_runtime", "set_runtime_from_env", "load", "unload", "get_runtime_info"]

_RUNTIME: Optional[clr_loader.Runtime] = None
_LOADER_ASSEMBLY: Optional[clr_loader.Assembly] = None
_LOADED: bool = False


def set_runtime(runtime: Union[clr_loader.Runtime, str], **params: str) -> None:
    """Set up a clr_loader runtime without loading it

    :param runtime:
        Either an already initialised `clr_loader` runtime, or one of netfx,
        coreclr, mono, or default. If a string parameter is given, the runtime
        will be created.
    """

    global _RUNTIME
    if _LOADED:
        raise RuntimeError(f"The runtime {_RUNTIME} has already been loaded")

    if isinstance(runtime, str):
        runtime = _create_runtime_from_spec(runtime, params)

    _RUNTIME = runtime


def get_runtime_info() -> Optional[clr_loader.RuntimeInfo]:
    """Retrieve information on the configured runtime"""

    if _RUNTIME is None:
        return None
    else:
        return _RUNTIME.info()


def _get_params_from_env(prefix: str) -> Dict[str, str]:
    from os import environ

    full_prefix = f"PYTHONNET_{prefix.upper()}_"
    len_ = len(full_prefix)

    env_vars = {
        (k[len_:].lower()): v
        for k, v in environ.items()
        if k.upper().startswith(full_prefix)
    }

    return env_vars


def _create_runtime_from_spec(
    spec: str, params: Optional[Dict[str, Any]] = None
) -> clr_loader.Runtime:
    was_default = False
    if spec == "default":
        was_default = True
        if sys.platform == "win32":
            spec = "netfx"
        else:
            spec = "mono"

    params = params or _get_params_from_env(spec)

    try:
        if spec == "netfx":
            return clr_loader.get_netfx(**params)
        elif spec == "mono":
            return clr_loader.get_mono(**params)
        elif spec == "coreclr":
            return clr_loader.get_coreclr(**params)
        else:
            raise RuntimeError(f"Invalid runtime name: '{spec}'")
    except Exception as exc:
        if was_default:
            raise RuntimeError(
                f"""Failed to create a default .NET runtime, which would
                    have been "{spec}" on this system. Either install a
                    compatible runtime or configure it explicitly via
                    `set_runtime` or the `PYTHONNET_*` environment variables
                    (see set_runtime_from_env)."""
            ) from exc
        else:
            raise RuntimeError(
                f"""Failed to create a .NET runtime ({spec}) using the
                parameters {params}."""
            ) from exc


def set_runtime_from_env() -> None:
    """Set up the runtime using the environment

    This will use the environment variable PYTHONNET_RUNTIME to decide the
    runtime to use, which may be one of netfx, coreclr or mono. The parameters
    of the respective clr_loader.get_<runtime> functions can also be given as
    environment variables, named `PYTHONNET_<RUNTIME>_<PARAM_NAME>`. In
    particular, to use `PYTHONNET_RUNTIME=coreclr`, the variable
    `PYTHONNET_CORECLR_RUNTIME_CONFIG` has to be set to a valid
    `.runtimeconfig.json`.

    If no environment variable is specified, a globally installed Mono is used
    for all environments but Windows, on Windows the legacy .NET Framework is
    used.
    """
    from os import environ

    spec = environ.get("PYTHONNET_RUNTIME", "default")
    runtime = _create_runtime_from_spec(spec)
    set_runtime(runtime)


def load(runtime: Union[clr_loader.Runtime, str, None] = None, **params: str) -> None:
    """Load Python.NET in the specified runtime

    The same parameters as for `set_runtime` can be used. By default,
    `set_default_runtime` is called if no environment has been set yet and no
    parameters are passed.

    After a successful call, further invocations will return immediately."""
    global _LOADED, _LOADER_ASSEMBLY

    if _LOADED:
        return

    if _RUNTIME is None:
        if runtime is None:
            set_runtime_from_env()
        else:
            set_runtime(runtime, **params)

    if _RUNTIME is None:
        raise RuntimeError("No valid runtime selected")

    dll_path = Path(__file__).parent / "runtime" / "Python.Runtime.dll"

    _LOADER_ASSEMBLY = assembly = _RUNTIME.get_assembly(str(dll_path))
    func = assembly.get_function("Python.Runtime.Loader.Initialize")

    if func(b"") != 0:
        raise RuntimeError("Failed to initialize Python.Runtime.dll")
    
    _LOADED = True

    import atexit

    atexit.register(unload)


def unload() -> None:
    """Explicitly unload a loaded runtime and shut down Python.NET"""

    global _RUNTIME, _LOADER_ASSEMBLY
    if _LOADER_ASSEMBLY is not None:
        func = _LOADER_ASSEMBLY.get_function("Python.Runtime.Loader.Shutdown")
        if func(b"full_shutdown") != 0:
            raise RuntimeError("Failed to call Python.NET shutdown")

        _LOADER_ASSEMBLY = None

    if _RUNTIME is not None:
        _RUNTIME.shutdown()
        _RUNTIME = None
