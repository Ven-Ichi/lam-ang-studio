# Architecture

[Documentation index](../README.md#documentation)

## Components

```text
MainWindow (WPF editor / local draft)
    | validated Profile + selected path + changed section names
    v
DeviceWorker parent (same executable; redirected JSON pipes)
    | spawn --worker, enforce deadline, retain result/log
    v
WorkerMain -> Protocol serializers -> HidDevice -> Windows HID -> keyboard

Vendor HID event -> HostActions -> matching physical container + applied map
                                      -> configured host action
```

| File | Responsibility |
| --- | --- |
| [`Program.cs`](../src/Program.cs) | UI/CLI/worker entry points; one resident UI; resource loading |
| [`Models.cs`](../src/Models.cs) | Profiles, physical layout, editing/state helpers, validation and atomic storage |
| [`Protocol.cs`](../src/Protocol.cs) | Pure lighting, mapping, gaming and macro payload construction |
| [`HidDevice.cs`](../src/HidDevice.cs) | Discovery, identity, report I/O, transactions and bounded process worker |
| [`HostActions.cs`](../src/HostActions.cs) | Vendor Raw Input dispatch, opt-in recording hooks and text injection |
| [`MainWindow.cs`](../src/MainWindow.cs) | Editor controls, staged actions, pending/apply state, profiles and diagnostics |
| [`Theme.xaml`](../src/Theme.xaml) | Native control styles and semantic theme resources |
| [`key-indices.json`](../src/key-indices.json) | Embedded usage-to-device matrix lookup |

## Why a worker process

Synchronous native HID calls can stall inside a device/driver. Keeping them on
the UI thread would reproduce the original app's unbounded-wait problem. The
parent starts the same executable with `--worker`, sends JSON over stdin and
reads a JSON result. Stdin writes and output reads are asynchronous relative to
the bounded wait. The parent gives the worker 15 seconds, then requests termination
and waits a bounded additional interval. Unconfirmed termination is reported honestly.

The worker validates and serializes every requested section **before** opening
the keyboard. A named mutex protects transactions across app instances. No
automatic whole-transaction retry follows a partial failure; the editor keeps
the settings pending for explicit recovery.

## State model

The profile is a local draft. Selecting/previewing values is separate from
staging a key assignment, local saving and device application. `DeviceState`
remembers bytes only for sections acknowledged in this session. It is not a
complete device mirror.

Apply snapshots the request/revision/target. A later completion does not clear
newer profile edits or install an old device's action map into a new selection.
Discovery results cannot switch targets while Apply is in flight. Discard may
still leave pending differences against acknowledged bytes.

## Device and host-action identity

Discovery checks VID/PID, `MI_00`, keyboard usage and report lengths. Vendor
events arrive from a different top-level collection. Windows device-container
GUIDs associate those collections with the selected physical device; a shared
VID/PID alone does not authorize host-action dispatch.

Only one resident UI/action dispatcher runs. The independent configuration mutex
also prevents concurrent writes. CLI key-map/macro changes are refused while the
resident UI is present, avoiding competing ownership of the host-action map.

## Recording and storage

Global low-level keyboard/mouse hooks are installed only after recording starts
and removed on stop, view/profile replacement or application close. Space for
required release events is reserved. Ordinary injected input is excluded from
recording. Raw vendor-event listening is not ordinary global keystroke recording.

Local storage uses strict, complete version-1 JSON and a 1 MiB UTF-8 file limit.
Profile replacements are atomic and retain a backup. A large local macro can
remain editable even when the device's separate transfer limit rejects upload.
See [the data format](DATA-FORMAT.md) and [protocol](PROTOCOL.md).
