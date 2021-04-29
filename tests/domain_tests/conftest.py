import os

def pytest_addoption(parser):
    try:
        parser.addoption(
            "--runtime",
            action="store",
            default="default",
            help="Must be one of default, netcore, netfx and mono"
        )
    except ValueError:
        pass # already added


collect_ignore = []

def pytest_configure(config):
    if config.getoption("--runtime") == "netcore":
        collect_ignore.append("test_domain_reload.py")
        return

    from subprocess import check_call
    # test_proj_path = os.path.join(cwd, "..", "testing")
    cfd = os.path.dirname(__file__)
    bin_path = os.path.join(cfd, 'bin')
    check_call(["dotnet", "build", cfd, '-o', bin_path])
