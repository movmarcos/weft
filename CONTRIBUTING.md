# Contributing to Weft

Thanks for your interest. A few ground rules:

## Development setup

1. Install .NET 10 SDK (`brew install --cask dotnet-sdk` on macOS).
2. Clone + restore:
   ```bash
   git clone https://github.com/marcosmagri/weft.git
   cd weft
   dotnet restore
   ```
3. Build + test:
   ```bash
   dotnet build -warnaserror
   dotnet test
   ```
   All must pass with 0 warnings before a PR lands.

## Commit messages

We use [Conventional Commits](https://www.conventionalcommits.org/) because `release-please` generates the changelog from them:

- `feat(core): add XYZ` → shows up as a Feature in the next minor release.
- `fix(cli): correct ABC` → Bug Fix in the next patch release.
- `docs: update quickstart` → Documentation section.
- `chore`, `test`, `refactor`, `perf` also accepted.

A single commit can close the plan task; a PR can bundle multiple.

## Testing expectations

- Every public method in `Weft.Core` has unit tests.
- Every CLI command has at least one happy-path test using the mock harness (`CliTestHost` + NSubstitute).
- Integration tests (`test/Weft.Integration.Tests`) run in CI only; they skip locally without the `WEFT_INT_*` env vars.
- New safety gates (drop, history-loss, integrity) MUST have a test covering the refusal path.

## Running integration tests locally

```bash
export WEFT_INT_WORKSPACE='powerbi://api.powerbi.com/v1.0/myorg/weft-ci'
export WEFT_INT_DATABASE='TinyStatic'
export WEFT_INT_TENANT_ID='<tenant-guid>'
export WEFT_INT_CLIENT_ID='<app-guid>'
export WEFT_INT_CLIENT_SECRET='<secret>'
dotnet test test/Weft.Integration.Tests
```

## Filing bugs

Use `.github/ISSUE_TEMPLATE/bug_report.md`. Include:
- Weft version (`weft --version` once we ship it; or commit SHA).
- .NET SDK version (`dotnet --version`).
- `weft plan` output if the bug is deploy-related.
- Minimal `.bim` fixture if possible.

## Pull requests

1. Branch off `main`.
2. One logical change per PR; split big ones.
3. All PR builds go green before review.
4. Squash-merge unless commit-by-commit history adds value.

## Security

Do NOT file auth / secret vulnerabilities as public issues. Email the maintainer.

## License

By contributing, you agree your contributions are licensed under the project's MIT license.
