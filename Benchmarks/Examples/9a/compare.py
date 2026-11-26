"""Compare deterministic tournament contracts while excluding measured costs."""
import argparse
import hashlib
import json
from collections import Counter
from pathlib import Path


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


def stable(value):
    if isinstance(value, dict):
        elapsed_fields = {"elapsed", "milliseconds", "allocatedBytes"}
        return {k: stable(v) for k, v in value.items() if k not in elapsed_fields}
    if isinstance(value, list):
        return [stable(v) for v in value]
    return value


def digest(value):
    payload = json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False)
    return hashlib.sha256(payload.encode("utf-8")).hexdigest()


def compare(left, right):
    baseline = read(left / "manifest.json")
    current = read(right / "manifest.json")
    for field in ("schema", "config", "maps", "matches", "opponents", "runtime", "architecture"):
        assert baseline[field] == current[field], f"Manifest field differs: {field}"
    rows = []
    outcomes = Counter()
    for match in baseline["matches"]:
        name = match["id"] + ".json"
        expected = stable(read(left / "matches" / name)["payload"])
        actual = stable(read(right / "matches" / name)["payload"])
        if expected != actual:
            fields = sorted(k for k in expected.keys() | actual.keys() if expected.get(k) != actual.get(k))
            raise AssertionError(f"{match['id']} ({match['map']}) differs: {', '.join(fields)}")
        rows.append(expected)
        outcomes["retry" if expected["opening"] == "retry" else expected["status"]] += 1
    return {
        "matches": len(rows),
        "outcomes": outcomes,
        "rounds": sum(len(row["rounds"]) for row in rows),
        "scheduleSha256": digest(baseline["matches"]),
        "fullDeterministicResultsSha256": digest(rows),
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("baseline", type=Path)
    parser.add_argument("candidate", type=Path)
    args = parser.parse_args()
    print(json.dumps(compare(args.baseline, args.candidate), indent=2))
