import os
#os.environ["MONO_LOG_LEVEL"] = "debug"
# os.environ["MONO_LOG_MASK"] = "cfg,dll"
os.environ["COREHOST_DEBUG"] = "1"
os.environ["COREHOST_TRACE"] = "1"
os.environ["COREHOST_TRACE_VERBOSITY"] = "4"

import pythonnet, clr_loader

import sys

if sys.argv[1] == "mono":
    mono = clr_loader.get_mono()
    rt = mono
elif sys.argv[1] == "core":
    core = clr_loader.get_coreclr("/home/benedikt/git/clr-loader/example/out/example.runtimeconfig.json")
    rt = core

pythonnet.set_runtime(rt)
pythonnet.load()

print("Loaded pythonnet")

import clr
from System import Console
Console.WriteLine("Success")
