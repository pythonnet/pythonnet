# How to contribute

Python.NET is developed and maintained by unpaid community members so well
written, documented and tested pull requests are encouraged.

By submitting a pull request for this project, you agree to license your
contribution under the MIT license to this project.

This project has adopted the code of conduct defined by the Contributor
Covenant to clarify expected behavior in our community. For more information
see the [.NET Foundation Code of Conduct](https://dotnetfoundation.org/code-of-conduct).

## Getting Started

-   Make sure you have a [GitHub account](https://github.com/signup/free)
-   Submit a ticket for your issue, assuming one does not already exist.
    -   Clearly describe the issue including steps to reproduce the bug.
    -   Include what Python version and operating system you are using.
-   Fork the repository on GitHub

## Making Changes

-   Create a topic branch from where you want to base your work.
    -   This is usually the master branch.
    -   Only target release branches if you are certain your fix must be on
        that branch.
    -   To quickly create a topic branch based on master;
        `git checkout -b fix/develop/my_contribution master`.
        Please avoid working directly on the `master` branch for anything
        other than trivial changes.
-   Make commits of logical units.
-   Check for unnecessary whitespace with `git diff --check` before committing.
-   Make sure your commit messages are in the proper format.
-   Make sure you have added the necessary tests for your changes.
-   Run _all_ the tests to assure nothing else was accidentally broken.

## Submitting Changes

-   Merge the topic branch into master and push to your fork of the repository.
-   Submit a pull request to the repository in the pythonnet organization.
-   After feedback has been given we expect responses within two weeks. After
    two weeks we may close the pull request if it isn't showing any activity.

# Additional Resources

-   [General GitHub documentation](https://help.github.com/)
-   [GitHub pull request documentation](https://help.github.com/send-pull-requests/)
-   [.NET Foundation Code of Conduct](https://dotnetfoundation.org/about/code-of-conduct)
