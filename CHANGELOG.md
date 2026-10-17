# Changelog

## 0.1.0 — 2026-10-17

### Added

- Standalone native Windows x64 WPF application with direct HID configuration.
- Twenty FineTuner lighting entries and recovered mode-specific controls.
- Per-key RGB painting, key assignments, macro editor/recorder, gaming locks,
  local profiles, recovery and diagnostics.
- Isolated I/O worker with bounded lifetime, transaction mutex, matched ACK
  validation and a one-resend policy for explicit negative acknowledgements.
- Physical-container-filtered vendor actions and one resident dispatcher.
- Complete JSON schema checking, UTF-8 file bounds, atomic replacements and
  safeguards for recording release capacity and stale editor/request state.
- Portable artificial fixtures, Windows CI build and comprehensive documentation.

### Verification

- 63 offline protocol/profile assertions and 36 native UI assertions passed on
  the development machine.
- Static, Breath and retained Scrolling lighting operations accepted by the
  connected development keyboard; packaged CLI/native Apply paths exercised.

### Known limits

- English UI; text-color preference is session-local.
- Complete settings readback is not implemented; not every playback/power-cycle
  path has been physically verified.
- No QMK/VIA, firmware flashing, polling/debounce controls or arbitrary live RGB.
