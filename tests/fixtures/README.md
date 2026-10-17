# Protocol fixtures

These are artificial, byte-exact configuration payloads observed from the
installed FineTuner `DeviceDriver.exe` serializers while their outgoing HID
writes were blocked. The original executable, database, UI assets and runtime
instrumentation are not included.

`manifest.json` records the original artifact digest, capture method, payload
lengths and SHA-256 digests. Fixtures contain only the transmitted 64-byte
payload blocks; the Windows report-ID byte is added by the new transport.
Unused bytes captured after the original helper's bulk-buffer convention are
removed. Device paths, container identifiers, process IDs, memory addresses,
usernames and real user profile content are not exported.

## Inputs represented

- `effect-1` through `effect-20`: start with factory-style local defaults and
  cumulatively update each mode to RGB `17,34,51`, single color, brightness `9`,
  speed `4`; direction `3` for Scrolling, `1` for Rolling/Flowing/Tilt, otherwise `0`.
- `custom-caps-color`: the accumulated settings above, plus Caps Lock painted
  RGB `17,34,51` in UserDefine. This checks matrix index `55` and channel order.
- `mapping-type-1`: Caps disabled.
- `mapping-type-2`: Caps mapped to HID usage `41` with the controlled modifier
  byte `59`. These are serializer test inputs, not recommended shortcuts.
- `mapping-type-12`: Caps bound to the three-key chord `41,58,59`.
- `mapping-type-3`: Caps bound to macro slot zero, repeated three times.
- `macro-events`: 100 ms delay, A down/up, left mouse down/up, including the
  original serializer's implicit 10 ms delays between undelayed events.
- `indices.json`: the original pure lookup function evaluated for usages `0–255`.

The tests compare the new serializers with these independent observations.
A fixture match does not prove physical rendering, playback or persistence on
every firmware revision. See [verification](../../docs/TESTING.md).

## Optional regeneration

For a separately obtained compatible set of local, write-blocked captures:

```powershell
py -3 scripts/export-fixtures.py evidence/vendor-capture tests/fixtures
```

Python is needed only for this optional conversion. Ordinary builds and tests
use the committed fixtures and do not need Python, Frida or FineTuner.
