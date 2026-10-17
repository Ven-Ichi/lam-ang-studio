# Contributing

This is a private application repository. Keep changes focused on the recovered
Lite protocol and document the evidence behind hardware claims.

## Setup and default verification

Read [Building](docs/BUILDING.md), then run:

```powershell
.\build.ps1
py -3 scripts/check-docs.py
```

The default build is hardware-free. Keep C# 5/.NET Framework compatibility,
framework-only production dependencies and embedded-resource packaging intact.
Python is only needed for the optional documentation/fixture tools.

## Changes involving the keyboard

Use [the protocol notes](docs/PROTOCOL.md) and independent fixtures. Add a
meaningful regression for changed bytes/state/error handling. Do not compute
expected payloads with the serializer being tested. A matching USB ID or a
successful OS write is insufficient to claim a new firmware feature works.

Run `-NativeChecks` only deliberately on matching hardware: it sends lighting
settings. Record whether evidence is a payload comparison, positive ACK,
physical observation, standalone playback or power-cycle result. Keep raw logs
local; publish only reduced artificial fixtures if needed.

## Source and data boundaries

- Do not add the original installer/executable, vendor graphics/fonts or databases.
- Do not commit real profiles, typed text, program paths, device-container IDs,
  capture sessions, credentials or machine-specific diagnostics.
- Preserve required JSON fields, UTF-8 byte bounds and atomic local replacement.
- Keep I/O out of the UI thread; retain bounded workers, explicit failure state
  and selected-physical-device action filtering.
- Keep ordinary builds/CI free of device writes and physical keyboard requirements.

## Documentation and releases

Update the relevant guide and [Changelog](CHANGELOG.md) for behavior changes.
Screenshots must show only the app with artificial data, not the surrounding desktop.
GitHub Actions builds the executable/checksum artifact; a repository owner can
publish those files as a private release. Review the verification scope in the
release notes rather than describing untested behavior as certified.

No public/open-source license is selected. Do not assume this private repository
grants redistribution rights to separately obtained vendor software.
