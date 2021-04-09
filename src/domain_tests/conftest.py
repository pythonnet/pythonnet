import os

from subprocess import check_call
# test_proj_path = os.path.join(cwd, "..", "testing")
cfd = os.path.dirname(__file__)
bin_path = os.path.join(cfd, 'bin')
check_call(["dotnet", "build", cfd, '-o', bin_path])