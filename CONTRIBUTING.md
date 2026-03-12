# Contributing

Thanks for taking an interest in AES!

## Getting Started

```bash
git clone <repo-url>
cd AzureDevOpsExcelSync
dotnet build src/AzureDevOpsExcelSync/AzureDevOpsExcelSync.csproj
```

There are no automated tests — verify changes manually with a real Azure DevOps connection.

## Branching

- `main` is always releasable — every merge triggers a release
- Work on short-lived branches: `feat/<name>` or `fix/<name>`
- Open a PR against `main`; keep it small and focused

## Making Changes

The app is a `partial class Program` split across multiple files. Add new commands by creating a new `Cmd*.cs` file following the existing pattern.

| File | Responsibility |
|------|---------------|
| `Program.cs` | Entry point, REPL loop, shared state |
| `Ui.cs` | Console helpers, banner, prompt |
| `Cmd*.cs` | One file per command (`/pull`, `/push`, etc.) |
| `UpdateCheck.cs` | NuGet update check at startup |
| `IAesPlugin.cs` / `AesContext.cs` / `PluginLoader.cs` | Plugin system |

## Versioning

Bump `Directory.Build.props` as part of your PR if the change warrants a release:

```xml
<Version>1.2.0</Version>
```

Follow [Semantic Versioning](https://semver.org): `MAJOR.MINOR.PATCH`.

| Change type | Version bump |
|-------------|-------------|
| Breaking change | Major |
| New command or feature | Minor |
| Bug fix or small improvement | Patch |

Merging to `main` automatically packs and publishes to NuGet via GitHub Actions. Requires `NUGET_API_KEY` set in repo secrets.

## Commit Style

Plain English, present tense, lowercase:

```
feat: add /archive command
fix: handle empty iteration path in /push
chore: bump version to 1.1.0
```

## Reporting Issues

Open a GitHub issue with:
- AES version (`/status` shows it in the banner)
- OS and .NET version (`dotnet --version`)
- Steps to reproduce
- Expected vs actual behaviour
