# UNIIZI TWEAKER 01

Windows gaming optimizer (C# / .NET 8 / WPF). **Build status: step 1 of 10** — see `docs/STATUS.md`
for exactly what works and what is still to do.

> This source was written without access to a Windows machine. It has **not** been compiled or run on
> Windows yet. The non-WPF C# code (services, view models, core logic) was compile-checked on Linux and the
> core logic assertions pass, but the first real Windows build happens in GitHub Actions (below).
> If that build reports errors, send them back and they will be fixed.

## Get the .exe without installing anything (GitHub Actions)

1. Create a free account at https://github.com and click **New repository** (name it e.g. `uniizi-tweaker-01`;
   public or private both work).
2. Put the project files in it. Easiest ways:
   - **Git/GitHub Desktop**: clone the empty repo, copy the content of this folder into it
     (including the hidden `.github` folder), commit and push.
   - **Web upload**: on the repo page choose *Add file → Upload files* and drag the content of this folder.
     If the `.github` folder is not uploaded, use *Add file → Create new file*, type
     `.github/workflows/build.yml` as the name and paste the content of that file.
3. Open the **Actions** tab. The workflow *Build UNIIZI TWEAKER 01* starts automatically after the push.
   (If not: select it and click **Run workflow**.)
4. Wait for the green check (a few minutes). Open the finished run, scroll to **Artifacts** and download
   `UNIIZI_TWEAKER_01-win-x64`. It is a zip containing `UNIIZI_TWEAKER_01.exe` and `SHA256.txt`.
5. Unzip and double-click `UNIIZI_TWEAKER_01.exe`. No installer, no .NET install needed (self-contained).

Windows SmartScreen may warn about an unsigned app: *More info → Run anyway*. The exe is not code-signed.

Optional: push a tag such as `v0.1.0` and the workflow also attaches the exe to a GitHub Release.

Artifacts are kept 90 days by default. A red run means a compile/test error: open the failing step and
copy the error text.

## Build locally (optional, needs Windows + .NET 8 SDK)

    dotnet test tests/UniiziTweaker01.Tests/UniiziTweaker01.Tests.csproj -c Release
    dotnet publish src/UniiziTweaker01/UniiziTweaker01.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish

The result is `publish/UNIIZI_TWEAKER_01.exe`.

## Layout

    src/UniiziTweaker01.Core/   pure logic (formatters, CPU calc, display-mode catalog) - unit tested
    src/UniiziTweaker01/        WPF app: Themes, Controls, Services (Windows APIs), ViewModels, Views
    tests/UniiziTweaker01.Tests xUnit tests for the core logic
    .github/workflows/build.yml Windows build + tests + exe artifact
    docs/                       STATUS.md (feature state) and MANUAL_TESTS.md

## Logs

`%LocalAppData%\UNIIZI\TWEAKER01\logs`

## Troubleshooting

- Red build in Actions: copy the failing step's error and send it.
- App does not start: check the log folder above.
- Resolution list is empty: Windows/driver reported no modes for that display; use *Windows display settings*.
