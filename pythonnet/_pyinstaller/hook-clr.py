from PyInstaller.utils.hooks import collect_data_files, collect_dynamic_libs

try:
    binaries = collect_dynamic_libs("pythonnet")
    datas = collect_data_files("pythonnet")
except Exception:
    # name conflict with https://pypi.org/project/clr/, do not crash if just clr is present
    binaries = []
    datas = []
