# Recovered stock HID protocol

[Documentation index](../README.md#documentation)

This describes the implemented serializer/transport and controlled original-app
observations. It is not a manufacturer protocol specification or a claim of
compatibility with other SONiX products. [Evidence boundaries](TESTING.md).

## Target and framing

The configuration collection is selected by the conjunction of:

- VID `0x0C45`, PID `0x8006`.
- Interface path containing `&mi_00#`.
- Usage page `0x0001`, usage `0x0006`.
- Feature-report length 65 and input-report length 9.

Windows handles are opened with desired access zero, read/write sharing and
`OPEN_EXISTING`. Each configuration report is **65 bytes**:

```text
byte 0       Report ID 0x00
bytes 1–64   64-byte payload
```

`HidD_SetFeature` sends a report. `HidD_GetFeature` reads a fresh zero-initialized
buffer when an acknowledgement is required. The transport does not reuse
prepopulated status bytes as evidence of a response.

## Short commands and transactions

Short control payloads are zero-filled, with `04` at payload byte 0, command at
byte 1, and the data-block count (when applicable) at byte 8.

| Section | Begin | Setup | Data | Commit | Additional finish |
| --- | --- | --- | --- | --- | --- |
| Lighting | `04 18` | `04 13`, count 18 | 18 × 64 bytes | `04 02` | `04 F0` |
| Key map | `04 18` | `04 11`, count 10 | 10 × 64 bytes | `04 02` | `04 F0` |
| Macros | `04 19` | `04 15`, calculated count | Aligned macro buffer | `04 02` | None |
| Gaming | `04 18` | `04 17`, count 1 | One acknowledged payload | `04 02` | None |

Begin/setup/commit/finish require acknowledgement. Bulk blocks do not each get
an ACK read; the single gaming data payload does. The implementation waits 6 ms
after writes and uses additional 30 ms intervals around macro bulk transfer.
These follow the recovered sender; they are not measured minimum safe timings
or an RGB frame-rate guarantee.

The original helper's bulk API accepted one extra 64-byte unit in its supplied
length. For example, it received 1216 while transmitting 1152 lighting bytes.
The new serializers return the **actual transmitted payload bytes** instead.

## ACK policy

For a positive ACK, the report must be 65 bytes, report ID zero, and report byte
4 must equal `01`. Control-command ACKs must also echo `04` and the requested
command at report bytes 1 and 2. Data ACK correlation is more limited, matching
the recovered available evidence.

A matching explicit status `FF` permits **one resend of that short report**,
following the original helper. A zero/missing response, read/write failure or
mismatched command is not blindly retried. A failed partial bulk transaction
does not restart automatically. The parent process bounds a stalled worker.

An ordinary successful lighting transaction has 22 writes and 4 positive ACKs;
a rejected-command resend can increase the write count. Counts/response bytes
are retained in diagnostics. None of this is complete settings readback.

## Lighting payload: 1152 bytes

| Payload region | Meaning |
| --- | --- |
| `0x000–0x13F` | Twenty 16-byte mode records |
| `0x140–0x1FF` | Reserved/zero in implemented layout |
| `0x200–0x43F` | 144 four-byte custom color records |
| `0x440–0x44F` | Active-mode record |
| Remaining bytes | Reserved/zero |

Normal mode IDs are 1–19. UserDefine's wire mode is `0x80`, although its editor
ID is 20. A 16-byte mode record uses:

| Offset | Value |
| ---: | --- |
| 0 | Mode |
| 1, 2, 3 | R, G, B |
| 4–7 | Reserved zero |
| 8 | Full Color flag |
| 9 | Brightness |
| 10 | Speed |
| 11 | Direction |
| 12–13 | Reserved zero |
| 14–15 | `AA 55` |

Direction values recovered from the original handler are Right `0`, Left `1`,
Up `2`, Down `3`; [availability is effect-specific](FEATURES.md).

The stored UserDefine mode record omits global RGB/speed/direction fields. Its
active record nevertheless retains selected-mode parameter fields, as shown by
the controlled capture. This distinction is explicitly tested.

Custom-color records contain `80 R G B` at `0x200 + matrix_index × 4`. A HID usage
must first pass through the embedded lookup; it is not itself a byte offset.
For example, Caps usage `57` maps to index `55`, Esc usage `41` to index `1`.
Lighting-off serialization uses zero data with the active-record sentinel.

## Key map: 640 bytes

Four-byte records are addressed by `matrix_index × 4`; `AA 55` is placed at
bytes 638–639. An absent/default override is zero-filled. Implemented records:

| Kind | Four-byte form |
| --- | --- |
| Disabled | `05 03 00 00` |
| Keyboard | `02 modifiers usage 00` |
| Three-key chord | `07 usage1 usage2 usage3` |
| Macro | `06 slot playbackFlag repeatCount` |
| Host action | `05 02 actionId 00` |

Mouse/media/Windows choices use the recovered action encodings in
[`Protocol.Mapping`](../src/Protocol.cs). Macro flags are 0 once, 1 repeated,
2 toggle; toggle uses a zero repeat byte. Host action IDs use the physical key's
matrix index and are resolved against the resident app's applied map.

## Macro buffer

The first 400 bytes are 100 little-endian 32-bit offsets. A macro's offset points
to an eight-byte header: 16-bit event-record count followed by reserved zeros.
An explicitly empty macro uses an `FFFFFFFF` slot value; unused slots remain zero.
Assigned macros must be nonempty.

Each four-byte event has the following form:

| Event | Bytes |
| --- | --- |
| Delay | `delayLo delayHi 00 50` |
| Key down | `00 00 usage B0` |
| Key up | `00 00 usage 30` |
| Mouse down / up | `00 00 buttonMask 90` / `00 00 buttonMask 10` |
| Wheel | `00 00 01-or-FF 20` |

Recorded delays are retained; a 10 ms delay is inserted between otherwise
undelayed events, following the original serializer. Fixed-delay mode supplies
the configured delay. Delays larger than 65535 ms are split into records.
The buffer is padded according to the recovered block convention, with `AA 55`
at its last two bytes. The maximum transmitted buffer is 3200 bytes; all macros
must fit together. Playback configuration belongs in the key-map binding.

## Gaming and host events

The 64-byte gaming payload is `0A`, enable, Alt+Tab lock, Alt+F4 lock, Windows-key
lock, reserved zeros, then `AA 55` at bytes 62–63. Disabled gaming clears flags.

A separate vendor input collection uses usage page `FFFF`, usage `1`, and
four-byte reports. The original listener checks report byte 0 for `05` and uses
byte 2 as action ID. The app filters by Windows physical device-container identity
before executing a configured host action. These input reports are not the
65-byte configuration transport.
