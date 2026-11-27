"""Verify the reviewed historical evidence against its pinned checkout bytes."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import subprocess
from tempfile import TemporaryDirectory

MANIFEST = "Benchmarks/ReferenceIntegrity.json"
# Preserve the reviewed digest when comparing against revisions before the chart moved.
RELOCATED_EVIDENCE = {
    "Documentation/assets/size-study-8e.png": "Documentation/assets/8e/size-study-8e.png",
}
FROZEN = {
    "Frozen", "Frozen8C", "Frozen8D2", "Frozen8D3", "Frozen8D4", "Frozen8E",
}


def git(root, *args, check=True):
    return subprocess.run(["git", "-C", str(root), *args], check=check, capture_output=True)


def protected(path):
    if path in RELOCATED_EVIDENCE or path in RELOCATED_EVIDENCE.values():
        return True
    parts = PurePosixPath(path).parts
    if len(parts) < 3 or parts[0] != "Benchmarks" or ".." in parts:
        return False
    if parts[1] in FROZEN:
        return path.lower().endswith(".cs")
    if parts[1] != "Reference":
        return False
    return parts[2] == "Maps" or (
        len(parts) == 3 and PurePosixPath(path).suffix.lower() in {".json", ".jsonl", ".csv"}
    )


def read_manifest(content):
    manifest = json.loads(content)
    if manifest["version"] != 1 or not re.fullmatch(r"[0-9a-f]{40}", manifest["baseline"]):
        raise ValueError("Invalid archive manifest version or baseline.")
    entries = manifest["sha256"]
    if not entries or any(
        not protected(path) or not re.fullmatch(r"[0-9a-f]{64}", digest)
        for path, digest in entries.items()
    ):
        raise ValueError("Archive manifest contains an invalid path or digest.")
    return manifest


def verify(root, manifest, previous=None):
    data = git(root, "ls-files", "-z", "--cached", "--others", "--exclude-standard").stdout
    actual = {path for path in data.decode("utf-8").split("\0") if protected(path)}
    expected = manifest["sha256"]
    errors = [f"{path}: unexpected protected file" for path in sorted(actual - expected.keys())]
    for path, digest in sorted(expected.items()):
        file = root / path
        if path not in actual or not file.is_file() or file.is_symlink():
            errors.append(f"{path}: protected file missing or not a regular file")
        elif (observed := hashlib.sha256(file.read_bytes()).hexdigest()) != digest:
            errors.append(f"{path}: SHA-256 mismatch (expected {digest}, got {observed})")
    if previous:
        if manifest["baseline"] != previous["baseline"]:
            errors.append("The reviewed archive baseline changed.")
        for path, digest in sorted(previous["sha256"].items()):
            current_path = path if path in expected else RELOCATED_EVIDENCE.get(path, path)
            if expected.get(current_path) != digest:
                errors.append(f"{path}: existing manifest digest removed or changed")
    return errors


def self_test():
    with TemporaryDirectory(prefix="castrum-archive-") as directory:
        root = Path(directory)
        git(root, "init", "-q")
        path = "Benchmarks/Frozen8E/Fixture.cs"
        file = root / path
        file.parent.mkdir(parents=True)
        file.write_bytes(b"// frozen\n")
        added = file.with_name("Unreviewed.cs")
        manifest = {"baseline": "0" * 40, "sha256": {path: hashlib.sha256(file.read_bytes()).hexdigest()}}
        assert not verify(root, manifest)
        file.write_bytes(b"// changed\n")
        added.write_bytes(b"// unreviewed\n")
        errors = verify(root, manifest)
        assert any("SHA-256 mismatch" in error for error in errors)
        assert any("unexpected protected file" in error for error in errors)
        file.unlink()
        assert any("protected file missing" in error for error in verify(root, manifest))
        candidate = {"baseline": manifest["baseline"], "sha256": {}}
        assert any("existing manifest digest" in error for error in verify(root, candidate, manifest))
    print("Archive self-test passed for valid, changed, deleted and unexpected evidence.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", help="Protect existing manifest entries from this Git revision.")
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        self_test()
        return 0

    root = Path(git(Path.cwd(), "rev-parse", "--show-toplevel").stdout.decode().strip())
    manifest = read_manifest((root / MANIFEST).read_bytes())
    previous = None
    if args.base:
        tree = git(root, "ls-tree", args.base, "--", MANIFEST).stdout
        if tree:
            previous = read_manifest(git(root, "show", f"{args.base}:{MANIFEST}").stdout)
        else:
            if git(root, "rev-parse", args.base).stdout.decode().strip() != manifest["baseline"]:
                raise ValueError(f"{args.base}: missing reviewed archive manifest.")
            paths = git(root, "ls-tree", "-r", "--name-only", "-z", args.base).stdout.decode().split("\0")
            previous = {"baseline": manifest["baseline"], "sha256": {
                path: hashlib.sha256(git(root, "show", f"{args.base}:{path}").stdout).hexdigest()
                for path in paths if protected(path)
            }}

    errors = verify(root, manifest, previous)
    for error in errors:
        print(error)
    if errors:
        return 1
    print(f"Verified {len(manifest['sha256'])} protected historical files.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
