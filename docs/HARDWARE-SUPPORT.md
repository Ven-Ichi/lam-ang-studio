# Hardware support

[Documentation index](../README.md#documentation)

## Implemented target

The development target is a **RAKK Lam-Ang Lite** used with the Lite (2021)
FineTuner package. Its configuration collection is VID/PID `0C45:8006`, MI_00,
keyboard usage `0001:0006`, feature length 65, input length 9. The observed
manufacturer/product strings were `SONiX` / `USB DEVICE`, with USB device-version
field `0113`. That field has not been established to be a firmware version.

Windows can expose consumer/system/keyboard/mouse/vendor collections for the
same physical unit. Configuration and vendor-event interfaces are distinct.
Container GUIDs are discovered at runtime; no development-machine device path
or container GUID is embedded in the application or fixtures.

## Family and revision boundaries

RAKK publishes separate Lite, Lite (2021), Lite V2, Pro and Pro Max downloads.
Shared branding, a SONiX string or reused USB identifiers are insufficient to
transfer another board's firmware or protocol assumptions.

Manufacturer Lite pages advertise 87-key ANSI TKL, RGB, 1000 Hz, NKRO, FN media
and volume/brightness controls. These are manufacturer specifications, not an
independent measurement of the connected revision. USB passthrough is advertised
on the consulted ABS Lite page; applicability to every 2021/PBT unit is unresolved.

The Lite PBT manual documents standalone lighting adjustment/custom recording
and 18 loop effects. The installed software has 19 labels plus UserDefine.
The application retains the software surface and records the discrepancy.
[Primary sources](RESEARCH.md).

## Implemented versus unestablished

| Area | Current position |
| --- | --- |
| Stock programmable settings | Implemented from the original serializers/controls |
| Static, Breath, Scrolling transfers | Accepted on the development keyboard |
| Every mode's exact rendering | Not all physically verified |
| Macro/host-action playback | Implemented; not every path physically exercised |
| Power-cycle persistence / onboard bank count | Not established by the current checks |
| Full device settings backup | No complete readback implementation |
| QMK / VIA | No verified Lite port/compatibility from the consulted records |
| Live RGB streaming / audio/screen effects | Requires a separately verified transient update path |
| Selectable/higher polling or debounce | No recovered stock controls or measurements |
| Pro/Pro Max radios, TFT, other physical features | Not features software can add to this unit |

SonixQMK's inspected compatibility entry is for **Lam-Ang Pro**. Inspected
OpenRGB controllers use their own device-specific protocols and do not establish
stock Lite compatibility. The app does not flash firmware or reuse those command
sets merely because a name/vendor ID looks similar.
