# Features and effect controls

[Documentation index](../README.md#documentation)

The replacement retains the installed FineTuner's original names and recovered
control rules. Wire serialization is covered by artificial original-app captures;
hardware coverage is described separately in [Testing](TESTING.md).

## Lighting matrix

All entries have brightness. The app offers native levels `0–15` for normal
effects, `0–16` for UserDefine, and `0–10` for applicable speed.

| ID | Original label | Color | Speed | Direction |
| ---: | --- | --- | --- | --- |
| 1 | Static | Single / Full Color | No | None |
| 2 | SingleOn | Single / Full Color | Yes | None |
| 3 | SingleOff | Single / Full Color | Yes | None |
| 4 | Glittering | Single / Full Color | Yes | None |
| 5 | Falling | Single / Full Color | Yes | None |
| 6 | Colourful | Effect-defined | Yes | None |
| 7 | Breath | Single / Full Color | Yes | None |
| 8 | Spectrum | Effect-defined | Yes | None |
| 9 | Outward | Single / Full Color | Yes | None |
| 10 | Scrolling | Single / Full Color | Yes | Up / Down |
| 11 | Rolling | Single / Full Color | Yes | Left / Right |
| 12 | Rotating | Single / Full Color | Yes | None |
| 13 | Explode | Single / Full Color | Yes | None |
| 14 | Launch | Single / Full Color | Yes | None |
| 15 | Ripples | Single / Full Color | Yes | None |
| 16 | Flowing | Single / Full Color | Yes | Left / Right |
| 17 | Pulsating | Single / Full Color | Yes | None |
| 18 | Tilt | Single / Full Color | Yes | Left / Right |
| 19 | Shuttle | Single / Full Color | Yes | None |
| 20 | UserDefine | Separate per-key map | No | None |

`20` is the editor/profile ID; the device record uses `0x80` for UserDefine.
Original code disabled direction in the remaining modes. The official Lite PBT
manual lists 18 loop-effect names, excluding Shuttle/UserDefine from that loop;
the software/manual revision difference is unresolved. [Sources](RESEARCH.md).

## Assignments

- Default and disabled keys.
- Keyboard usage plus eight possible left/right modifier bits.
- Up to three-key chord usages.
- Macro slot and Once/Repeat/Toggle playback descriptor.
- Left/middle/right/back/forward mouse actions and scroll up/down.
- Play/pause, stop, previous/next, volume up/down and mute.
- Named Windows shortcuts, including copy/paste/cut, desktop, Explorer,
  lock, app switching, calculator, browser, e-mail and media-player actions.
- Resident host actions: program, website, text and local-profile switching.

Host actions require the running application and its acknowledged map. They are
not new onboard execution capabilities. Physical Fn/roller behavior that firmware
does not expose cannot be inferred from host input APIs.

## Macros, profiles and gaming

Macro editing includes naming, recording, explicit delays, keyboard/mouse/wheel
events, reorder/delete/clear and native playback configuration. Local event
validation and upload capacity are separate; a full transfer supports up to 100
index slots within the recovered 3200-byte buffer. This is not a promise that
100 macros of arbitrary length fit or that every playback path is verified.

Profiles include all four programmable sections and support local save/load,
duplicate/remove, JSON import/export and recovery. Hardware profile-bank count
is not established; a larger PC library does not create additional onboard banks.

Gaming mode has the three original locks: Windows key, Alt+Tab and Alt+F4.
Preferences provide appearance refresh, session text-color customization, profile
folder access and staged factory-style defaults. English is the implemented UI language.

## Potential enhancements, not current features

Undo/redo, searchable effect/action palettes, persistent appearance preferences,
additional translations and richer macro visualization are host-side improvements.
Per-application/scheduled profiles could wrap validated setting operations.

Audio/screen-reactive RGB or arbitrary animations first need a verified transient
frame-update path and measured behavior; repeated static configuration writes do
not establish one. QMK/VIA, firmware layers, debounce changes and higher polling
require additional Lite-specific firmware/hardware support, not just a new UI.
