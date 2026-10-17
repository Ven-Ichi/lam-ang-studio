# Building and packaging

[Documentation index](../README.md#documentation)

## Requirements

- Windows x64. The development/hardware run used Windows 11.
- .NET Framework 4.8 with its framework compiler and WPF assemblies.
- PowerShell; CI uses PowerShell 7 on `windows-2022`.
- Git and private-repository authentication for cloning. The README's
  `gh repo clone` example additionally requires installed/authenticated GitHub CLI.

The compiler is resolved at
`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`; WPF references are in
that directory's `WPF` subdirectory. Source uses C# 5-compatible syntax and
framework assemblies. No NuGet restore, .NET SDK or Visual Studio workload is
required. Python 3 is optional for fixture export and documentation checking.

## Commands

```powershell
.\build.ps1 -TestsOnly
.\build.ps1
```

The first compiles/runs the protocol/profile suite and builds the device helper.
The second additionally compiles the native app. Both use committed fixtures;
neither needs the original FineTuner installation or local `evidence/` captures.

| Output | Purpose |
| --- | --- |
| `dist/LamAngStudio.exe` | Standalone application and isolated worker entry point |
| `dist/SHA256SUMS.txt` | Checksum for the built application |
| `dist/ProtocolChecks.exe` | Offline protocol/profile suite |
| `dist/DeviceCheck.exe` | Discovery and explicit live-lighting helper |
| `dist/NativeUiChecks.exe` | Created only with opt-in native UI checks |

Do not rebuild a particular executable while that same output file is running.
Running a separately copied application does not lock the `dist` build output.

## Hardware checks

```powershell
.\build.ps1 -NativeChecks
```

This creates an isolated native test window/data directory, discovers a real
keyboard, sends lighting changes, and restores a **declared test lighting state**.
It cannot restore an unknown original state through a readback that does not
exist. Run deliberately with the target connected and other configuration apps
closed. [Detailed test boundaries](TESTING.md).

## CI

[Windows build](../.github/workflows/build.yml) runs on pushes to main, pull
requests and manual dispatch. It checks out source, executes `build.ps1`, and
uploads the app/checksum as a restricted repository artifact. It has read-only
contents permissions and does not run device-writing checks or publish releases.

## Releases and verification

The initial release is `v0.1.0`. Releases contain the standalone executable and
`SHA256SUMS.txt`; source history contains neither compiled/vendor executables
nor local captures. Downloading a private release requires repository access.

Compare a downloaded executable with the accompanying checksum:

```powershell
Get-FileHash .\LamAngStudio.exe -Algorithm SHA256
```

The build does not Authenticode-sign the executable. A checksum identifies the
downloaded bytes, not a publisher certificate. Source builds may have different
PE timestamps/checksums even with the same source; this is a repeatable build
procedure, not a claim of bit-for-bit reproducible binaries.
