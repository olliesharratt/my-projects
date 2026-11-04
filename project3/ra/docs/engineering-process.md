# Engineering Process

## Configured Automation

- The GitHub Actions CI workflow runs the .NET tests with coverage on pull requests and pushes to `main`. On `main`, it publishes a Docker image to GitHub Container Registry after tests pass.
- CodeQL analyzes C# on pull requests, pushes to `main`, and weekly.
- Dependabot checks NuGet, GitHub Actions, and Docker dependencies weekly.
- `.editorconfig` defines formatting preferences.

These are workflow files in the repository. Branch protection, required status checks, and a GitHub Projects board are repository settings and must be enabled separately; their status cannot be established from these files.

## Suggested Repository Settings

- Protect `main` and require pull requests.
- Require the CI test job and CodeQL status checks before merging.
- Enable Dependabot security alerts.
- Create a project board if a visual backlog is useful.
