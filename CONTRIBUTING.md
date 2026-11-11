# Contributing

Pull requests targeting `main`, and pushes to `main`, run the [PR check](.github/workflows/pr-check.yml) workflow.

The check cancels a run when a newer commit supersedes it. For a pull request it refuses to continue when the branch conflicts with `main`, then checks out GitHub's merge ref so the commands run on the pull request merged with the latest base. It restores NuGet packages (cached between runs) and runs the commands this repo already documents:

```powershell
dotnet test .\MxClickHaptics.slnx -nologo --verbosity minimal
dotnet build .\MxClickHaptics.slnx -nologo --verbosity minimal
```

There is no separate lint or type-check script. Nullable reference types are checked by the build. The job uses `windows-latest` because the desktop app targets `net10.0-windows` and a Linux runner cannot build that project with the command above.

`dotnet build` of the solution also compiles the Logitech plugin. That project stops the build when `C:\Program Files\Logi\LogiPluginService\PluginApi.dll` is missing. GitHub-hosted runners do not have that SDK, so the build step fails on the current default branch. The workflow still runs it. `dotnet test` covers the xUnit project and does not require the plugin DLL.

A maintainer should require the **Build and test** status check in branch protection for `main` before merge. This repository's workflow does not change those settings. Requiring the check as it stands will block merges until the solution build can succeed on the runner.
