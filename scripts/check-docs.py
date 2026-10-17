"""Check tracked Markdown local links and fixture checksums without hardware."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
from urllib.parse import unquote

ROOT = Path(__file__).resolve().parents[1]


def main() -> None:
    tracked = subprocess.run(["git", "ls-files", "--cached", "--", "*.md"],
                             cwd=ROOT, capture_output=True, text=True, check=True)
    files = [ROOT / line for line in tracked.stdout.splitlines()]
    if not files:
        raise SystemExit("No tracked Markdown. Stage the intended documentation first.")
    errors = []
    for path in files:
        for target in re.findall(r"!?\[[^\]]*\]\(([^\s)]+)(?:\s+\"[^\"]*\")?\)",
                                 path.read_text(encoding="utf-8")):
            if target.startswith(("https://", "http://", "mailto:", "#")):
                continue
            local = unquote(target.split("#", 1)[0])
            if not (path.parent / local).exists():
                errors.append(f"{path.relative_to(ROOT)}: missing {target}")
    folder = ROOT / "tests" / "fixtures"
    manifest = json.loads((folder / "manifest.json").read_text(encoding="utf-8"))
    for entry in manifest["Fixtures"]:
        fixture = json.loads((folder / entry["File"]).read_text(encoding="utf-8"))
        payload = bytes.fromhex(fixture["PayloadHex"])
        if (len(payload) != fixture["PayloadLength"] or len(payload) != entry["PayloadLength"]
                or hashlib.sha256(payload).hexdigest() != entry["Sha256"]):
            errors.append(f"Invalid fixture: {entry['File']}")
    if errors:
        raise SystemExit("\n".join(errors))
    print(f"PASS: {len(files)} Markdown files' local links and "
          f"{len(manifest['Fixtures'])} fixture digests.")


if __name__ == "__main__":
    main()
