# Lam-Ang Studio v0.1.0

Initial native Windows x64 build for the RAKK Lam-Ang Lite stock `0C45:8006`
configuration protocol.

## Download

- `LamAngStudio.exe`: standalone app, using installed .NET Framework 4.8 and
  Windows HID support. No vendor executable or additional runtime packages.
- `SHA256SUMS.txt`: checksum for this executable.

## Included

All 20 original lighting entries/conditional controls, UserDefine painting,
key mapping/combinations, macros, gaming locks, profiles, recovery and diagnostics.
The executable sends actual device settings; Save profile remains a local operation.

## Verification

63 offline protocol/profile assertions and 36 native UI assertions passed on
the development machine. The connected keyboard positively acknowledged complete
Static/Breath/Scrolling lighting transfers, including packaged app and native UI
Apply/restoration. Portable fixtures compare the serializers with artificial
write-blocked original-app observations.

ACKs are not proof of exact rendering for every effect, every playback path or
power-cycle retention. Complete settings readback is not implemented. English
is the current UI language and text-color changes are session-local. This is
stock-protocol configuration, not QMK/VIA, a firmware flasher or an RGB streaming engine.

See the repository README and documentation for usage, build steps, hardware
scope and diagnostics. The repository/release is private and requires access.
