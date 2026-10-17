# Research and provenance

[Documentation index](../README.md#documentation)

Research access date: **2026-10-17**. Manufacturer claims, controlled serializer
observations and successful live transactions are intentionally distinguished.

## Original application evidence

The inspected native PE32 `DeviceDriver.exe` came from the locally installed
FineTuner. SHA-256:

```text
c935f37e68853c95c94961e87f3bbbfd9d37d892137f201b8e838f82cff6361f
```

GNU objdump/static inspection identified feature-report paths, control helpers
and serialization. An isolated copy was later instrumented with its outgoing
HID writes blocked to obtain artificial packet fixtures and the pure usage/index
lookup. The new app is independent C#/WPF code; it does not load vendor code at runtime.

The original installer was identified as Inno Setup. Byte-for-byte equivalence
between that supplied installer payload and the installed executable was not
established. The original binaries, skins, fonts, databases, instrumentation
sessions and per-machine capture logs remain outside this repository.

See [fixture provenance](../tests/fixtures/README.md) and the
[machine-readable manifest](../tests/fixtures/manifest.json).

## Manufacturer sources

- [RAKK drivers and software](https://rakk.ph/drivers-and-softwares/): distinct
  Lite/Lite (2021)/Lite V2/Pro/Pro Max software entries.
- [RAKK manuals](https://rakk.ph/manuals/) and
  [Lite PBT manual](https://rakk.ph/wp-content/uploads/2023/05/Lam-ang-Lite-PBT-Manual_2.pdf):
  lighting controls, standalone custom recording and 18-name effect loop.
- [Lite ABS product](https://rakk.ph/product/rakk-lam-ang-lite-rgb-outemu-blue-red-mechanical-keyboard/)
  and [Lite PBT product](https://rakk.ph/product/rakk-lam-ang-lite-rgb-mechanical-keyboard-outemu-pbt-red-blue-white-keycaps/):
  manufacturer feature specifications, not measured performance of this revision.

No consulted manufacturer source supplied a complete revision-to-USB-ID protocol
crosswalk. The manual/software effect discrepancy remains unresolved.

## Upstream comparisons

- [SonixQMK compatibility](https://sonixqmk.github.io/SonixDocs/compatible_kb/)
  and [FAQ](https://sonixqmk.github.io/SonixDocs/faq/): the inspected RAKK entry
  is Pro; support/recovery depends on actual hardware and matching firmware.
- [OpenRGB source](https://github.com/CalcProgrammer1/OpenRGB/tree/7d5fecf55f71876f450f060b71971e9a4cbed44b/Controllers):
  inspected Sinowealth, EVision and QMK paths are device-specific. Their report
  layouts/handshakes do not establish stock Lite support.

These projects are research comparisons, not runtime dependencies or proof that
their firmware/commands can be used on this keyboard.

## Windows APIs

- [HidD_GetFeature](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/hidsdi/nf-hidsdi-hidd_getfeature):
  selected collection, report ID and buffer requirements.
- [Device container identity](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/devpkey-device-containerid):
  relating collections belonging to the same physical device.
- [Raw Input](https://learn.microsoft.com/en-us/windows/win32/inputdev/about-raw-input):
  device-specific input registration/observation.
- [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput):
  host text injection and Windows integrity/key-state constraints.

## Design

The UI follows the approved keyboard-first editor/inspector design, system
typography, explicit feedback and restrained colors. Apple HIG principles
informed hierarchy/accessibility/feedback; native Windows conventions and WPF
controls remain the implementation platform. This does not imply Apple APIs,
Apple platform compliance certification or use of original vendor graphics.
