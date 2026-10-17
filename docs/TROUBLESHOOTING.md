# Troubleshooting

[Documentation index](../README.md#documentation)

## Not connected

1. Connect the target keyboard by USB and press Refresh/F5.
2. Confirm it is the Lite revision this app targets, not Pro/Pro Max/V2.
3. If several matching devices appear, select the intended one.
4. Open Diagnostics to inspect the discovered collection/capabilities.

The app requires the matching keyboard control collection, not merely any HID
device. Its normal metadata/feature access was tested without a replacement
kernel driver. This is not a promise that another revision with similar branding
will match.

## Apply failed or timed out

Read the reported stage and actual response. Close FineTuner/other configuration
apps before reapplying, since they can send their own settings. Reconnect the
keyboard if its connection changed. A partial failure may have changed some
data; the app keeps the draft pending rather than claiming a successful restore.

Matching explicit `FF` rejection gets one bounded resend. Missing, stale or
mismatched replies do not trigger a blind transaction replay. A stalled worker
is bounded independently of the responsive editor. Logs are available through
Diagnostics and `%LOCALAPPDATA%\LamAngStudio\Logs\`.

## Color or direction is unavailable

- Turn Full Color off to choose a single color in applicable modes.
- Colourful and Spectrum define their own colors.
- Static and UserDefine do not use speed.
- Scrolling has Up/Down; Rolling/Flowing/Tilt have Left/Right.
- Select UserDefine to paint individual keys.

This is the original software's availability model, not missing generic controls.

## An assignment or macro does nothing

Stage the assignment and Apply; local Save is not device application. Create a
nonempty macro before assigning it. Validate capacity before uploading a large
macro. Program/website/text/profile actions need the resident app; reapply the
map after restart. Confirm paths/URLs/profile names still exist and check the
reported host-action error. An action from another physical keyboard is ignored.

Not every standalone playback path or firmware restriction has been physically
verified. Include a minimal reproduction and the acknowledgement/failure stage
when investigating a particular key or macro.

## Profile will not import/save/upload

Use a complete version-1 export from this application, not a partial JSON object
or vendor database. Check field/range errors and the 1 MiB UTF-8 limit. A large
local macro may be valid to edit/save but too large for the shared 3200-byte upload
buffer. Reduce/split events before Apply. Required release events also need room
before starting a new recording.

Backups and retained removed/recovered drafts are described in [Data format](DATA-FORMAT.md).

## Build fails

Confirm the framework compiler/WPF assemblies exist at the paths in
[Building](BUILDING.md). Use a full clone with committed `tests/fixtures` and
`src/key-indices.json`. The build does not depend on ignored local captures.
Close the same `dist` executable before overwriting it during compilation.

## Command-line diagnostics

GUI executables may return differently from console commands in interactive
shells. Use `Start-Process -Wait` and inspect the explicit JSON report:

```powershell
Start-Process -Wait -FilePath .\LamAngStudio.exe -ArgumentList '--diagnose "device-report.json"'
Start-Process -Wait -FilePath .\LamAngStudio.exe -ArgumentList '--apply "profile.json" lighting auto "apply-report.json"'
```

`--diagnose` does not write keyboard settings. `Success:true` with an empty
Devices list means enumeration completed but no matching target was found.
`--apply` writes the explicitly listed sections: `lighting,mapping,macros,gaming`.
`auto` requires exactly one matching keyboard; an explicit discovered device path
can be used instead. The output path must differ from the input profile path.
Exit zero means the operation succeeded; inspect Error/counts/receipts for failures.
Reports are outputs; choose a path intended for that report.

Close the resident UI before CLI key-map/macro changes. Lighting-only CLI changes
do not update an already-open editor's local draft; there is no automatic complete
device readback after another process changes configuration.

Exact Apply grammar:

```text
--apply <profile.json> <comma-separated-sections> <device-path-or-auto> <report.json>
```

Section tokens are exact lowercase names, without spaces. Unlike GUI Apply,
CLI `mapping` does not automatically add macro uploads. Playback/repeat descriptors
are also stored in the key map, so use **`mapping,macros` together** when changing
macro bindings or playback settings. A CLI mapping write does not initialize the
resident action map of a GUI opened later; reapply from that GUI for host actions.
