# Profiles, validation and local data

[Documentation index](../README.md#documentation)

## Complete version-1 schema

Profiles use the exact PascalCase member names written by `ProfileStore.Encode`.
Required collections/members are checked before deserialization can substitute
constructor defaults. Missing sections, explicit null data, unknown versions and
invalid values are rejected.

| Root member | Meaning |
| --- | --- |
| `Version` | `1` |
| `Name` | Nonblank, at most 120 characters |
| `Effect` | Selected editor effect ID, 1–20 |
| `LightingOn` | Boolean |
| `GamingEnabled`, `LockAltTab`, `LockAltF4`, `LockWindows` | Booleans |
| `Lights` | Exactly 20 effect-setting objects, in ID order |
| `Colors` | Custom key-color objects |
| `Assignments` | Physical-key overrides |
| `Macros` | Local macro definitions |

Every `Lights` object has `Red`, `Green`, `Blue` (0–255), `Brightness` (0–15;
UserDefine 0–16), `Speed` (0–10), `Direction` (0–3) and `FullColor` (Boolean).
Validation's numeric direction range does not mean every effect supports every
direction. The UI applies [mode-specific availability](FEATURES.md).

Colors require `Usage`, `Red`, `Green`, `Blue`. Usages identify distinct physical
keys from the supported layout. Duplicate custom colors or assignments are rejected.

Assignments require `Usage`, `Kind`, `Value`, `Modifiers`, `Chord`, `Target`:

| `Kind` | Used values |
| --- | --- |
| `Default`, `Disabled` | Physical `Usage` |
| `Keyboard` | Destination usage 1–255; modifier bitmask 0–255 |
| `Chord` | `Chord`: 1–3 usages, each 1–255 |
| `Macro` | Zero-based `Value` slot pointing to a nonempty macro |
| `Mouse`, `Media`, `Windows` | Named action IDs emitted by the UI; see serializer/source |
| `Program` | Absolute program path in `Target` |
| `Website` | Absolute HTTP/HTTPS URL in `Target` |
| `Text` | `Target`, at most 10,000 characters |
| `Profile` | Local profile name in `Target` |

Unused members are still part of the complete exported object. Physical usage,
destination usage, matrix index and Windows VK code are different identifiers.
Do not substitute one for another when editing JSON.

Every macro requires `Name`, `Playback`, `DelayMode`, `Repeats`, `FixedDelay`,
`Events`. Playback is `Once`, `Repeat` or `Toggle`; delays are `Recorded` or
`Fixed`; repeat count is 1–255 and fixed delay 1–999999 ms. Up to 600 local events
per macro and 100 macro definitions are accepted, subject to separate upload capacity.

Events require `Kind` and `Value`:

- `Delay`: 1–999999 ms; long delays are split into 16-bit device records.
- `Key down` / `Key up`: HID usage 4–231.
- `Mouse down` / `Mouse up`: button index 1–5.
- `Wheel`: +1 or -1.

Prefer exporting from the application to obtain a complete editable profile.
The app's JSON format is not the original vendor SQLite database format.

## Storage

Under `%LOCALAPPDATA%\LamAngStudio\`:

| Path | Contents |
| --- | --- |
| `Profiles\` | Local JSON library; replacements retain `.backup` files |
| `Draft.json` | Editor draft saved on close |
| `Last acknowledged.json` | Whole request-draft snapshot after a successful operation; only requested sections were acknowledged |
| `Logs\` | Transaction counts, responses and failure information |
| `Removed profiles\` | Removed library entries retained for recovery |
| `Recovered drafts\` | Prior draft retained before a keyboard-triggered profile switch |

Files are limited to **1 MiB UTF-8**, with byte-count checking before replacing
the previous usable file. Profile saving uses a temporary sibling and atomic
move/replacement. A successful local save is not an Apply operation.

Profiles and diagnostics are local, unencrypted data. Text/program/website
assignments contain the configured target/content, and live diagnostics can
contain device identifiers and local paths. These runtime folders and original
captures are excluded from this repository.

### Recover a local profile

`Draft.json` is automatically loaded when available at startup. To recover a
backup or retained removed/recovered profile, copy it to a filename ending in
`.json` if needed, use Import JSON, inspect it and Save profile. Apply is separate
and changes the device. Keep the retained original until recovery is confirmed.

`Last acknowledged.json` contains the **entire draft snapshot** associated with
a successful request, even when that request sent only some sections. It can
therefore include fields that were never applied. Consult the matching transaction
receipts for scope; do not treat the file as a full device backup.
