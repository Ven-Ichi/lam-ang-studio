"""Export artificial, write-blocked vendor captures as portable payload fixtures.

Optional research utility; the committed fixtures need no original executable.
Exports only bytes consumed by serializers, never device paths or runtime logs.
"""
import argparse
import hashlib
import json
from pathlib import Path


def export(source: Path, destination: Path) -> None:
    cases = [(f"effect-{i}", 1216) for i in range(1, 21)]
    cases += [("custom-caps-color", 1216), ("macro-events", 576)]
    cases += [(f"mapping-type-{i}", 704) for i in (1, 2, 3, 12)]
    destination.mkdir(parents=True, exist_ok=True)
    entries = []
    for name, captured_size in cases:
        capture = json.loads((source / f"{name}.json").read_text(encoding="utf-8"))
        record = next(r for r in capture["records"]
                      if r.get("kind") == "payload" and r["length"] == captured_size)
        if record.get("blocked") is not True:
            raise ValueError(f"{name}: expected a write-blocked capture")
        raw = bytes.fromhex(record["hex"])
        if len(raw) != captured_size:
            raise ValueError(f"{name}: invalid captured length")
        length = captured_size - 64 if captured_size > 65 else 64
        payload = raw[:length]
        fixture = {"Name": name, "PayloadLength": length, "PayloadHex": payload.hex()}
        (destination / f"{name}.json").write_text(
            json.dumps(fixture, indent=2) + "\n", encoding="utf-8")
        entries.append({"File": f"{name}.json", "PayloadLength": length,
                        "Sha256": hashlib.sha256(payload).hexdigest()})
    indices = json.loads((source / "indices.json").read_text(encoding="utf-8"))
    if len(indices) != 256 or any(not isinstance(i, int) or not 0 <= i < 160 for i in indices):
        raise ValueError("Invalid 256-entry key-index table")
    (destination / "indices.json").write_text(json.dumps(indices) + "\n", encoding="utf-8")
    manifest = {
        "SourceArtifact": "DeviceDriver.exe",
        "SourceSha256": "c935f37e68853c95c94961e87f3bbbfd9d37d892137f201b8e838f82cff6361f",
        "CaptureDate": "2026-10-17",
        "Method": "Original serializers; outgoing HID writes blocked; artificial settings",
        "Fixtures": entries,
    }
    (destination / "manifest.json").write_text(
        json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Exported {len(entries)} payload fixtures and the key-index table.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, help="Local vendor-capture directory")
    parser.add_argument("destination", type=Path, help="Portable fixture output directory")
    args = parser.parse_args()
    export(args.source, args.destination)
